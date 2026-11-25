using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace GameHomework.Core
{
    /// <summary>%AppData%\GameHomework\data.json 에 저장/불러오기</summary>
    public class DataStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() }
        };

        public string FolderPath { get; }
        public string FilePath => Path.Combine(FolderPath, "data.json");

        public DataStore(string? folder = null)
        {
            FolderPath = folder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameHomework");
        }

        public AppData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var data = JsonSerializer.Deserialize<AppData>(json, Options);
                    if (data != null)
                    {
                        Normalize(data);
                        return data;
                    }
                }
            }
            catch
            {
                // 파일이 깨졌으면 백업해 두고 기본값으로 시작
                try { File.Copy(FilePath, FilePath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
            }
            var fresh = DefaultData.Create();
            Save(fresh);
            return fresh;
        }

        public void Save(AppData data)
        {
            Directory.CreateDirectory(FolderPath);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
            if (File.Exists(FilePath))
                File.Replace(tmp, FilePath, FilePath + ".bak", true);
            else
                File.Move(tmp, FilePath);
        }

        public static string Serialize(AppData data) => JsonSerializer.Serialize(data, Options);
        public static AppData? Deserialize(string json) => JsonSerializer.Deserialize<AppData>(json, Options);

        private static void Normalize(AppData data)
        {
            data.Games ??= new();
            data.Events ??= new();
            data.Pickups ??= new();
            data.WelcomeShown ??= true;   // 예전부터 쓰던 데이터면 안내 안 띄움
            data.Completions ??= new();
            data.Widget ??= new WidgetSettings();
            data.Widget.Placements ??= new();
            data.Widget.EnsureSlots();   // 위젯 칸 (예전 합쳐서 보기 설정은 여기서 옮김)
            foreach (var g in data.Games)
            {
                g.Characters ??= new();
                g.Tasks ??= new();
                g.ExeNames ??= new();
                foreach (var t in g.Tasks)
                {
                    t.ResetDays ??= new();
                    t.HiddenDays ??= new();
                    t.CharacterIds ??= new();
                    t.CharacterLabels ??= new();
                }
            }

            PurgeExpired(data, DateTime.Now);
        }

        /// <summary>
        /// 끝난 이벤트/목표를 데이터에서 삭제한다. 삭제한 게 있으면 true.
        /// - 기한 이벤트: 완료한 것만, 마감일 다음 날 게임 초기화 시각이 지나면
        ///   (완료 못 한 것은 "놓친 이벤트"로 남아서 사용자가 확인 후 삭제)
        /// - 무기한 목표: 완료 체크한 뒤 그 게임의 초기화 시각이 지나면
        /// - 게임이 삭제된 이벤트
        /// </summary>
        public static bool PurgeExpired(AppData data, DateTime now)
        {
            var games = data.Games.ToDictionary(g => g.Id);
            int removed = data.Events.RemoveAll(e =>
                !games.TryGetValue(e.GameId, out var game) || ResetCalculator.ShouldAutoDelete(e, game, now));
            bool pickups = PickupCalc.Purge(data, now);   // 끝난 픽업도 자동 삭제
            return removed > 0 || pickups;
        }
    }
}

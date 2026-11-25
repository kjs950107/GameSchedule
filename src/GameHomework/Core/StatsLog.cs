using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace GameHomework.Core
{
    /// <summary>기록 종류</summary>
    public static class LogType
    {
        /// <summary>앱 시작 (자동 실행이면 사실상 PC를 켠 시각)</summary>
        public const string Start = "start";
        /// <summary>절전에서 깨어남</summary>
        public const string Resume = "resume";
        public const string Check = "check";
        public const string Uncheck = "uncheck";
        public const string EventDone = "event_done";
        public const string EventUndone = "event_undone";
        /// <summary>놓친 이벤트를 확인하고 삭제함</summary>
        public const string EventMissed = "event_missed";
        /// <summary>게임 실행 한 번 (시작~종료, 켜둔 시간/맨 앞 시간)</summary>
        public const string Play = "play";
    }

    /// <summary>기록 한 줄. 종류에 따라 쓰는 칸만 채운다 (나머지는 저장 안 함)</summary>
    public class LogRecord
    {
        public string Type { get; set; } = "";
        /// <summary>기록 시각 (play는 시작 시각)</summary>
        public DateTime T { get; set; }

        public Guid? GameId { get; set; }
        public string? Game { get; set; }

        // 숙제 체크
        public Guid? TaskId { get; set; }
        public string? Task { get; set; }
        public ResetKind? Kind { get; set; }
        public Guid? CharId { get; set; }
        public string? Char { get; set; }

        // 이벤트
        public Guid? EventId { get; set; }
        public string? EventName { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>완료했을 때 마감까지 남은 일수 (0 = 마감 당일)</summary>
        public int? DaysLeft { get; set; }

        // 플레이
        public DateTime? End { get; set; }
        public double? RunSec { get; set; }
        public double? FgSec { get; set; }
        /// <summary>숙제 없는 게임이었는지 (기록 당시)</summary>
        public bool? PlayOnly { get; set; }

        [JsonIgnore]
        public string CheckKey => TaskId + ":" + CharId;
    }

    /// <summary>%AppData%\GameHomework\logs\yyyy-MM.jsonl 에 한 줄씩 쌓는다</summary>
    public class StatsLog
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public string FolderPath { get; }

        public StatsLog(string dataFolder)
        {
            FolderPath = Path.Combine(dataFolder, "logs");
        }

        public string FileFor(DateTime t) => Path.Combine(FolderPath, t.ToString("yyyy-MM") + ".jsonl");

        public void Append(LogRecord record)
        {
            Directory.CreateDirectory(FolderPath);
            File.AppendAllText(FileFor(record.T), JsonSerializer.Serialize(record, Options) + "\n", new UTF8Encoding(false));
        }

        public void AppendMany(IEnumerable<LogRecord> records)
        {
            foreach (var r in records) Append(r);
        }

        /// <summary>from 이후 기록 전부 (시간순). 깨진 줄은 건너뜀</summary>
        public List<LogRecord> ReadAll(DateTime? from = null)
        {
            var list = new List<LogRecord>();
            if (!Directory.Exists(FolderPath)) return list;
            foreach (var file in Directory.GetFiles(FolderPath, "*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
            {
                // 파일 이름(yyyy-MM)으로 기간 밖 달은 건너뜀. play는 시작 시각 기준 파일에 있으므로 한 달 여유
                if (from != null && DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM",
                        null, System.Globalization.DateTimeStyles.None, out var month)
                    && month.AddMonths(2) < from.Value)
                    continue;
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var r = JsonSerializer.Deserialize<LogRecord>(line, Options);
                        if (r != null && (from == null || (r.End ?? r.T) >= from.Value)) list.Add(r);
                    }
                    catch (JsonException) { }
                }
            }
            return list.OrderBy(r => r.T).ToList();
        }

        // ---------- 진행 중인 플레이 세션 (앱이 갑자기 꺼져도 잃지 않도록) ----------

        private string OpenFile => Path.Combine(FolderPath, "play-open.json");

        public void SaveOpenSessions(IEnumerable<PlaySession> sessions)
        {
            Directory.CreateDirectory(FolderPath);
            var tmp = OpenFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(sessions.ToList(), Options));
            File.Move(tmp, OpenFile, true);
        }

        /// <summary>지난번에 끝나지 않은 세션을 마지막으로 확인된 시각에 끝난 것으로 기록</summary>
        public int RecoverOpenSessions()
        {
            if (!File.Exists(OpenFile)) return 0;
            try
            {
                var sessions = JsonSerializer.Deserialize<List<PlaySession>>(File.ReadAllText(OpenFile), Options) ?? new();
                AppendMany(sessions.Select(s => s.ToRecord()));
                File.Delete(OpenFile);
                return sessions.Count;
            }
            catch (Exception)
            {
                try { File.Delete(OpenFile); } catch { }
                return 0;
            }
        }

        public static string ToCsv(IEnumerable<LogRecord> records)
        {
            static string Q(object? v)
            {
                var s = v switch
                {
                    null => "",
                    DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss"),
                    double x => Math.Round(x / 60, 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => v.ToString() ?? ""
                };
                return s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            }
            var sb = new StringBuilder();
            sb.AppendLine("종류,시각,게임,숙제,종류(초기화),캐릭터,이벤트,마감일,남은일수,종료시각,켜둔시간(분),맨앞시간(분),숙제없는게임");
            foreach (var r in records)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Q(r.Type), Q(r.T), Q(r.Game), Q(r.Task), Q(r.Kind), Q(r.Char), Q(r.EventName),
                    Q(r.EndDate?.ToString("yyyy-MM-dd")), Q(r.DaysLeft), Q(r.End), Q(r.RunSec), Q(r.FgSec), Q(r.PlayOnly)
                }));
            }
            return sb.ToString();
        }
    }
}

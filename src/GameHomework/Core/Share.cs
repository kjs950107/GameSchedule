using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameHomework.Core
{
    /// <summary>공유 파일 종류 — 탭 하나 단위</summary>
    public enum ShareType { Events, Cycles, Pickups }

    /// <summary>
    /// 공유용 파일 (이벤트·목표 / 주기 컨텐츠 / 픽업 중 하나).
    /// 게임은 이름으로 적는다 (사람마다 게임 ID가 다르므로). 완료 체크는 넣지 않는다.
    /// </summary>
    public class SharePackage
    {
        public const string FormatName = "GameHomeworkShare";
        /// <summary>파일 형식 표시 (백업 파일과 구분). 저장할 때 채워진다</summary>
        public string Format { get; set; } = "";
        public int Version { get; set; } = 1;
        public ShareType Type { get; set; }
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public string? AppVersion { get; set; }
        public List<ShareEvent> Events { get; set; } = new();
        public List<ShareCycle> Cycles { get; set; } = new();
        public List<SharePickup> Pickups { get; set; } = new();

        [JsonIgnore]
        public int Count => Type switch
        {
            ShareType.Events => Events.Count,
            ShareType.Cycles => Cycles.Count,
            _ => Pickups.Count
        };
    }

    public class ShareEvent
    {
        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public bool NoDeadline { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class ShareCycle
    {
        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public int Weeks { get; set; } = 4;
        /// <summary>내보낸 시점의 "이번 회차" 마지막 날</summary>
        public DateTime CurrentEnd { get; set; }
        public DayOfWeek EndDay { get; set; }
        /// <summary>RPG 캐릭터마다 하는 컨텐츠였는지</summary>
        public bool PerCharacter { get; set; }
    }

    public class SharePickup
    {
        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>가져오기 미리보기 한 줄</summary>
    public class ImportItem
    {
        public string GameName { get; init; } = "";
        public string Name { get; init; } = "";
        /// <summary>"~12/10", "4주마다 월요일까지 · 이번 회차 12/7까지" 등</summary>
        public string Detail { get; init; } = "";
        public object Source { get; init; } = null!;
    }

    public enum ImportState
    {
        /// <summary>추가할 수 있음</summary>
        Ok,
        /// <summary>연결할 게임을 고르지 않음 (건너뛰기)</summary>
        NoGame,
        /// <summary>같은 내용이 이미 있음</summary>
        Duplicate,
        /// <summary>기간이 이미 끝남</summary>
        Expired,
        /// <summary>그 게임에는 넣을 수 없음 (기한 이벤트 → 모바일 게임만)</summary>
        NotAllowed
    }

    public class ImportResult
    {
        public int Added { get; set; }
        public int Skipped { get; set; }
    }

    public static class ShareCodec
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() }
        };

        public static string TypeName(ShareType t) => t switch
        {
            ShareType.Events => "이벤트·목표",
            ShareType.Cycles => "주기 컨텐츠",
            _ => "픽업"
        };

        public static string Serialize(SharePackage p)
        {
            p.Format = SharePackage.FormatName;
            return JsonSerializer.Serialize(p, Options);
        }

        /// <summary>공유 파일 읽기. 형식이 아니면 null (백업 파일 등)</summary>
        public static SharePackage? Parse(string json)
        {
            try
            {
                var p = JsonSerializer.Deserialize<SharePackage>(json, Options);
                if (p == null || p.Format != SharePackage.FormatName) return null;
                p.Events ??= new();
                p.Cycles ??= new();
                p.Pickups ??= new();
                return p;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // ---------- 내보내기 ----------

        public static SharePackage ExportEvents(IEnumerable<EventQuest> events, AppData data) => new()
        {
            Type = ShareType.Events,
            Events = events.Select(e => new ShareEvent
            {
                Game = GameName(data, e.GameId),
                Name = e.Name ?? "",
                NoDeadline = e.NoDeadline,
                EndDate = e.NoDeadline ? null : e.EndDate.Date
            }).ToList()
        };

        public static SharePackage ExportCycles(IEnumerable<(Game Game, HomeworkTask Task)> cycles, DateTime now) => new()
        {
            Type = ShareType.Cycles,
            Cycles = cycles.Select(c => new ShareCycle
            {
                Game = c.Game.Name,
                Name = c.Task.Name,
                Weeks = ResetCalculator.CycleWeeksOf(c.Task),
                CurrentEnd = ResetCalculator.CycleCurrentEnd(c.Task, c.Game, now),
                EndDay = c.Task.CycleEndDay,
                PerCharacter = c.Game.Type == GameType.Rpg && c.Task.Scope == TaskScope.PerCharacter
            }).ToList()
        };

        public static SharePackage ExportPickups(IEnumerable<PickupBanner> pickups, AppData data) => new()
        {
            Type = ShareType.Pickups,
            Pickups = pickups.Select(p => new SharePickup
            {
                Game = GameName(data, p.GameId),
                Name = p.Name ?? "",
                StartDate = p.StartDate.Date,
                EndDate = p.EndDate.Date
            }).ToList()
        };

        private static string GameName(AppData data, Guid id) => data.Games.FirstOrDefault(g => g.Id == id)?.Name ?? "";

        // ---------- 가져오기 ----------

        /// <summary>미리보기 목록</summary>
        public static List<ImportItem> Items(SharePackage p) => p.Type switch
        {
            ShareType.Events => p.Events.Select(e => new ImportItem
            {
                GameName = e.Game,
                Name = string.IsNullOrWhiteSpace(e.Name) ? (e.NoDeadline ? "목표" : "이벤트") : e.Name,
                Detail = e.NoDeadline || e.EndDate == null ? "[목표] 무기한" : $"[이벤트] ~{e.EndDate:yyyy'-'MM'-'dd}",
                Source = e
            }).ToList(),
            ShareType.Cycles => p.Cycles.Select(c => new ImportItem
            {
                GameName = c.Game,
                Name = c.Name,
                Detail = $"{Math.Clamp(c.Weeks, 1, 26)}주마다 {ResetCalculator.DayName(c.EndDay)}요일까지" + (c.PerCharacter ? " · 캐릭터마다" : ""),
                Source = c
            }).ToList(),
            _ => p.Pickups.Select(x => new ImportItem
            {
                GameName = x.Game,
                Name = x.Name,
                Detail = $"{x.StartDate:yyyy'-'MM'-'dd} ~ {x.EndDate:yyyy'-'MM'-'dd}",
                Source = x
            }).ToList()
        };

        /// <summary>이름 비교용: 공백·기호 빼고 소문자</summary>
        public static string Normalize(string s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        /// <summary>게임 이름으로 자동 연결 (똑같은 이름 → 공백·기호 무시하고 같은 이름 → 한쪽이 다른 쪽을 포함). 못 찾으면 null</summary>
        public static Game? MatchGame(string name, AppData data)
        {
            var candidates = data.Games.Where(g => g.Type != GameType.PlayOnly).ToList();
            var exact = candidates.FirstOrDefault(g => g.Name.Trim() == (name ?? "").Trim());
            if (exact != null) return exact;
            var n = Normalize(name ?? "");
            if (n.Length == 0) return null;
            var norm = candidates.FirstOrDefault(g => Normalize(g.Name) == n);
            if (norm != null) return norm;
            // "명조" ↔ "명조: 워더링 웨이브" — 하나만 걸릴 때만
            var partial = candidates.Where(g =>
            {
                var gn = Normalize(g.Name);
                return gn.Length > 0 && (gn.StartsWith(n) || n.StartsWith(gn));
            }).ToList();
            return partial.Count == 1 ? partial[0] : null;
        }

        /// <summary>이 항목을 target 게임에 넣을 수 있는지</summary>
        public static ImportState State(ImportItem item, Game? target, AppData data, DateTime now)
        {
            if (target == null) return ImportState.NoGame;
            var gd = ResetCalculator.GameDate(now, target.GetResetTimeOfDay());
            switch (item.Source)
            {
                case ShareEvent e:
                    if (!e.NoDeadline && e.EndDate != null)
                    {
                        if (target.Type != GameType.Mobile) return ImportState.NotAllowed;
                        if (e.EndDate.Value.Date < gd) return ImportState.Expired;
                    }
                    bool dupE = data.Events.Any(x => x.GameId == target.Id &&
                        Normalize(x.Name) == Normalize(e.Name) &&
                        x.NoDeadline == (e.NoDeadline || e.EndDate == null) &&
                        (x.NoDeadline || x.EndDate.Date == e.EndDate?.Date));
                    return dupE ? ImportState.Duplicate : ImportState.Ok;

                case ShareCycle c:
                    bool dupC = target.Tasks.Any(t => t.Kind == ResetKind.Cycle && Normalize(t.Name) == Normalize(c.Name));
                    return dupC ? ImportState.Duplicate : ImportState.Ok;

                case SharePickup p:
                    if (p.EndDate.Date < gd) return ImportState.Expired;
                    bool dupP = data.Pickups.Any(x => x.GameId == target.Id &&
                        Normalize(x.Name) == Normalize(p.Name) &&
                        x.StartDate.Date == p.StartDate.Date && x.EndDate.Date == p.EndDate.Date);
                    return dupP ? ImportState.Duplicate : ImportState.Ok;
            }
            return ImportState.NotAllowed;
        }

        public static string StateText(ImportState s) => s switch
        {
            ImportState.Ok => "추가",
            ImportState.NoGame => "건너뜀 (게임 없음)",
            ImportState.Duplicate => "이미 있음",
            ImportState.Expired => "이미 끝남",
            _ => "넣을 수 없음 (기한 이벤트는 모바일 게임만)"
        };

        /// <summary>
        /// 고른 항목을 추가한다. mapping: 파일의 게임 이름 → 받는 사람의 게임 (null = 건너뛰기).
        /// 완료 체크 없이 새로 추가, 이미 있거나 끝난 건 건너뜀.
        /// </summary>
        public static ImportResult Apply(IEnumerable<ImportItem> selected, IReadOnlyDictionary<string, Game?> mapping, AppData data, DateTime now)
        {
            var r = new ImportResult();
            foreach (var item in selected)
            {
                mapping.TryGetValue(item.GameName, out var game);
                if (State(item, game, data, now) != ImportState.Ok || game == null) { r.Skipped++; continue; }
                switch (item.Source)
                {
                    case ShareEvent e:
                        bool goal = e.NoDeadline || e.EndDate == null;
                        data.Events.Add(new EventQuest
                        {
                            GameId = game.Id,
                            Name = e.Name ?? "",
                            NoDeadline = goal,
                            EndDate = goal ? default : e.EndDate!.Value.Date
                        });
                        break;

                    case ShareCycle c:
                        var task = new HomeworkTask { Name = c.Name, Kind = ResetKind.Cycle };
                        // 파일이 오래돼서 그 회차가 이미 지났으면 같은 주기로 지금 회차까지 넘긴다
                        var probe = new HomeworkTask
                        {
                            Kind = ResetKind.Cycle, CycleFirstEnd = c.CurrentEnd.Date,
                            CycleWeeks = Math.Clamp(c.Weeks, 1, 26), CycleEndDay = c.EndDay
                        };
                        var end = ResetCalculator.CycleEndOnOrAfter(probe, ResetCalculator.GameDate(now, game.GetResetTimeOfDay()));
                        ResetCalculator.ApplyCycle(task, game, end, c.Weeks, c.EndDay, now, isNew: true);
                        task.Scope = c.PerCharacter && game.Type == GameType.Rpg && game.Characters.Count > 0
                            ? TaskScope.PerCharacter : TaskScope.Common;
                        game.Tasks.Add(task);
                        break;

                    case SharePickup p:
                        data.Pickups.Add(new PickupBanner
                        {
                            GameId = game.Id,
                            Name = p.Name ?? "",
                            StartDate = p.StartDate.Date,
                            EndDate = p.EndDate.Date
                        });
                        break;
                }
                r.Added++;
            }
            return r;
        }
    }
}

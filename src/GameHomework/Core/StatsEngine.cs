using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    public class StatsOptions
    {
        /// <summary>이 시각 이후 기록만 (null = 전체)</summary>
        public DateTime? From { get; set; }
        /// <summary>특정 게임만 (null = 전체 게임)</summary>
        public Guid? GameId { get; set; }
        /// <summary>숙제 없는 게임(하스스톤 등)을 플레이 시간 통계에 넣을지</summary>
        public bool IncludePlayOnly { get; set; } = true;
        /// <summary>지금 시각 (요일별 평균의 날짜 수 계산용, 테스트에서 고정)</summary>
        public DateTime? Now { get; set; }
    }

    /// <summary>요일별 게임 시간 (월=0 … 일=6)</summary>
    public class WeekdayPlayStat
    {
        public string Game { get; set; } = "";
        public bool PlayOnly { get; set; }
        /// <summary>요일별 켜둔 시간 합계(초)</summary>
        public double[] RunSec { get; set; } = new double[7];
        /// <summary>요일별 화면 맨 앞 시간 합계(초)</summary>
        public double[] FgSec { get; set; } = new double[7];
        public double Total => RunSec.Sum();
    }

    public class PlayStat
    {
        public Guid GameId { get; set; }
        public string Game { get; set; } = "";
        public bool PlayOnly { get; set; }
        public double RunSec { get; set; }
        public double FgSec { get; set; }
    }

    public class FirstGameStat
    {
        public string Game { get; set; } = "";
        public int Days { get; set; }
        public double Ratio { get; set; }
    }

    public class WeekdayStat
    {
        /// <summary>"로스트아크 · 레이드 1"</summary>
        public string Name { get; set; } = "";
        /// <summary>월~일 순서 완료 횟수</summary>
        public int[] Counts { get; set; } = new int[7];
        /// <summary>주간 초기화 요일 (월=0 … 일=6), 없으면 -1</summary>
        public int ResetIndex { get; set; } = -1;
        public int Total => Counts.Sum();
        public int TopIndex => Total == 0 ? -1 : Array.IndexOf(Counts, Counts.Max());
    }

    public class EventStat
    {
        public string Game { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime EndDate { get; set; }
        public DateTime? DoneAt { get; set; }
        /// <summary>완료 시 마감까지 남은 일수, 놓쳤으면 null</summary>
        public int? DaysLeft { get; set; }
        public bool Missed => DoneAt == null;
    }

    /// <summary>일일 숙제 순서 — 게임별 평균 몇 번째로 했는지</summary>
    public class OrderAvgStat
    {
        public string Game { get; set; } = "";
        public double AvgRank { get; set; }
        /// <summary>일일 숙제를 한 날 수</summary>
        public int Days { get; set; }
    }

    /// <summary>일일 숙제 순서 — 하루 동안 게임을 한 순서 그대로, 같은 순서가 나온 날 수</summary>
    public class OrderSeqStat
    {
        public List<string> Games { get; set; } = new();
        public int Days { get; set; }
    }

    public class StatsResult
    {
        // 첫 체크까지
        public List<double> FirstCheckMinutes { get; } = new();
        // 체크 간격
        public List<double> GapMinutes { get; } = new();
        public static readonly string[] GapLabels = { "~5분", "5~15분", "15~30분", "30~60분", "1~2시간", "2시간~" };
        public int[] GapBuckets { get; } = new int[6];
        // 주간 요일
        public List<WeekdayStat> Weekdays { get; } = new();
        // 이벤트
        public List<EventStat> Events { get; } = new();
        // 플레이
        public List<PlayStat> Play { get; } = new();
        public int PlayDays { get; set; }

        /// <summary>실제로 게임한 시간(초): 게임이 하나라도 켜져 있던 시간. 동시에 켠 시간은 한 번만 셈</summary>
        public double ActualPlaySec { get; set; }
        /// <summary>두 개 이상 동시에 켜져 있던 시간(초) — 게임별 합계가 실제보다 길어지는 만큼</summary>
        public double OverlapSec { get; set; }
        public List<FirstGameStat> FirstGames { get; } = new();

        // 요일별 게임 시간
        public List<WeekdayPlayStat> WeekdayPlay { get; } = new();
        /// <summary>요일별 실제로 게임한 시간(초) — 동시에 켠 시간은 한 번만</summary>
        public double[] WeekdayActualSec { get; } = new double[7];
        /// <summary>기간 안에 그 요일이 몇 번 있었는지 (평균 계산용)</summary>
        public int[] WeekdayDayCount { get; } = new int[7];
        public int FirstGameDays { get; set; }
        // 일일 숙제 순서
        public List<OrderAvgStat> DailyOrderAvg { get; } = new();
        public List<OrderSeqStat> DailyOrderSeq { get; } = new();
        public int DailyOrderDays { get; set; }

        public double TotalRunSec => Play.Sum(p => p.RunSec);
        public double TotalFgSec => Play.Sum(p => p.FgSec);
        public double TotalRunSec_Homework => Play.Where(p => !p.PlayOnly).Sum(p => p.RunSec);
        public double TotalRunSec_PlayOnly => Play.Where(p => p.PlayOnly).Sum(p => p.RunSec);

        public bool IsEmpty => FirstCheckMinutes.Count == 0 && GapMinutes.Count == 0 && Weekdays.Count == 0
                               && Events.Count == 0 && Play.Count == 0 && DailyOrderDays == 0;
    }

    /// <summary>기록(LogRecord)으로 통계를 계산한다</summary>
    public static class StatsEngine
    {
        /// <summary>하루를 나누는 기준 (새벽 4시 이전은 전날로 침)</summary>
        public static readonly TimeSpan DayCutoff = TimeSpan.FromHours(4);

        public static DateTime StatDay(DateTime t) => (t - DayCutoff).Date;

        public static int MondayIndex(DayOfWeek d) => ((int)d + 6) % 7;

        /// <summary>체크 후 해제한 것을 빼고 실제로 남은 체크만 (시간순)</summary>
        public static List<LogRecord> EffectiveChecks(IEnumerable<LogRecord> records)
        {
            var result = new List<LogRecord>();
            foreach (var r in records.OrderBy(r => r.T))
            {
                if (r.Type == LogType.Check) result.Add(r);
                else if (r.Type == LogType.Uncheck)
                {
                    // 같은 칸의 가장 최근 체크를 취소
                    int idx = result.FindLastIndex(c => c.CheckKey == r.CheckKey);
                    if (idx >= 0) result.RemoveAt(idx);
                }
            }
            return result;
        }

        public static StatsResult Compute(IReadOnlyList<LogRecord> all, AppData data, StatsOptions opt)
        {
            var res = new StatsResult();
            var records = all.Where(r => opt.From == null || (r.End ?? r.T) >= opt.From.Value).OrderBy(r => r.T).ToList();
            bool GameMatch(LogRecord r) => opt.GameId == null || r.GameId == opt.GameId;

            var checks = EffectiveChecks(records);
            var starts = records.Where(r => r.Type == LogType.Start || r.Type == LogType.Resume).Select(r => r.T).ToList();

            // ---- 1. PC를 켜고(앱 시작/깨어남) 첫 체크까지 ----
            for (int i = 0; i < starts.Count; i++)
            {
                var s = starts[i];
                DateTime until = i + 1 < starts.Count ? starts[i + 1] : DateTime.MaxValue;
                var first = checks.FirstOrDefault(c => c.T >= s && c.T < until && GameMatch(c));
                if (first == null) continue;
                var min = (first.T - s).TotalMinutes;
                if (min <= 12 * 60) res.FirstCheckMinutes.Add(min); // 12시간 넘게 지나서 체크한 건 제외
            }

            // ---- 2. 체크 간격 (같은 켜짐 구간 안에서만) ----
            var scoped = checks.Where(GameMatch).ToList();
            for (int i = 1; i < scoped.Count; i++)
            {
                var a = scoped[i - 1];
                var b = scoped[i];
                bool restarted = starts.Any(s => s > a.T && s <= b.T);
                if (restarted) continue;
                double min = (b.T - a.T).TotalMinutes;
                res.GapMinutes.Add(min);
                int bucket = min < 5 ? 0 : min < 15 ? 1 : min < 30 ? 2 : min < 60 ? 3 : min < 120 ? 4 : 5;
                res.GapBuckets[bucket]++;
            }

            // ---- 3. 주간(요일·월간 포함) 숙제를 깬 요일 ----
            var gamesById = data.Games.ToDictionary(g => g.Id);
            foreach (var grp in scoped.Where(c => c.Kind != null && c.Kind != ResetKind.Daily)
                                      .GroupBy(c => (c.GameId, c.TaskId)))
            {
                var any = grp.Last();
                gamesById.TryGetValue(any.GameId ?? Guid.Empty, out var game);
                var task = game?.Tasks.FirstOrDefault(t => t.Id == any.TaskId);
                var stat = new WeekdayStat
                {
                    Name = $"{game?.Name ?? any.Game} · {task?.Name ?? any.Task}"
                };
                foreach (var c in grp)
                {
                    // 게임 기준 날짜의 요일 (로아 수요일 새벽 3시 = 화요일)
                    var day = game != null ? ResetCalculator.GameDate(c.T, game.GetResetTimeOfDay()) : c.T.Date;
                    stat.Counts[MondayIndex(day.DayOfWeek)]++;
                }
                if (game != null && task != null)
                {
                    if (task.Kind == ResetKind.Weekly) stat.ResetIndex = MondayIndex(game.WeeklyResetDay);
                    else if (task.Kind == ResetKind.Weekdays && task.ResetDays.Count > 0)
                        stat.ResetIndex = MondayIndex(ResetCalculator.OrderDays(task.ResetDays).First());
                }
                res.Weekdays.Add(stat);
            }
            res.Weekdays.Sort((x, y) => y.Total.CompareTo(x.Total));

            // ---- 4. 이벤트 (기한 있는 것만) ----
            var evState = new Dictionary<Guid, EventStat>();
            foreach (var r in records.Where(r => r.EventId != null && GameMatch(r)))
            {
                var id = r.EventId!.Value;
                if (r.Type == LogType.EventDone)
                    evState[id] = new EventStat { Game = r.Game ?? "", Name = r.EventName ?? "", EndDate = r.EndDate ?? r.T, DoneAt = r.T, DaysLeft = r.DaysLeft };
                else if (r.Type == LogType.EventUndone)
                    evState.Remove(id);
                else if (r.Type == LogType.EventMissed)
                    evState[id] = new EventStat { Game = r.Game ?? "", Name = r.EventName ?? "", EndDate = r.EndDate ?? r.T };
            }
            res.Events.AddRange(evState.Values.OrderByDescending(e => e.EndDate));

            // ---- 5. 플레이 시간 ----
            var plays = records.Where(r => r.Type == LogType.Play && GameMatch(r))
                               .Where(r => opt.IncludePlayOnly || r.PlayOnly != true)
                               .ToList();
            foreach (var grp in plays.GroupBy(r => r.GameId))
            {
                gamesById.TryGetValue(grp.Key ?? Guid.Empty, out var game);
                res.Play.Add(new PlayStat
                {
                    GameId = grp.Key ?? Guid.Empty,
                    Game = game?.Name ?? grp.Last().Game ?? "",
                    PlayOnly = game != null ? game.Type == GameType.PlayOnly : grp.Last().PlayOnly == true,
                    RunSec = grp.Sum(r => r.RunSec ?? 0),
                    FgSec = grp.Sum(r => r.FgSec ?? 0)
                });
            }
            res.Play.Sort((x, y) => y.RunSec.CompareTo(x.RunSec));
            res.PlayDays = plays.Select(r => StatDay(r.T)).Distinct().Count();

            // 실제로 게임한 시간: 세션 구간을 겹치지 않게 합침 (기간 시작 이전 부분은 잘라냄)
            var intervals = plays
                .Select(r => (Start: opt.From != null && r.T < opt.From.Value ? opt.From.Value : r.T, End: r.End ?? r.T))
                .Where(x => x.End > x.Start)
                .ToList();
            res.ActualPlaySec = UnionSeconds(intervals);
            res.OverlapSec = Math.Max(0, intervals.Sum(x => (x.End - x.Start).TotalSeconds) - res.ActualPlaySec);

            // ---- 요일별 게임 시간 (하루 = 새벽 4시 기준, 날을 넘긴 세션은 날짜별로 나눔) ----
            var pieces = new List<(Guid? GameId, DateTime Day, DateTime S, DateTime E, double Run, double Fg)>();
            foreach (var r in plays)
            {
                var s0 = opt.From != null && r.T < opt.From.Value ? opt.From.Value : r.T;
                var e0 = r.End ?? r.T;
                double len = (e0 - s0).TotalSeconds;
                if (len <= 0) continue;
                double fullLen = Math.Max(1, ((r.End ?? r.T) - r.T).TotalSeconds);
                // 켜둔 시간/맨 앞 시간은 구간 길이 비율로 나눔 (절전 등으로 실제 길이와 다를 수 있어서)
                double runRate = (r.RunSec ?? fullLen) / fullLen;
                double fgRate = (r.FgSec ?? 0) / fullLen;
                var cur = s0;
                while (cur < e0)
                {
                    var day = StatDay(cur);
                    var dayEnd = day.AddDays(1) + DayCutoff;
                    var pieceEnd = e0 < dayEnd ? e0 : dayEnd;
                    double sec = (pieceEnd - cur).TotalSeconds;
                    pieces.Add((r.GameId, day, cur, pieceEnd, sec * runRate, sec * fgRate));
                    cur = pieceEnd;
                }
            }
            foreach (var grp in pieces.GroupBy(x => x.GameId))
            {
                gamesById.TryGetValue(grp.Key ?? Guid.Empty, out var game);
                var stat = new WeekdayPlayStat
                {
                    Game = game?.Name ?? plays.LastOrDefault(r => r.GameId == grp.Key)?.Game ?? "",
                    PlayOnly = game?.Type == GameType.PlayOnly
                };
                foreach (var pc in grp)
                {
                    int wd = MondayIndex(pc.Day.DayOfWeek);
                    stat.RunSec[wd] += pc.Run;
                    stat.FgSec[wd] += pc.Fg;
                }
                res.WeekdayPlay.Add(stat);
            }
            res.WeekdayPlay.Sort((x, y) => y.Total.CompareTo(x.Total));
            foreach (var dayGrp in pieces.GroupBy(x => x.Day))
                res.WeekdayActualSec[MondayIndex(dayGrp.Key.DayOfWeek)] += UnionSeconds(dayGrp.Select(x => (x.S, x.E)));
            if (pieces.Count > 0)
            {
                // 기간 안 요일 수: 기간 시작(전체면 첫 기록) ~ 오늘
                var firstDay = opt.From != null ? StatDay(opt.From.Value) : pieces.Min(x => x.Day);
                var lastDay = StatDay(opt.Now ?? DateTime.Now);
                if (lastDay < pieces.Max(x => x.Day)) lastDay = pieces.Max(x => x.Day);
                for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
                    res.WeekdayDayCount[MondayIndex(d.DayOfWeek)]++;
            }

            // ---- 6. 먼저 시작하는 게임 (숙제 없는 게임 제외, 게임 필터와 무관하게 전체 비교) ----
            var firstPerDay = records.Where(r => r.Type == LogType.Play && r.PlayOnly != true)
                .Where(r => !(r.GameId is Guid gid && gamesById.TryGetValue(gid, out var gg) && gg.Type == GameType.PlayOnly))
                .GroupBy(r => StatDay(r.T))
                .Select(g => g.OrderBy(r => r.T).First())
                .ToList();
            res.FirstGameDays = firstPerDay.Count;

            // ---- 7. 일일 숙제 하는 순서 (게임 필터와 무관하게 전체 게임 비교) ----
            // 하루(새벽 4시 기준) 동안 게임별 첫 "일일 숙제" 체크 시각 순서
            var dayOrders = checks
                .Where(c => c.Kind == ResetKind.Daily && c.GameId != null)
                .GroupBy(c => StatDay(c.T))
                .Select(day => day.GroupBy(c => c.GameId!.Value)
                                  .Select(g => (GameId: g.Key, First: g.Min(c => c.T), Name: g.Last().Game ?? ""))
                                  .OrderBy(x => x.First)
                                  .ToList())
                .ToList();
            res.DailyOrderDays = dayOrders.Count;
            string NameOf(Guid id, string fallback) => gamesById.TryGetValue(id, out var gg) ? gg.Name : fallback;
            var ranks = new Dictionary<Guid, List<int>>();
            var names = new Dictionary<Guid, string>();
            foreach (var order in dayOrders)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    var (id, _, name) = order[i];
                    if (!ranks.TryGetValue(id, out var list)) ranks[id] = list = new List<int>();
                    list.Add(i + 1);
                    names[id] = NameOf(id, name);
                }
            }
            res.DailyOrderAvg.AddRange(ranks
                .Select(kv => new OrderAvgStat { Game = names[kv.Key], AvgRank = kv.Value.Average(), Days = kv.Value.Count })
                .OrderBy(x => x.AvgRank).ThenByDescending(x => x.Days));
            res.DailyOrderSeq.AddRange(dayOrders
                .Select(o => o.Select(x => NameOf(x.GameId, x.Name)).ToList())
                .GroupBy(o => string.Join("\u0001", o))
                .Select(g => new OrderSeqStat { Games = g.First(), Days = g.Count() })
                .OrderByDescending(x => x.Days).ThenByDescending(x => x.Games.Count)
                .Take(5));
            foreach (var grp in firstPerDay.GroupBy(r => r.GameId).OrderByDescending(g => g.Count()))
            {
                gamesById.TryGetValue(grp.Key ?? Guid.Empty, out var game);
                res.FirstGames.Add(new FirstGameStat
                {
                    Game = game?.Name ?? grp.First().Game ?? "",
                    Days = grp.Count(),
                    Ratio = firstPerDay.Count == 0 ? 0 : (double)grp.Count() / firstPerDay.Count
                });
            }
            return res;
        }

        /// <summary>시간 구간들을 합쳤을 때의 총 길이(초). 겹친 부분은 한 번만</summary>
        public static double UnionSeconds(IEnumerable<(DateTime Start, DateTime End)> intervals)
        {
            double total = 0;
            DateTime? curStart = null, curEnd = null;
            foreach (var (s, e) in intervals.Where(x => x.End > x.Start).OrderBy(x => x.Start))
            {
                if (curEnd == null || s > curEnd.Value)
                {
                    if (curEnd != null) total += (curEnd.Value - curStart!.Value).TotalSeconds;
                    curStart = s;
                    curEnd = e;
                }
                else if (e > curEnd.Value)
                {
                    curEnd = e;
                }
            }
            if (curEnd != null) total += (curEnd.Value - curStart!.Value).TotalSeconds;
            return total;
        }

        public static double Median(IEnumerable<double> values)
        {
            var v = values.OrderBy(x => x).ToList();
            if (v.Count == 0) return 0;
            return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
        }

        /// <summary>"3시간 40분", "45분", "0분"</summary>
        public static string FormatDuration(double seconds)
        {
            var m = (int)Math.Round(seconds / 60);
            if (m < 60) return $"{m}분";
            return m % 60 == 0 ? $"{m / 60}시간" : $"{m / 60}시간 {m % 60}분";
        }

        public static readonly string[] DayNames = { "월", "화", "수", "목", "금", "토", "일" };
    }
}

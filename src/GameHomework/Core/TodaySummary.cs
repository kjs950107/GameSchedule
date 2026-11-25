using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    public class TodayGameStat
    {
        public string Game { get; set; } = "";
        public bool PlayOnly { get; set; }
        public double RunSec { get; set; }
        public double FgSec { get; set; }
        /// <summary>지금 켜져 있음</summary>
        public bool Running { get; set; }
    }

    /// <summary>오늘(새벽 4시 기준) 요약: PC 켠 시각, 실제 게임 시간(실행 중 포함), 숙제 진행</summary>
    public class TodaySummary
    {
        public DateTime Day { get; set; }
        /// <summary>이번에 PC(앱)를 켠 시각 — 마지막 시작/절전 해제</summary>
        public DateTime? PcOnAt { get; set; }
        public TimeSpan OnFor { get; set; }
        /// <summary>오늘 실제로 게임한 시간 (동시에 켠 시간은 한 번만, 실행 중 포함)</summary>
        public double ActualPlaySec { get; set; }
        public double FgSec { get; set; }
        public List<TodayGameStat> Games { get; } = new();
        public int DailyDone { get; set; }
        public int DailyTotal { get; set; }
        public int WeeklyDone { get; set; }
        public int WeeklyTotal { get; set; }
        public int ChecksToday { get; set; }

        public IEnumerable<string> RunningGames => Games.Where(g => g.Running).Select(g => g.Game);

        /// <param name="records">기록 파일 (오늘 것이 들어 있으면 됨)</param>
        /// <param name="open">지금 켜져 있는 게임 세션 (아직 기록 파일에 안 들어간 것)</param>
        public static TodaySummary Build(IEnumerable<LogRecord> records, IEnumerable<PlaySession> open, AppData data, DateTime now)
        {
            var day = StatsEngine.StatDay(now);
            var dayStart = day + StatsEngine.DayCutoff;
            var list = records.ToList();
            var s = new TodaySummary { Day = day };

            // PC 켠 시각: 지금 이전의 마지막 시작/절전 해제
            var lastStart = list.Where(r => (r.Type == LogType.Start || r.Type == LogType.Resume) && r.T <= now)
                                .OrderBy(r => r.T).LastOrDefault();
            if (lastStart != null)
            {
                s.PcOnAt = lastStart.T;
                s.OnFor = now - lastStart.T;
            }

            // 게임 시간: 끝난 세션 + 실행 중 세션, 오늘(04:00~) 부분만
            var openList = open.ToList();
            var sessions = list.Where(r => r.Type == LogType.Play)
                               .Select(r => (GameId: r.GameId, Game: r.Game, T: r.T, End: r.End ?? r.T, RunSec: r.RunSec, FgSec: r.FgSec, PlayOnly: r.PlayOnly == true, Running: false))
                               .Concat(openList.Select(o => (GameId: (Guid?)o.GameId, Game: (string?)o.Game, T: o.Start, End: o.LastSeen, RunSec: (double?)o.RunSec, FgSec: (double?)o.FgSec, PlayOnly: o.PlayOnly, Running: true)))
                               .ToList();
            var intervals = new List<(DateTime, DateTime)>();
            var gamesById = data.Games.ToDictionary(g => g.Id);
            var byGame = new Dictionary<Guid, TodayGameStat>();
            foreach (var x in sessions)
            {
                var st = x.T < dayStart ? dayStart : x.T;
                var en = x.End > now ? now : x.End;
                if (en <= st) continue;
                double full = Math.Max(1, (x.End - x.T).TotalSeconds);
                double part = (en - st).TotalSeconds / full;
                intervals.Add((st, en));
                var id = x.GameId ?? Guid.Empty;
                if (!byGame.TryGetValue(id, out var gs))
                {
                    gamesById.TryGetValue(id, out var game);
                    gs = new TodayGameStat
                    {
                        Game = game?.Name ?? x.Game ?? "",
                        PlayOnly = game != null ? game.Type == GameType.PlayOnly : x.PlayOnly
                    };
                    byGame[id] = gs;
                }
                gs.RunSec += (x.RunSec ?? full) * part;
                gs.FgSec += (x.FgSec ?? 0) * part;
                gs.Running |= x.Running;
            }
            s.Games.AddRange(byGame.Values.OrderByDescending(g => g.RunSec));
            s.ActualPlaySec = StatsEngine.UnionSeconds(intervals);
            s.FgSec = s.Games.Sum(g => g.FgSec);

            // 숙제 진행 (지금 상태)
            foreach (var g in data.Games)
            {
                var d = ProgressBuilder.Build(g, data, now, WidgetKind.Daily);
                s.DailyDone += d.DoneCount;
                s.DailyTotal += d.Total;
                var w = ProgressBuilder.Build(g, data, now, WidgetKind.Weekly);
                s.WeeklyDone += w.DoneCount;
                s.WeeklyTotal += w.Total;
            }
            s.ChecksToday = StatsEngine.EffectiveChecks(list).Count(c => StatsEngine.StatDay(c.T) == day);
            return s;
        }

        /// <summary>트레이 아이콘 툴팁용 한 줄 (짧게)</summary>
        public string ShortLine() =>
            $"오늘 게임 {StatsEngine.FormatDuration(ActualPlaySec)} · 일일 {DailyDone}/{DailyTotal}";
    }
}

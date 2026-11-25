using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    public enum PickupStatus { Upcoming, Active, Ended }

    /// <summary>위젯에 보여줄 한 게임의 픽업들</summary>
    public class GamePickups
    {
        public Game Game { get; init; } = null!;
        public List<PickupBanner> Active { get; init; } = new();
        public List<PickupBanner> Upcoming { get; init; } = new();
    }

    /// <summary>픽업 기간 계산 — 날짜는 게임 기준 날짜 (초기화 시각 전이면 전날)</summary>
    public static class PickupCalc
    {
        private static DateTime Gd(Game game, DateTime now) => ResetCalculator.GameDate(now, game.GetResetTimeOfDay());

        public static PickupStatus Status(PickupBanner p, Game game, DateTime now)
        {
            var gd = Gd(game, now);
            if (gd < p.StartDate.Date) return PickupStatus.Upcoming;
            return gd <= p.EndDate.Date ? PickupStatus.Active : PickupStatus.Ended;
        }

        /// <summary>남은 일수 (0 = 오늘이 마지막 날)</summary>
        public static int DaysLeft(PickupBanner p, Game game, DateTime now) => (p.EndDate.Date - Gd(game, now)).Days;

        /// <summary>시작까지 남은 일수</summary>
        public static int DaysToStart(PickupBanner p, Game game, DateTime now) => (p.StartDate.Date - Gd(game, now)).Days;

        /// <summary>게임별로 진행 중 / 예정 픽업 (끝난 건 제외). 게임 순서는 설정 순서</summary>
        public static List<GamePickups> ForWidget(AppData data, DateTime now)
        {
            var result = new List<GamePickups>();
            foreach (var game in data.Games.Where(g => g.Type != GameType.PlayOnly))
            {
                var mine = data.Pickups.Where(p => p.GameId == game.Id).ToList();
                if (mine.Count == 0) continue;
                var active = mine.Where(p => Status(p, game, now) == PickupStatus.Active)
                                 .OrderBy(p => p.EndDate).ThenBy(p => p.CreatedAt).ToList();
                var upcoming = mine.Where(p => Status(p, game, now) == PickupStatus.Upcoming)
                                   .OrderBy(p => p.StartDate).ThenBy(p => p.CreatedAt).ToList();
                if (active.Count + upcoming.Count == 0) continue;
                result.Add(new GamePickups { Game = game, Active = active, Upcoming = upcoming });
            }
            return result;
        }

        /// <summary>끝난 픽업 / 게임이 지워진 픽업 삭제. 지운 게 있으면 true</summary>
        public static bool Purge(AppData data, DateTime now)
        {
            var games = data.Games.ToDictionary(g => g.Id);
            return data.Pickups.RemoveAll(p =>
                !games.TryGetValue(p.GameId, out var game) || Status(p, game, now) == PickupStatus.Ended) > 0;
        }

        /// <summary>"D-5 · ~12/10" / "12/3 시작 (D-2)"</summary>
        public static string PeriodText(PickupBanner p, Game game, DateTime now) =>
            Status(p, game, now) == PickupStatus.Upcoming
                ? $"{p.StartDate:M'/'d} 시작 (D-{DaysToStart(p, game, now)})"
                : $"{ProgressBuilder.FormatDDay(DaysLeft(p, game, now))} · ~{p.EndDate:M'/'d}";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    /// <summary>
    /// 초기화 계산. 타이머로 체크를 지우는 대신 "마지막 완료 시각 >= 직전 초기화 시점"이면 완료로 본다.
    /// 그래서 프로그램이 꺼져 있던 동안 초기화가 지나가도 항상 정확하다.
    /// </summary>
    public static class ResetCalculator
    {
        /// <summary>게임 기준 날짜: 초기화 시각 이전이면 전날로 본다. (06:00 초기화면 새벽 3시는 전날)</summary>
        public static DateTime GameDate(DateTime now, TimeSpan resetTime) => (now - resetTime).Date;

        /// <summary>now 시점 기준으로 가장 최근에 지나간 초기화 시점</summary>
        public static DateTime LastReset(HomeworkTask task, Game game, DateTime now)
        {
            var t = game.GetResetTimeOfDay();
            switch (task.Kind)
            {
                case ResetKind.Daily:
                    return LastDaily(now, t);
                case ResetKind.Weekly:
                    return LastOnWeekdays(now, t, new[] { game.WeeklyResetDay });
                case ResetKind.Weekdays:
                    return task.ResetDays.Count == 0
                        ? LastOnWeekdays(now, t, new[] { game.WeeklyResetDay })
                        : LastOnWeekdays(now, t, task.ResetDays);
                case ResetKind.Monthly:
                    return LastMonthly(now, t, task.MonthDay);
                case ResetKind.Cycle:
                    return CycleLastReset(task, game, now);
                default:
                    return LastDaily(now, t);
            }
        }

        /// <summary>다음 초기화 시점</summary>
        public static DateTime NextReset(HomeworkTask task, Game game, DateTime now)
        {
            var t = game.GetResetTimeOfDay();
            switch (task.Kind)
            {
                case ResetKind.Daily:
                    return LastDaily(now, t).AddDays(1);
                case ResetKind.Weekly:
                    return NextOnWeekdays(now, t, new[] { game.WeeklyResetDay });
                case ResetKind.Weekdays:
                    return task.ResetDays.Count == 0
                        ? NextOnWeekdays(now, t, new[] { game.WeeklyResetDay })
                        : NextOnWeekdays(now, t, task.ResetDays);
                case ResetKind.Monthly:
                    return NextMonthly(now, t, task.MonthDay);
                case ResetKind.Cycle:
                    return CycleCurrentEnd(task, game, now).AddDays(1) + t;
                default:
                    return LastDaily(now, t).AddDays(1);
            }
        }

        public static DateTime NextDailyReset(Game game, DateTime now) =>
            LastDaily(now, game.GetResetTimeOfDay()).AddDays(1);

        public static bool IsDone(HomeworkTask task, Game game, DateTime? completedAt, DateTime now)
        {
            if (completedAt == null) return false;
            return completedAt.Value >= LastReset(task, game, now);
        }

        /// <summary>
        /// 초기화 전날(게임 기준 날짜)인지 — 다음 초기화까지 24시간 이하.
        /// 예: 월요일 04:00 초기화 → 일요일 04:00부터. 매일 초기화되는 숙제는 해당 없음.
        /// </summary>
        public static bool IsDueSoon(HomeworkTask task, Game game, DateTime now)
        {
            if (task.Kind == ResetKind.Daily) return false;
            // 특정 요일 숙제(예: 심연 화·금)는 그날만 보이므로, 보이는 날 = 마지막 날 → 당일 경고
            if (task.Kind == ResetKind.Weekdays) return IsVisibleToday(task, game, now);
            // 주기 컨텐츠는 마감 1주일 전(D-7)부터
            if (task.Kind == ResetKind.Cycle) return CycleDaysLeft(task, game, now) <= CycleWarnDays;
            return DueAt(task, game, now) - now <= TimeSpan.FromDays(1);
        }

        /// <summary>
        /// 이 숙제를 언제까지 해야 하는지.
        /// 매주·매월 = 다음 초기화, 특정 요일 = 오늘(게임 기준 날짜)이 끝나는 시각 (그 뒤엔 위젯에서 사라짐)
        /// </summary>
        public static DateTime DueAt(HomeworkTask task, Game game, DateTime now)
        {
            if (task.Kind == ResetKind.Weekdays)
            {
                var t = game.GetResetTimeOfDay();
                return GameDate(now, t).AddDays(1) + t;
            }
            return NextReset(task, game, now);
        }

        /// <summary>"내일 04:00까지" / "오늘 06:00까지"</summary>
        public static string DueLabel(DateTime dueAt, DateTime now) =>
            (dueAt.Date == now.Date ? "오늘 " : "내일 ") + dueAt.ToString("HH:mm") + "까지";

        /// <summary>마감 안내 문구: 주기 컨텐츠는 "D-5 · 12/10(수)까지", 나머지는 "내일 04:00까지"</summary>
        public static string DueText(HomeworkTask task, Game game, DateTime now)
        {
            if (task.Kind == ResetKind.Cycle)
            {
                var end = CycleCurrentEnd(task, game, now);
                return $"{ProgressBuilder.FormatDDay(CycleDaysLeft(task, game, now))} · {end:M'/'d}({DayName(end.DayOfWeek)})까지";
            }
            return DueLabel(DueAt(task, game, now), now);
        }

        // ---------- 주기 컨텐츠 (N주마다) ----------

        /// <summary>주기 컨텐츠 경고 시작: 마감 7일 전 (D-7)부터</summary>
        public const int CycleWarnDays = 7;

        public static int CycleWeeksOf(HomeworkTask task) => Math.Clamp(task.CycleWeeks, 1, 26);

        /// <summary>첫 회차 마지막 날 (설정 안 했으면 오늘)</summary>
        public static DateTime CycleEnd0(HomeworkTask task) => (task.CycleFirstEnd ?? DateTime.Today).Date;

        /// <summary>
        /// 두 번째 회차 마지막 날: 첫 마감 + N주 (마감 요일이 다르면 그 직전 7일 안의 그 요일).
        /// 그 뒤로는 계속 N주씩.
        /// </summary>
        public static DateTime CycleEnd1(HomeworkTask task)
        {
            var target = CycleEnd0(task).AddDays(7 * CycleWeeksOf(task));
            for (int i = 0; i < 7; i++)
            {
                var d = target.AddDays(-i);
                if (d.DayOfWeek == task.CycleEndDay) return d;
            }
            return target;
        }

        /// <summary>gameDate 이후(그날 포함) 첫 마감일</summary>
        public static DateTime CycleEndOnOrAfter(HomeworkTask task, DateTime gameDate)
        {
            var e0 = CycleEnd0(task);
            gameDate = gameDate.Date;
            if (gameDate <= e0) return e0;
            var e1 = CycleEnd1(task);
            if (gameDate <= e1) return e1;
            int period = 7 * CycleWeeksOf(task);
            int k = ((gameDate - e1).Days + period - 1) / period;
            return e1.AddDays(period * k);
        }

        /// <summary>이번 회차 마지막 날 (게임 기준 날짜)</summary>
        public static DateTime CycleCurrentEnd(HomeworkTask task, Game game, DateTime now) =>
            CycleEndOnOrAfter(task, GameDate(now, game.GetResetTimeOfDay()));

        /// <summary>이번 회차 시작 시점 (첫 회차는 등록/수정한 날 = CycleStart)</summary>
        private static DateTime CycleLastReset(HomeworkTask task, Game game, DateTime now)
        {
            var t = game.GetResetTimeOfDay();
            var cur = CycleCurrentEnd(task, game, now);
            var e0 = CycleEnd0(task);
            if (cur <= e0) return task.CycleStart ?? DateTime.MinValue;
            var prev = cur == CycleEnd1(task) ? e0 : cur.AddDays(-7 * CycleWeeksOf(task));
            return prev.AddDays(1) + t;
        }

        /// <summary>마감까지 남은 일수 (0 = 오늘이 마지막 날)</summary>
        public static int CycleDaysLeft(HomeworkTask task, Game game, DateTime now) =>
            (CycleCurrentEnd(task, game, now) - GameDate(now, game.GetResetTimeOfDay())).Days;

        /// <summary>
        /// 주기 설정 저장. 첫 회차 시작 = 지금 회차의 시작(수정이면 이미 한 체크 유지) 또는 오늘(게임 기준) 초기화 시각.
        /// </summary>
        public static void ApplyCycle(HomeworkTask task, Game game, DateTime firstEnd, int weeks, DayOfWeek endDay, DateTime now, bool isNew)
        {
            var t = game.GetResetTimeOfDay();
            DateTime start = GameDate(now, t) + t;   // 오늘(게임 기준 날짜)의 시작
            if (!isNew && task.Kind == ResetKind.Cycle)
            {
                var prev = CycleLastReset(task, game, now);
                if (prev > DateTime.MinValue && prev <= now) start = prev;
            }
            task.Kind = ResetKind.Cycle;
            task.CycleFirstEnd = firstEnd.Date;
            task.CycleWeeks = Math.Clamp(weeks, 1, 26);
            task.CycleEndDay = endDay;
            task.CycleStart = start;
        }

        /// <summary>앞으로의 마감일 count개 (설정 창 미리보기용)</summary>
        public static List<DateTime> CycleUpcomingEnds(HomeworkTask task, DateTime fromGameDate, int count)
        {
            var list = new List<DateTime>();
            var d = CycleEndOnOrAfter(task, fromGameDate);
            while (list.Count < count)
            {
                list.Add(d);
                d = CycleEndOnOrAfter(task, d.AddDays(1));
            }
            return list;
        }

        /// <summary>표시 안 할 요일(게임 기준 날짜)에 해당하면 false</summary>
        /// <remarks>특정 요일 숙제(예: 붕3 심연 화·금)는 그 요일(게임 기준 날짜)에만 표시</remarks>
        public static bool IsVisibleToday(HomeworkTask task, Game game, DateTime now)
        {
            var gd = GameDate(now, game.GetResetTimeOfDay());
            if (task.HiddenDays.Contains(gd.DayOfWeek)) return false;
            if (task.Kind == ResetKind.Weekdays) return WeekdayDays(task, game).Contains(gd.DayOfWeek);
            return true;
        }

        /// <summary>특정 요일 숙제를 하는 요일들 (비어 있으면 게임의 주간 초기화 요일)</summary>
        public static List<DayOfWeek> WeekdayDays(HomeworkTask task, Game game) =>
            task.ResetDays.Count == 0 ? new List<DayOfWeek> { game.WeeklyResetDay } : task.ResetDays;

        // ---------- 내부 계산 ----------

        private static DateTime LastDaily(DateTime now, TimeSpan t)
        {
            var candidate = now.Date + t;
            return candidate <= now ? candidate : candidate.AddDays(-1);
        }

        private static DateTime LastOnWeekdays(DateTime now, TimeSpan t, IEnumerable<DayOfWeek> days)
        {
            var set = new HashSet<DayOfWeek>(days);
            for (int i = 0; i <= 7; i++)
            {
                var d = now.Date.AddDays(-i);
                var candidate = d + t;
                if (set.Contains(d.DayOfWeek) && candidate <= now) return candidate;
            }
            // 도달하지 않음 (set이 비어있는 경우 대비)
            return LastDaily(now, t);
        }

        private static DateTime NextOnWeekdays(DateTime now, TimeSpan t, IEnumerable<DayOfWeek> days)
        {
            var set = new HashSet<DayOfWeek>(days);
            for (int i = 0; i <= 7; i++)
            {
                var d = now.Date.AddDays(i);
                var candidate = d + t;
                if (set.Contains(d.DayOfWeek) && candidate > now) return candidate;
            }
            return LastDaily(now, t).AddDays(1);
        }

        private static DateTime MonthlyCandidate(int year, int month, int day, TimeSpan t)
        {
            int dim = DateTime.DaysInMonth(year, month);
            int d = Math.Clamp(day, 1, dim);
            return new DateTime(year, month, d) + t;
        }

        private static DateTime LastMonthly(DateTime now, TimeSpan t, int day)
        {
            var c = MonthlyCandidate(now.Year, now.Month, day, t);
            if (c <= now) return c;
            var prev = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            return MonthlyCandidate(prev.Year, prev.Month, day, t);
        }

        private static DateTime NextMonthly(DateTime now, TimeSpan t, int day)
        {
            var c = MonthlyCandidate(now.Year, now.Month, day, t);
            if (c > now) return c;
            var next = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            return MonthlyCandidate(next.Year, next.Month, day, t);
        }

        // ---------- 이벤트 ----------

        /// <summary>
        /// 남은 일수 (게임 기준 날짜). 0 = 오늘이 마지막 날, 음수 = 종료됨.
        /// 마감일 다음 날 "게임 초기화 시각"에 끝난다 (예: 로아 12/10 마감 → 12/11 06:00 종료)
        /// </summary>
        public static int EventDaysLeft(EventQuest ev, Game game, DateTime now) =>
            (ev.EndDate.Date - GameDate(now, game.GetResetTimeOfDay())).Days;

        /// <summary>
        /// 위젯에 표시할지.
        /// 기한 이벤트: 마감일(게임 기준 날짜)까지 (완료해도 흐리게 남음).
        /// 무기한 목표: 완료 전까지, 완료하면 그 게임의 다음 초기화 전까지만.
        /// 기간이 끝났는데 완료 못 한 이벤트는 false지만 삭제되지 않고 "놓친 이벤트"로 남는다 (IsMissed).
        /// </summary>
        public static bool EventVisible(EventQuest ev, Game game, DateTime now)
        {
            if (!ev.NoDeadline) return EventDaysLeft(ev, game, now) >= 0;
            if (!ev.Done) return true;
            if (ev.DoneAt == null) return false;
            var t = game.GetResetTimeOfDay();
            return GameDate(ev.DoneAt.Value, t) >= GameDate(now, t);
        }

        /// <summary>기간이 끝났는데 완료하지 못한 이벤트 — 사용자가 확인하고 지울 때까지 남겨 둔다</summary>
        public static bool IsMissed(EventQuest ev, Game game, DateTime now) =>
            !ev.NoDeadline && !ev.Done && EventDaysLeft(ev, game, now) < 0;

        /// <summary>자동 삭제 대상: 끝났는데 완료한 이벤트, 완료 후 초기화가 지난 무기한 목표</summary>
        public static bool ShouldAutoDelete(EventQuest ev, Game game, DateTime now) =>
            !EventVisible(ev, game, now) && !IsMissed(ev, game, now);

        /// <summary>이 숙제가 어느 위젯에 나오는지: 매일 → 일일, N주마다 → 주기 컨텐츠, 나머지(매주·특정 요일·매월) → 주간</summary>
        public static WidgetKind WidgetFor(ResetKind kind) => kind switch
        {
            ResetKind.Daily => WidgetKind.Daily,
            ResetKind.Cycle => WidgetKind.Cycle,
            _ => WidgetKind.Weekly
        };

        // ---------- 체크 키 ----------

        public static string Key(HomeworkTask task, Character? ch) =>
            ch == null ? task.Id.ToString() : task.Id + ":" + ch.Id;

        /// <summary>이 숙제가 적용되는 캐릭터 목록 (RPG 캐릭터별 숙제)</summary>
        public static List<Character> ApplicableCharacters(HomeworkTask task, Game game)
        {
            if (task.CharacterIds.Count == 0) return game.Characters.ToList();
            return game.Characters.Where(c => task.CharacterIds.Contains(c.Id)).ToList();
        }

        public static bool IsPerCharacter(HomeworkTask task, Game game) =>
            game.Type == GameType.Rpg && task.Scope == TaskScope.PerCharacter && game.Characters.Count > 0;

        public static string DescribeRule(HomeworkTask task, Game game)
        {
            string time = game.GetResetTimeOfDay().ToString(@"hh\:mm");
            switch (task.Kind)
            {
                case ResetKind.Daily: return $"매일 {time}";
                case ResetKind.Weekly: return $"매주 {DayName(game.WeeklyResetDay)} {time}";
                case ResetKind.Weekdays:
                    var days = task.ResetDays.Count == 0 ? new List<DayOfWeek> { game.WeeklyResetDay } : task.ResetDays;
                    return string.Join("·", OrderDays(days).Select(DayName)) + "에만 (" + time + " 기준)";
                case ResetKind.Monthly: return $"매월 {task.MonthDay}일 {time}";
                case ResetKind.Cycle:
                    return $"{CycleWeeksOf(task)}주마다 {DayName(task.CycleEndDay)}요일까지 (다음 날 {time} 초기화)";
                default: return "";
            }
        }

        public static IEnumerable<DayOfWeek> OrderDays(IEnumerable<DayOfWeek> days) =>
            days.Distinct().OrderBy(d => ((int)d + 6) % 7); // 월요일부터

        public static string DayName(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "월",
            DayOfWeek.Tuesday => "화",
            DayOfWeek.Wednesday => "수",
            DayOfWeek.Thursday => "목",
            DayOfWeek.Friday => "금",
            DayOfWeek.Saturday => "토",
            _ => "일"
        };

        /// <summary>월요일부터 일요일 순서</summary>
        public static readonly DayOfWeek[] WeekOrder =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
    }
}

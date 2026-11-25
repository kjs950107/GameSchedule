using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    /// <summary>위젯 한 칸 (공통 숙제면 Character = null)</summary>
    public class TaskCell
    {
        public HomeworkTask Task { get; init; } = null!;
        public Character? Character { get; init; }
        public string Key { get; init; } = "";
        public bool Done { get; init; }
        public string? Label { get; init; }
        /// <summary>초기화 전날인데 아직 안 함 → 주황 경고</summary>
        public bool DueSoon { get; init; }
        public DateTime NextReset { get; init; }
        /// <summary>언제까지 해야 하는지 (특정 요일 숙제는 그날 끝)</summary>
        public DateTime DueAt { get; init; }
        /// <summary>마감 안내 ("내일 04:00까지", 주기 컨텐츠는 "D-5 · 12/10(수)까지")</summary>
        public string DueText { get; init; } = "";
    }

    public class GameProgress
    {
        public Game Game { get; init; } = null!;
        /// <summary>오늘 표시되는 숙제 (표시 안 할 요일 제외)</summary>
        public List<HomeworkTask> VisibleTasks { get; init; } = new();
        public List<TaskCell> Cells { get; init; } = new();
        public List<EventQuest> Events { get; init; } = new();

        /// <summary>놓친 이벤트 (기간 끝, 미완료) — 진행도에는 안 들어가고 확인용으로만 표시</summary>
        public List<EventQuest> Missed { get; init; } = new();

        /// <summary>위젯에 보여줄 게 있는지</summary>
        public bool HasContent => Total > 0 || Missed.Count > 0;
        public DateTime NextDailyReset { get; init; }

        /// <summary>이 위젯에 보이는 숙제 중 가장 빨리 오는 초기화 (이벤트 위젯이면 null)</summary>
        public DateTime? NextReset { get; init; }

        public int Total => Cells.Count + Events.Count;
        public int DoneCount => Cells.Count(c => c.Done) + Events.Count(e => e.Done);
        public int Remaining => Total - DoneCount;
        public bool IsComplete => Total > 0 && Remaining == 0;

        /// <summary>초기화 전날인데 아직 안 한 칸들</summary>
        public List<TaskCell> DueSoonUndone => Cells.Where(c => c.DueSoon && !c.Done).ToList();
        public bool HasDueSoon => Cells.Any(c => c.DueSoon && !c.Done);

        public TaskCell? Cell(HomeworkTask task, Character? ch) =>
            Cells.FirstOrDefault(c => c.Task.Id == task.Id && c.Character?.Id == ch?.Id);

        /// <summary>해당 캐릭터의 숙제를 모두 끝냈는지</summary>
        public bool CharacterComplete(Character ch)
        {
            var mine = Cells.Where(c => c.Character?.Id == ch.Id).ToList();
            return mine.Count > 0 && mine.All(c => c.Done);
        }
    }

    public static class ProgressBuilder
    {
        /// <param name="widget">null이면 전체(숙제+이벤트), 아니면 해당 위젯에 들어갈 것만</param>
        public static GameProgress Build(Game game, AppData data, DateTime now, WidgetKind? widget = null)
        {
            // 숙제 없는 게임은 위젯에 아무것도 표시하지 않는다
            if (game.Type == GameType.PlayOnly)
                return new GameProgress { Game = game, NextDailyReset = ResetCalculator.NextDailyReset(game, now) };

            // 합친 위젯 = 전부 (null과 같음)
            if (widget == WidgetKind.Combined) widget = null;
            var visible = game.Tasks
                .Where(t => ResetCalculator.IsVisibleToday(t, game, now))
                .Where(t => widget == null || ResetCalculator.WidgetFor(t.Kind) == widget)
                .ToList();
            var cells = new List<TaskCell>();
            foreach (var task in visible)
            {
                if (ResetCalculator.IsPerCharacter(task, game))
                {
                    foreach (var ch in ResetCalculator.ApplicableCharacters(task, game))
                        cells.Add(MakeCell(task, ch, game, data, now));
                }
                else
                {
                    cells.Add(MakeCell(task, null, game, data, now));
                }
            }

            // 기한 있는 이벤트는 모바일 게임만, 무기한 목표는 모든 게임
            var events = widget == null || widget == WidgetKind.Events
                ? data.Events
                    .Where(e => e.GameId == game.Id && (e.NoDeadline || game.Type == GameType.Mobile))
                    .Where(e => ResetCalculator.EventVisible(e, game, now))
                    .OrderBy(e => e.NoDeadline)          // 기한 이벤트 먼저
                    .ThenBy(e => e.EndDate)
                    .ThenBy(e => e.CreatedAt)
                    .ToList()
                : new List<EventQuest>();

            var missed = widget == null || widget == WidgetKind.Events
                ? data.Events
                    .Where(e => e.GameId == game.Id && ResetCalculator.IsMissed(e, game, now))
                    .OrderBy(e => e.EndDate)
                    .ToList()
                : new List<EventQuest>();

            DateTime? next = visible.Count == 0
                ? null
                : visible.Min(t => ResetCalculator.NextReset(t, game, now));

            return new GameProgress
            {
                Game = game,
                VisibleTasks = visible,
                Cells = cells,
                Events = events,
                Missed = missed,
                NextDailyReset = ResetCalculator.NextDailyReset(game, now),
                NextReset = next
            };
        }

        private static TaskCell MakeCell(HomeworkTask task, Character? ch, Game game, AppData data, DateTime now)
        {
            var key = ResetCalculator.Key(task, ch);
            DateTime? at = data.Completions.TryGetValue(key, out var v) ? v : null;
            string? label = null;
            if (ch != null && task.CharacterLabels.TryGetValue(ch.Id, out var l) && !string.IsNullOrWhiteSpace(l))
                label = l.Trim();
            return new TaskCell
            {
                Task = task,
                Character = ch,
                Key = key,
                Done = ResetCalculator.IsDone(task, game, at, now),
                Label = label,
                DueSoon = ResetCalculator.IsDueSoon(task, game, now),
                NextReset = ResetCalculator.NextReset(task, game, now),
                DueAt = ResetCalculator.DueAt(task, game, now),
                DueText = ResetCalculator.DueText(task, game, now)
            };
        }

        /// <summary>"3시간 12분", "45분"</summary>
        public static string FormatRemaining(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalDays >= 1) return span.Hours > 0 ? $"{span.Days}일 {span.Hours}시간" : $"{span.Days}일";
            int h = (int)span.TotalHours;
            int m = span.Minutes;
            if (h > 0) return $"{h}시간 {m}분";
            return $"{Math.Max(m, 1)}분";
        }

        public static string FormatDDay(int daysLeft) => daysLeft == 0 ? "D-DAY" : $"D-{daysLeft}";
    }
}

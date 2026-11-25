using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHomework.Core
{
    /// <summary>
    /// 위젯 표시 순서: 끝낸 것은 아래로, 남은 것은 위로.
    /// 같은 상태끼리는 설정에 등록한 순서를 그대로 유지한다 (안정 정렬).
    /// sort = false면 등록 순서 그대로.
    /// </summary>
    public static class WidgetOrdering
    {
        public static int KindOrder(ResetKind k) => k switch
        {
            ResetKind.Daily => 0,
            ResetKind.Weekdays => 1,
            ResetKind.Weekly => 2,
            ResetKind.Monthly => 3,
            _ => 4
        };

        /// <summary>게임 카드: 다 끝낸 게임(놓친 이벤트 없음)은 아래로</summary>
        public static List<GameProgress> Games(IEnumerable<GameProgress> games, bool sort) =>
            sort ? games.OrderBy(p => IsCardDone(p) ? 1 : 0).ToList() : games.ToList();

        public static bool IsCardDone(GameProgress p) => p.IsComplete && p.Missed.Count == 0;

        /// <summary>공통 숙제(체크박스 목록): 종류별 줄로 묶고, 줄 안에서 남은 것 먼저. 다 끝낸 줄은 아래로</summary>
        public static List<List<TaskCell>> CommonRows(IEnumerable<TaskCell> cells, bool sort)
        {
            var rows = cells.GroupBy(c => KindOrder(c.Task.Kind))
                            .OrderBy(g => g.Key)
                            .Select(g => sort ? g.OrderBy(c => c.Done ? 1 : 0).ToList() : g.ToList())
                            .ToList();
            return sort ? rows.OrderBy(r => r.All(c => c.Done) ? 1 : 0).ToList() : rows;
        }

        /// <summary>RPG 표의 한 줄(숙제)이 모든 캐릭터 완료인지</summary>
        public static bool RowDone(GameProgress p, HomeworkTask task)
        {
            var cells = p.Cells.Where(c => c.Task.Id == task.Id && c.Character != null).ToList();
            return cells.Count > 0 && cells.All(c => c.Done);
        }

        /// <summary>RPG 표: 종류별 묶음, 묶음 안에서 남은 줄 먼저. 다 끝낸 묶음은 아래로</summary>
        public static List<List<HomeworkTask>> GridGroups(GameProgress p, IEnumerable<HomeworkTask> perCharTasks, bool sort)
        {
            var groups = perCharTasks.GroupBy(t => KindOrder(t.Kind))
                                     .OrderBy(g => g.Key)
                                     .Select(g => sort ? g.OrderBy(t => RowDone(p, t) ? 1 : 0).ToList() : g.ToList())
                                     .ToList();
            return sort ? groups.OrderBy(g => g.All(t => RowDone(p, t)) ? 1 : 0).ToList() : groups;
        }

        /// <summary>이벤트/목표: 완료한 것은 아래로 (그 안에서는 마감 순서 유지)</summary>
        public static List<EventQuest> Events(IEnumerable<EventQuest> events, bool sort) =>
            sort ? events.OrderBy(e => e.Done ? 1 : 0).ToList() : events.ToList();
    }
}

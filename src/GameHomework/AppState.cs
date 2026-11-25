using System;
using GameHomework.Core;

namespace GameHomework
{
    /// <summary>앱 전체가 공유하는 데이터. 변경되면 Save() → Changed 이벤트로 위젯이 다시 그려진다.</summary>
    public static class AppState
    {
        public static DataStore Store { get; } = new DataStore();
        public static AppData Data { get; private set; } = new AppData();

        public static event Action? Changed;

        public static void Load()
        {
            Data = Store.Load();
        }

        public static void Save()
        {
            try { Store.Save(Data); }
            catch (Exception ex) { App.LogError("저장 실패", ex); }
            Changed?.Invoke();
        }

        /// <summary>위젯 위치처럼 다시 그릴 필요 없는 저장</summary>
        public static void SaveSilently()
        {
            try { Store.Save(Data); }
            catch (Exception ex) { App.LogError("저장 실패", ex); }
        }

        /// <summary>백업 파일 불러오기</summary>
        public static void ReplaceFromJson(string json)
        {
            var d = DataStore.Deserialize(json) ?? throw new InvalidOperationException("올바른 백업 파일이 아닙니다.");
            Store.Save(d);
            Data = Store.Load(); // 정리(Normalize) 포함
            Changed?.Invoke();
        }

        /// <summary>통계용 기록 (logs 폴더)</summary>
        public static StatsLog Log { get; } = new StatsLog(Store.FolderPath);

        /// <summary>기록 실패는 앱 동작에 영향 주지 않음</summary>
        public static void LogSafe(LogRecord record)
        {
            try { Log.Append(record); }
            catch (Exception ex) { App.LogError("기록 실패", ex); }
        }

        /// <summary>숙제 체크/해제 (+ 통계 기록)</summary>
        public static void SetDone(Game game, HomeworkTask task, Character? ch, bool done)
        {
            var now = DateTime.Now;
            var key = ResetCalculator.Key(task, ch);
            if (done) Data.Completions[key] = now;
            else Data.Completions.Remove(key);
            LogSafe(new LogRecord
            {
                Type = done ? LogType.Check : LogType.Uncheck,
                T = now,
                GameId = game.Id,
                Game = game.Name,
                TaskId = task.Id,
                Task = task.Name,
                Kind = task.Kind,
                CharId = ch?.Id,
                Char = ch?.Name
            });
            Save();
        }

        /// <summary>이벤트/목표 완료 체크 (+ 기한 이벤트면 마감까지 남은 일수 기록)</summary>
        public static void SetEventDone(EventQuest ev, bool done)
        {
            var now = DateTime.Now;
            ev.SetDone(done, now);
            var game = Data.Games.Find(g => g.Id == ev.GameId);
            if (!ev.NoDeadline && game != null)
            {
                LogSafe(new LogRecord
                {
                    Type = done ? LogType.EventDone : LogType.EventUndone,
                    T = now,
                    GameId = game.Id,
                    Game = game.Name,
                    EventId = ev.Id,
                    EventName = ev.Name,
                    EndDate = ev.EndDate,
                    DaysLeft = done ? ResetCalculator.EventDaysLeft(ev, game, now) : null
                });
            }
            Save();
        }

        /// <summary>이벤트 삭제. 놓친 이벤트면 "놓침"으로 기록하고 지운다</summary>
        public static void DeleteEvent(EventQuest ev)
        {
            var game = Data.Games.Find(g => g.Id == ev.GameId);
            if (game != null && ResetCalculator.IsMissed(ev, game, DateTime.Now))
            {
                LogSafe(new LogRecord
                {
                    Type = LogType.EventMissed,
                    T = DateTime.Now,
                    GameId = game.Id,
                    Game = game.Name,
                    EventId = ev.Id,
                    EventName = ev.Name,
                    EndDate = ev.EndDate
                });
            }
            Data.Events.Remove(ev);
            Save();
        }
    }
}

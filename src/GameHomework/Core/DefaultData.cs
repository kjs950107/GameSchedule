using System;
using System.Collections.Generic;

namespace GameHomework.Core
{
    /// <summary>처음 실행할 때 들어가는 기본 데이터</summary>
    public static class DefaultData
    {
        public static AppData Create()
        {
            var data = new AppData { WelcomeShown = false }; // 새로 설치 → 처음 실행 안내 한 번

            // ---- 모바일 게임 ----
            // 초기화 시각: 조사로 확인된 것은 그 시각(한국 시간), 확인 못 한 것은 04:00
            data.Games.Add(Mobile("붕괴3rd", "04:00", DayOfWeek.Monday, extra: g =>
            {
                g.Tasks.Add(new HomeworkTask
                {
                    Name = "심연",
                    Kind = ResetKind.Weekdays,
                    ResetDays = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Friday }
                });
            }));
            data.Games.Add(Mobile("니케", "04:00", DayOfWeek.Tuesday));
            data.Games.Add(Mobile("붕괴: 스타레일", "05:00", DayOfWeek.Monday));
            data.Games.Add(Mobile("젠레스 존 제로", "05:00", DayOfWeek.Monday));
            data.Games.Add(Mobile("명조", "05:00", DayOfWeek.Monday));
            data.Games.Add(Mobile("명일방주", "04:00", DayOfWeek.Monday));
            data.Games.Add(Mobile("명일방주: 엔드필드", "05:00", DayOfWeek.Monday));
            data.Games.Add(Mobile("카오스 제로 나이트메어", "03:00", DayOfWeek.Monday));

            // ---- 마비노기 영웅전 ---- (공통 숙제 없음, 모두 캐릭터별)
            var heroes = new Game
            {
                Name = "마비노기 영웅전",
                Type = GameType.Rpg,
                ResetTime = "04:00",
                WeeklyResetDay = DayOfWeek.Thursday
            };
            // 캐릭터는 예시 — 설정 → 캐릭터에서 이름 바꾸기
            heroes.Characters.Add(new Character { Name = "캐릭터1" });
            heroes.Characters.Add(new Character { Name = "캐릭터2" });
            foreach (var n in new[] { "코부털기", "붉던", "기사단" })
                heroes.Tasks.Add(new HomeworkTask { Name = n, Kind = ResetKind.Daily, Scope = TaskScope.PerCharacter });
            foreach (var n in new[] { "시공", "성채", "스던", "결사대" })
                heroes.Tasks.Add(new HomeworkTask { Name = n, Kind = ResetKind.Weekly, Scope = TaskScope.PerCharacter });
            heroes.Tasks.Add(new HomeworkTask { Name = "미탑", Kind = ResetKind.Monthly, MonthDay = 1, Scope = TaskScope.PerCharacter });
            data.Games.Add(heroes);

            // ---- 로스트아크 ----
            var lostark = new Game
            {
                Name = "로스트아크",
                Type = GameType.Rpg,
                ResetTime = "06:00",
                WeeklyResetDay = DayOfWeek.Wednesday
            };
            // 캐릭터는 예시 — 설정 → 캐릭터에서 이름 바꾸기
            lostark.Characters.Add(new Character { Name = "캐릭터1" });
            lostark.Characters.Add(new Character { Name = "캐릭터2" });
            lostark.Characters.Add(new Character { Name = "캐릭터3" });
            lostark.Tasks.Add(new HomeworkTask
            {
                Name = "카게/필보",
                Kind = ResetKind.Daily,
                Scope = TaskScope.Common,
                HiddenDays = new List<DayOfWeek> { DayOfWeek.Wednesday }
            });
            lostark.Tasks.Add(new HomeworkTask { Name = "카던", Kind = ResetKind.Daily, Scope = TaskScope.PerCharacter });
            lostark.Tasks.Add(new HomeworkTask { Name = "가토", Kind = ResetKind.Daily, Scope = TaskScope.PerCharacter });
            for (int i = 1; i <= 3; i++)
                lostark.Tasks.Add(new HomeworkTask { Name = $"레이드 {i}", Kind = ResetKind.Weekly, Scope = TaskScope.PerCharacter });
            lostark.Tasks.Add(new HomeworkTask { Name = "모래시계", Kind = ResetKind.Weekly, Scope = TaskScope.PerCharacter });
            data.Games.Add(lostark);

            // ---- 숙제 없는 게임 (플레이 시간만 기록) ----
            // 실행 파일은 설정 → 게임 정보 → "실행 중인 프로그램에서 고르기"로 등록
            data.Games.Add(new Game { Name = "하스스톤", Type = GameType.PlayOnly });
            data.Games.Add(new Game { Name = "리그 오브 레전드", Type = GameType.PlayOnly });

            return data;
        }

        private static Game Mobile(string name, string reset, DayOfWeek weekly, Action<Game>? extra = null)
        {
            var g = new Game { Name = name, Type = GameType.Mobile, ResetTime = reset, WeeklyResetDay = weekly };
            g.Tasks.Add(new HomeworkTask { Name = "일퀘", Kind = ResetKind.Daily, Scope = TaskScope.Common });
            g.Tasks.Add(new HomeworkTask { Name = "주간퀘", Kind = ResetKind.Weekly, Scope = TaskScope.Common });
            extra?.Invoke(g);
            return g;
        }
    }
}

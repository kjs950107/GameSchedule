using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameHomework.Core;

namespace CoreTests
{
    internal static class Program
    {
        private static int _pass, _fail;

        private static void Check(string name, bool ok, string? detail = null)
        {
            if (ok) { _pass++; Console.WriteLine("  PASS  " + name); }
            else { _fail++; Console.WriteLine("  FAIL  " + name + (detail != null ? "  → " + detail : "")); }
        }

        private static void Eq<T>(string name, T expected, T actual) =>
            Check(name, Equals(expected, actual), $"expected {expected}, actual {actual}");

        private static DateTime D(int y, int mo, int d, int h = 0, int mi = 0) => new(y, mo, d, h, mi, 0);

        private static int Main()
        {
            // 날짜 구분 기호를 '-'로 바꾼 PC(윈도우 지역 설정)에서도 "12/7"로 나오는지 확인
            var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            culture.DateTimeFormat.DateSeparator = "-";
            System.Globalization.CultureInfo.CurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;

            // 2026-11-23 = 월요일
            Console.WriteLine("[매일 초기화]");
            var lostark = new Game { Name = "로아", Type = GameType.Rpg, ResetTime = "06:00", WeeklyResetDay = DayOfWeek.Wednesday };
            var daily = new HomeworkTask { Kind = ResetKind.Daily };
            Eq("06:00 이후 → 오늘 06:00", D(2026, 11, 23, 6), ResetCalculator.LastReset(daily, lostark, D(2026, 11, 23, 11)));
            Eq("06:00 이전(새벽 3시) → 어제 06:00", D(2026, 11, 22, 6), ResetCalculator.LastReset(daily, lostark, D(2026, 11, 23, 3)));
            Eq("정확히 06:00 → 오늘 06:00", D(2026, 11, 23, 6), ResetCalculator.LastReset(daily, lostark, D(2026, 11, 23, 6)));
            Check("어제 23시에 체크 → 오늘 11시엔 미완료",
                !ResetCalculator.IsDone(daily, lostark, D(2026, 11, 22, 23), D(2026, 11, 23, 11)));
            Check("새벽 2시에 체크 → 새벽 5시엔 완료 유지",
                ResetCalculator.IsDone(daily, lostark, D(2026, 11, 23, 2), D(2026, 11, 23, 5)));
            Check("며칠 꺼뒀다 켜도 정확 (3일 전 체크 → 미완료)",
                !ResetCalculator.IsDone(daily, lostark, D(2026, 11, 20, 12), D(2026, 11, 23, 12)));

            Console.WriteLine("[주간 초기화 - 로아 수요일 06:00]");
            var weekly = new HomeworkTask { Kind = ResetKind.Weekly };
            Eq("월요일 → 지난 수요일 06:00", D(2026, 11, 18, 6), ResetCalculator.LastReset(weekly, lostark, D(2026, 11, 23, 12)));
            Eq("수요일 05:00 → 지난주 수요일", D(2026, 11, 18, 6), ResetCalculator.LastReset(weekly, lostark, D(2026, 11, 25, 5)));
            Eq("수요일 07:00 → 오늘 수요일", D(2026, 11, 25, 6), ResetCalculator.LastReset(weekly, lostark, D(2026, 11, 25, 7)));
            Eq("다음 주간 초기화", D(2026, 11, 25, 6), ResetCalculator.NextReset(weekly, lostark, D(2026, 11, 23, 12)));
            Check("화요일 밤 체크 → 수요일 05:59 완료, 06:00 미완료",
                ResetCalculator.IsDone(weekly, lostark, D(2026, 11, 24, 22), D(2026, 11, 25, 5, 59)) &&
                !ResetCalculator.IsDone(weekly, lostark, D(2026, 11, 24, 22), D(2026, 11, 25, 6, 0)));

            Console.WriteLine("[주간 초기화 - 니케 화요일, 마영전 목요일]");
            var nikke = new Game { ResetTime = "04:00", WeeklyResetDay = DayOfWeek.Tuesday };
            Eq("니케 월요일 → 지난 화요일", D(2026, 11, 17, 4), ResetCalculator.LastReset(weekly, nikke, D(2026, 11, 23, 12)));
            var heroes = new Game { ResetTime = "04:00", WeeklyResetDay = DayOfWeek.Thursday };
            Eq("마영전 월요일 → 지난 목요일", D(2026, 11, 19, 4), ResetCalculator.LastReset(weekly, heroes, D(2026, 11, 23, 12)));

            Console.WriteLine("[특정 요일 - 붕괴3 심연 화·금]");
            var hi3 = new Game { ResetTime = "04:00", WeeklyResetDay = DayOfWeek.Monday };
            var abyss = new HomeworkTask { Kind = ResetKind.Weekdays, ResetDays = new() { DayOfWeek.Tuesday, DayOfWeek.Friday } };
            Eq("월요일 → 지난 금요일", D(2026, 11, 20, 4), ResetCalculator.LastReset(abyss, hi3, D(2026, 11, 23, 12)));
            Eq("화요일 12시 → 오늘 화요일", D(2026, 11, 24, 4), ResetCalculator.LastReset(abyss, hi3, D(2026, 11, 24, 12)));
            Eq("목요일 → 화요일", D(2026, 11, 24, 4), ResetCalculator.LastReset(abyss, hi3, D(2026, 11, 26, 12)));
            Eq("다음 초기화 (월요일 기준) → 화요일", D(2026, 11, 24, 4), ResetCalculator.NextReset(abyss, hi3, D(2026, 11, 23, 12)));
            Eq("다음 초기화 (수요일 기준) → 금요일", D(2026, 11, 27, 4), ResetCalculator.NextReset(abyss, hi3, D(2026, 11, 25, 12)));
            var mwf = new HomeworkTask { Kind = ResetKind.Weekdays, ResetDays = new() { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday } };
            Eq("월수금 - 일요일 → 금요일", D(2026, 11, 20, 4), ResetCalculator.LastReset(mwf, hi3, D(2026, 11, 22, 12)));

            Console.WriteLine("[매월 N일 - 마영전 미탑 1일]");
            var monthly = new HomeworkTask { Kind = ResetKind.Monthly, MonthDay = 1 };
            Eq("11/23 → 11/1 04:00", D(2026, 11, 1, 4), ResetCalculator.LastReset(monthly, heroes, D(2026, 11, 23, 12)));
            Eq("12/1 03:00 → 11/1 (아직 초기화 전)", D(2026, 11, 1, 4), ResetCalculator.LastReset(monthly, heroes, D(2026, 12, 1, 3)));
            Eq("12/1 05:00 → 12/1", D(2026, 12, 1, 4), ResetCalculator.LastReset(monthly, heroes, D(2026, 12, 1, 5)));
            Eq("다음 → 12/1", D(2026, 12, 1, 4), ResetCalculator.NextReset(monthly, heroes, D(2026, 11, 23, 12)));
            Eq("1월 → 전년도 12월", D(2026, 12, 1, 4), ResetCalculator.LastReset(monthly, heroes, D(2027, 1, 1, 1)));
            var m31 = new HomeworkTask { Kind = ResetKind.Monthly, MonthDay = 31 };
            Eq("31일 규칙, 2월엔 말일(28일)", D(2027, 2, 28, 4), ResetCalculator.LastReset(m31, heroes, D(2027, 3, 10, 12)));

            Console.WriteLine("[표시 요일 - 카게/필보 수요일 숨김]");
            var kage = new HomeworkTask { Kind = ResetKind.Daily, HiddenDays = new() { DayOfWeek.Wednesday } };
            Check("화요일 낮 표시", ResetCalculator.IsVisibleToday(kage, lostark, D(2026, 11, 24, 12)));
            Check("수요일 05:00(게임상 화요일) 표시", ResetCalculator.IsVisibleToday(kage, lostark, D(2026, 11, 25, 5)));
            Check("수요일 07:00 숨김", !ResetCalculator.IsVisibleToday(kage, lostark, D(2026, 11, 25, 7)));
            Check("목요일 05:00(게임상 수요일) 숨김", !ResetCalculator.IsVisibleToday(kage, lostark, D(2026, 11, 26, 5)));
            Check("목요일 07:00 표시", ResetCalculator.IsVisibleToday(kage, lostark, D(2026, 11, 26, 7)));

            Console.WriteLine("[이벤트]");
            var ev = new EventQuest { EndDate = D(2026, 11, 30) };
            var evGame = new Game { ResetTime = "05:00" };
            Eq("11/23 → D-7", 7, ResetCalculator.EventDaysLeft(ev, evGame, D(2026, 11, 23, 12)));
            Check("마지막 날 진행 중", ResetCalculator.EventVisible(ev, evGame, D(2026, 11, 30, 23)));
            Check("다음 날 새벽 4시(초기화 전)까지 진행 중", ResetCalculator.EventVisible(ev, evGame, D(2026, 12, 1, 4)));
            Check("다음 날 초기화(05:00) 후 종료", !ResetCalculator.EventVisible(ev, evGame, D(2026, 12, 1, 5)));

            Console.WriteLine("[기본 데이터 & 저장]");
            var data = DefaultData.Create();
            Eq("게임 수 (숙제 없는 게임 2개 포함)", 12, data.Games.Count);
            var la = data.Games.Single(g => g.Name == "로스트아크");
            Eq("로아 캐릭터 (예시 3명)", "캐릭터1,캐릭터2,캐릭터3", string.Join(",", la.Characters.Select(c => c.Name)));
            Eq("로아 공통 숙제", "카게/필보", string.Join(",", la.Tasks.Where(t => t.Scope == TaskScope.Common).Select(t => t.Name)));
            Eq("로아 주간 숙제", "레이드 1,레이드 2,레이드 3,모래시계",
                string.Join(",", la.Tasks.Where(t => t.Kind == ResetKind.Weekly).Select(t => t.Name)));
            var mh = data.Games.Single(g => g.Name == "마비노기 영웅전");
            Check("마영전 공통 숙제 없음", mh.Tasks.All(t => t.Scope == TaskScope.PerCharacter));
            Eq("마영전 주간 요일", DayOfWeek.Thursday, mh.WeeklyResetDay);
            Eq("니케 주간 요일", DayOfWeek.Tuesday, data.Games.Single(g => g.Name == "니케").WeeklyResetDay);
            Check("붕괴3 심연 화·금", data.Games.Single(g => g.Name == "붕괴3rd").Tasks.Any(t =>
                t.Name == "심연" && t.ResetDays.SequenceEqual(new[] { DayOfWeek.Tuesday, DayOfWeek.Friday })));

            // 캐릭터별 레이드 이름 + 저장/불러오기 왕복
            var raid1 = la.Tasks.First(t => t.Name == "레이드 1");
            raid1.CharacterLabels[la.Characters[0].Id] = "카제로스";
            data.Completions[ResetCalculator.Key(raid1, la.Characters[0])] = D(2026, 11, 23, 10);
            data.Events.Add(new EventQuest { GameId = data.Games[1].Id, Name = "테스트", EndDate = D(2026, 12, 10) });
            var dir = Path.Combine(Path.GetTempPath(), "gh-test-" + Guid.NewGuid().ToString("N"));
            var store = new DataStore(dir);
            store.Save(data);
            store.Save(data); // 두 번째 저장은 File.Replace 경로
            var loaded = store.Load();
            var la2 = loaded.Games.Single(g => g.Name == "로스트아크");
            Eq("레이드 이름 저장", "카제로스", la2.Tasks.First(t => t.Name == "레이드 1").CharacterLabels[la2.Characters[0].Id]);
            Eq("완료 기록 저장", 1, loaded.Completions.Count);
            Eq("이벤트 저장", 1, loaded.Events.Count);
            Check("한글이 그대로 저장됨", File.ReadAllText(store.FilePath).Contains("로스트아크"));

            Console.WriteLine("[끝난 이벤트/목표 자동 삭제]");
            var pg = DefaultData.Create();
            var pgNikke = pg.Games.Single(g => g.Name == "니케");      // 04:00 초기화
            var pgLa = pg.Games.Single(g => g.Name == "로스트아크");   // 06:00 초기화
            var evOpen = new EventQuest { GameId = pgNikke.Id, Name = "진행중", EndDate = D(2026, 11, 30) };
            var evOld = new EventQuest { GameId = pgNikke.Id, Name = "끝남", EndDate = D(2026, 11, 22) };
            var evDone = new EventQuest { GameId = pgNikke.Id, Name = "완료-기간남음", EndDate = D(2026, 11, 30) };
            evDone.SetDone(true, D(2026, 11, 20, 12));
            var goalOpen = new EventQuest { GameId = pgLa.Id, Name = "목표-진행", NoDeadline = true };
            var goalDone = new EventQuest { GameId = pgLa.Id, Name = "목표-완료", NoDeadline = true };
            goalDone.SetDone(true, D(2026, 11, 23, 10));
            var orphan = new EventQuest { GameId = Guid.NewGuid(), Name = "게임없음", NoDeadline = true };
            pg.Events.AddRange(new[] { evOpen, evOld, evDone, goalOpen, goalDone, orphan });

            Check("11/23 03:00 (니케 초기화 전) → 11/22 마감 이벤트는 아직 유지",
                DataStore.PurgeExpired(pg, D(2026, 11, 23, 3)) && pg.Events.Contains(evOld) && !pg.Events.Contains(orphan));
            Check("11/23 낮: 마감 지났지만 완료 못 한 이벤트는 삭제 안 함 (놓친 이벤트)",
                !DataStore.PurgeExpired(pg, D(2026, 11, 23, 12)) && pg.Events.Contains(evOld));
            Check("놓친 이벤트로 분류", ResetCalculator.IsMissed(evOld, pgNikke, D(2026, 11, 23, 12)));
            var pgp = ProgressBuilder.Build(pgNikke, pg, D(2026, 11, 23, 12), WidgetKind.Events);
            Eq("이벤트 위젯: 진행 중 2개 (놓친 건 진행도 제외)", 2, pgp.Total);
            Eq("이벤트 위젯: 놓친 이벤트 1개 표시", "끝남", string.Join(",", pgp.Missed.Select(e => e.Name)));
            Eq("일일 위젯에는 놓친 이벤트 없음", 0, ProgressBuilder.Build(pgNikke, pg, D(2026, 11, 23, 12), WidgetKind.Daily).Missed.Count);
            pg.Events.Remove(evOld); // 사용자가 확인 버튼으로 삭제
            DataStore.PurgeExpired(pg, D(2026, 11, 24, 5));
            Check("11/24 05:00 (로아 초기화 전) → 완료 목표 유지", pg.Events.Contains(goalDone));
            Check("11/24 06:00 (로아 초기화 후) → 완료 목표 삭제",
                DataStore.PurgeExpired(pg, D(2026, 11, 24, 6)) && !pg.Events.Contains(goalDone) && pg.Events.Contains(goalOpen));
            Check("삭제할 게 없으면 false", !DataStore.PurgeExpired(pg, D(2026, 11, 24, 7)));
            DataStore.PurgeExpired(pg, D(2026, 12, 1, 4));
            Check("12/1 04:00 (니케 초기화) → 완료한 이벤트는 삭제, 못 한 이벤트는 놓친 이벤트로 남음",
                !pg.Events.Contains(evDone) && pg.Events.Contains(evOpen) && pg.Events.Contains(goalOpen)
                && ResetCalculator.IsMissed(evOpen, pgNikke, D(2026, 12, 1, 4)));
            Directory.Delete(dir, true);

            Console.WriteLine("[위젯 진행도]");
            var pd = DefaultData.Create();
            var pla = pd.Games.Single(g => g.Name == "로스트아크");
            var wedNoon = D(2026, 11, 25, 12);
            var tueNoon = D(2026, 11, 24, 12);
            // 로아: 공통 1(카게필보) + 캐릭 3명 × (카던, 가토, 레이드×3, 모래시계 = 6) = 19
            Eq("로아 화요일 칸 수", 19, ProgressBuilder.Build(pla, pd, tueNoon).Total);
            Eq("로아 수요일 칸 수 (카게/필보 숨김)", 18, ProgressBuilder.Build(pla, pd, wedNoon).Total);
            // 마영전: 캐릭 2명 × 8 = 16
            Eq("마영전 칸 수", 16, ProgressBuilder.Build(pd.Games.Single(g => g.Name == "마비노기 영웅전"), pd, tueNoon).Total);

            // 특정 캐릭터만 적용
            var gunsl = pla.Characters[0];
            var raid3 = pla.Tasks.First(t => t.Name == "레이드 3");
            raid3.CharacterIds = new List<Guid> { gunsl.Id };
            Eq("레이드 3을 첫 캐릭터만 → 17칸", 17, ProgressBuilder.Build(pla, pd, tueNoon).Total);
            raid3.CharacterIds.Clear();

            // 모바일 + 이벤트
            var nk = pd.Games.Single(g => g.Name == "니케");
            pd.Events.Add(new EventQuest { GameId = nk.Id, EndDate = D(2026, 11, 30) });
            pd.Events.Add(new EventQuest { GameId = nk.Id, EndDate = D(2026, 11, 20) }); // 종료됨
            var np = ProgressBuilder.Build(nk, pd, D(2026, 11, 23, 12));
            Eq("니케 칸 수 (일퀘+주간퀘+진행중 이벤트1)", 3, np.Total);
            Check("니케 미완료", !np.IsComplete);
            pd.Completions[ResetCalculator.Key(nk.Tasks[0], null)] = D(2026, 11, 23, 10);
            pd.Completions[ResetCalculator.Key(nk.Tasks[1], null)] = D(2026, 11, 23, 10);
            pd.Events.First(e => e.EndDate == D(2026, 11, 30)).Done = true;
            np = ProgressBuilder.Build(nk, pd, D(2026, 11, 23, 12));
            Check("니케 전부 완료 → 접힘 대상", np.IsComplete);
            np = ProgressBuilder.Build(nk, pd, D(2026, 11, 24, 12));
            Eq("화요일 → 니케는 일퀘+주간퀘 둘 다 열림 (주간 초기화일)", 2, np.Remaining);
            var hsr = pd.Games.Single(g => g.Name == "붕괴: 스타레일");
            pd.Completions[ResetCalculator.Key(hsr.Tasks[0], null)] = D(2026, 11, 23, 10);
            pd.Completions[ResetCalculator.Key(hsr.Tasks[1], null)] = D(2026, 11, 23, 10);
            Eq("스타레일 화요일 → 일퀘만 열림 (주간은 월요일 초기화)", 1, ProgressBuilder.Build(hsr, pd, D(2026, 11, 24, 12)).Remaining);
            Eq("남은 시간 표시", "3시간 12분", ProgressBuilder.FormatRemaining(new TimeSpan(3, 12, 40)));
            Eq("D-DAY 표시", "D-DAY", ProgressBuilder.FormatDDay(0));
            Eq("며칠 남은 표시", "2일 5시간", ProgressBuilder.FormatRemaining(new TimeSpan(2, 5, 30, 0)));

            Console.WriteLine("[위젯 3종 분리]");
            var wd = DefaultData.Create();
            var wla = wd.Games.Single(g => g.Name == "로스트아크");
            var tue = D(2026, 11, 24, 12);
            // 일일: 카게/필보 1 + 3캐릭 × (카던, 가토) = 7
            Eq("로아 일일 위젯", 7, ProgressBuilder.Build(wla, wd, tue, WidgetKind.Daily).Total);
            // 주간: 3캐릭 × (레이드×3, 모래시계) = 12
            Eq("로아 주간 위젯", 12, ProgressBuilder.Build(wla, wd, tue, WidgetKind.Weekly).Total);
            Eq("로아 이벤트 위젯 (없음)", 0, ProgressBuilder.Build(wla, wd, tue, WidgetKind.Events).Total);
            var whi3 = wd.Games.Single(g => g.Name == "붕괴3rd");
            Eq("붕3 일일 위젯 = 일퀘", "일퀘", string.Join(",", ProgressBuilder.Build(whi3, wd, tue, WidgetKind.Daily).VisibleTasks.Select(t => t.Name)));
            Eq("붕3 주간 위젯 = 주간퀘+심연", "주간퀘,심연", string.Join(",", ProgressBuilder.Build(whi3, wd, tue, WidgetKind.Weekly).VisibleTasks.Select(t => t.Name)));
            var wmh = wd.Games.Single(g => g.Name == "마비노기 영웅전");
            Eq("마영전 주간 위젯 (주간4+미탑) × 2캐릭", 10, ProgressBuilder.Build(wmh, wd, tue, WidgetKind.Weekly).Total);
            Eq("로아 주간 위젯 다음 초기화 = 수요일 06:00", D(2026, 11, 25, 6), ProgressBuilder.Build(wla, wd, tue, WidgetKind.Weekly).NextReset);
            Check("일일 위젯에는 이벤트 안 나옴", ProgressBuilder.Build(whi3, wd, tue, WidgetKind.Daily).Events.Count == 0);

            Console.WriteLine("[무기한 목표]");
            var goal = new EventQuest { GameId = wla.Id, Name = "메인스토리", NoDeadline = true };
            wd.Events.Add(goal);
            Eq("RPG에도 무기한 목표 표시", 1, ProgressBuilder.Build(wla, wd, tue, WidgetKind.Events).Events.Count);
            var deadlineOnRpg = new EventQuest { GameId = wla.Id, EndDate = D(2026, 12, 31) };
            wd.Events.Add(deadlineOnRpg);
            Eq("RPG에는 기한 이벤트는 표시 안 함", 1, ProgressBuilder.Build(wla, wd, tue, WidgetKind.Events).Events.Count);
            Check("무기한 목표는 몇 년 뒤에도 표시", ResetCalculator.EventVisible(goal, wla, D(2030, 1, 1, 12)));
            goal.SetDone(true, D(2026, 11, 24, 12));
            Check("완료한 날은 흐리게 남음", ResetCalculator.EventVisible(goal, wla, D(2026, 11, 25, 5)));
            Check("다음 날(로아 06:00 이후) 사라짐", !ResetCalculator.EventVisible(goal, wla, D(2026, 11, 25, 7)));
            goal.SetDone(false, D(2026, 11, 26, 12));
            Check("완료 취소하면 다시 표시", ResetCalculator.EventVisible(goal, wla, D(2026, 11, 26, 12)));
            var nkGame = wd.Games.Single(g => g.Name == "니케");
            wd.Events.Add(new EventQuest { GameId = nkGame.Id, Name = "B", NoDeadline = true });
            wd.Events.Add(new EventQuest { GameId = nkGame.Id, Name = "A", EndDate = D(2026, 12, 5) });
            Eq("기한 이벤트가 무기한 목표보다 먼저", "A,B",
                string.Join(",", ProgressBuilder.Build(nkGame, wd, tue, WidgetKind.Events).Events.Select(e => e.Name)));

            // 저장/불러오기: 무기한 목표는 정리 대상 아님
            var dir2 = Path.Combine(Path.GetTempPath(), "gh-test-" + Guid.NewGuid().ToString("N"));
            var store2 = new DataStore(dir2);
            wd.Widget.Get(WidgetKind.Weekly).Left = 100;
            wd.Widget.Get(WidgetKind.Events).Visible = false;
            store2.Save(wd);
            var l2 = store2.Load();
            Check("무기한 목표 유지됨", l2.Events.Any(e => e.Name == "메인스토리" && e.NoDeadline));
            Eq("위젯별 위치 저장", 100.0, l2.Widget.Get(WidgetKind.Weekly).Left);
            Check("위젯별 표시 여부 저장", !l2.Widget.Get(WidgetKind.Events).Visible && l2.Widget.Get(WidgetKind.Daily).Visible);
            Directory.Delete(dir2, true);

            // 이전 버전 data.json (Widget에 Left/Top이 바로 있던 형식)도 읽혀야 함
            var old = DataStore.Deserialize("{\"Games\":[],\"Widget\":{\"Left\":10,\"Top\":20,\"Width\":380,\"Height\":720,\"Opacity\":0.8,\"LockPosition\":true}}");
            Check("이전 형식 호환 (투명도/잠금 유지)", old != null && old.Widget.Opacity == 0.8 && old.Widget.LockPosition);

            Console.WriteLine("[숙제 없는 게임]");
            var sd = DefaultData.Create();
            var hs = sd.Games.Single(g => g.Name == "하스스톤");
            Check("하스스톤은 숙제 없는 게임", hs.Type == GameType.PlayOnly);
            Check("위젯에 아무것도 안 나옴", !ProgressBuilder.Build(hs, sd, D(2026, 11, 23, 12), WidgetKind.Daily).HasContent
                && !ProgressBuilder.Build(hs, sd, D(2026, 11, 23, 12), WidgetKind.Events).HasContent);

            Console.WriteLine("[게임 실행 감지]");
            var la3 = sd.Games.Single(g => g.Name == "로스트아크");
            la3.ExeNames.Add("LOSTARK.exe");
            hs.ExeNames.Add("Hearthstone.exe");
            Eq("exe 이름 정리", "lostark", PlayTracker.NormalizeExe(" LostArk.EXE "));
            var tr = new PlayTracker(TimeSpan.FromSeconds(15));
            var t0 = D(2026, 11, 23, 20);
            // 20:00 로아만 켬
            tr.Sample(t0, sd.Games, new[] { "explorer", "LOSTARK" }, "LOSTARK");
            // 20:00~20:10 로아(맨 앞) + 하스스톤(옆에 켜둠, 20:00:15부터)
            var ended = new List<PlaySession>();
            for (int i = 1; i <= 40; i++)
            {
                var t = t0.AddSeconds(15 * i);
                bool hsFront = i > 30; // 마지막 10번(2분 30초)은 하스스톤이 맨 앞
                ended.AddRange(tr.Sample(t, sd.Games, new[] { "LOSTARK", "Hearthstone" }, hsFront ? "Hearthstone" : "LOSTARK"));
            }
            Eq("실행 중 세션 2개", 2, tr.OpenSessions.Count);
            var laS = tr.OpenSessions.Single(s => s.GameId == la3.Id);
            var hsS = tr.OpenSessions.Single(s => s.GameId == hs.Id);
            Eq("로아 켜둔 시간 600초", 600.0, laS.RunSec);
            Eq("로아 맨 앞 450초", 450.0, laS.FgSec);
            Eq("하스스톤 켜둔 시간 585초 (15초 늦게 켬)", 585.0, hsS.RunSec);
            Eq("하스스톤 맨 앞 150초", 150.0, hsS.FgSec);
            // 하스스톤 끔
            ended.AddRange(tr.Sample(t0.AddSeconds(15 * 41), sd.Games, new[] { "LOSTARK" }, "LOSTARK"));
            Check("하스스톤 끄면 세션 종료", ended.Count == 1 && ended[0].GameId == hs.Id && ended[0].PlayOnly);
            // 절전으로 1시간 샘플 없음 → 끊긴 것으로 처리
            var gapEnded = tr.Sample(t0.AddHours(1), sd.Games, new[] { "LOSTARK" }, "LOSTARK");
            Check("긴 공백 뒤에는 세션을 끊고 새로 시작 (공백 시간 안 셈)",
                gapEnded.Count == 1 && gapEnded[0].RunSec == 615.0 && tr.OpenSessions.Single().RunSec == 0);
            Check("exe 등록 안 한 게임은 무시", tr.OpenSessions.All(s => s.GameId == la3.Id));

            Console.WriteLine("[기록 파일]");
            var dir3 = Path.Combine(Path.GetTempPath(), "gh-test-" + Guid.NewGuid().ToString("N"));
            var log = new StatsLog(dir3);
            log.Append(new LogRecord { Type = LogType.Start, T = D(2026, 10, 31, 19) });
            log.Append(new LogRecord { Type = LogType.Check, T = D(2026, 11, 1, 9), GameId = la3.Id, Game = "로스트아크", Task = "카던" });
            File.AppendAllText(log.FileFor(D(2026, 11, 1)), "깨진 줄\n");
            Eq("월별 파일 + 깨진 줄 무시", 2, log.ReadAll().Count);
            Eq("기간 필터", 1, log.ReadAll(D(2026, 11, 1)).Count);
            log.SaveOpenSessions(new[] { laS });
            Eq("진행 중이던 세션 복구 → 기록", 1, log.RecoverOpenSessions());
            Check("복구 후 play 기록 생김", log.ReadAll().Any(r => r.Type == LogType.Play && r.GameId == la3.Id));
            Check("CSV에 한글 헤더", StatsLog.ToCsv(log.ReadAll()).StartsWith("종류,시각,게임"));
            Directory.Delete(dir3, true);

            Console.WriteLine("[통계 계산]");
            var rec = new List<LogRecord>();
            var nk4 = sd.Games.Single(g => g.Name == "니케");
            var la4Raid = la3.Tasks.First(t => t.Name == "레이드 1");
            var la4Kadun = la3.Tasks.First(t => t.Name == "카던");
            var c1 = la3.Characters[0];
            LogRecord Chk(DateTime t, Game g, HomeworkTask task, Character? ch = null, string type = LogType.Check) =>
                new() { Type = type, T = t, GameId = g.Id, Game = g.Name, TaskId = task.Id, Task = task.Name, Kind = task.Kind, CharId = ch?.Id, Char = ch?.Name };
            // 11/21(토) 19:00 켜고 19:20 첫 체크, 19:25, 19:50
            rec.Add(new LogRecord { Type = LogType.Start, T = D(2026, 11, 21, 19) });
            rec.Add(Chk(D(2026, 11, 21, 19, 20), la3, la4Kadun, c1));
            rec.Add(Chk(D(2026, 11, 21, 19, 25), la3, la4Raid, c1));
            rec.Add(Chk(D(2026, 11, 21, 19, 50), nk4, nk4.Tasks[0]));
            // 실수 체크 후 해제 → 무시
            rec.Add(Chk(D(2026, 11, 21, 19, 51), nk4, nk4.Tasks[1]));
            rec.Add(Chk(D(2026, 11, 21, 19, 51), nk4, nk4.Tasks[1], null, LogType.Uncheck));
            // 11/22(일) 20:00 켜고 20:06 첫 체크 (로아 레이드)
            rec.Add(new LogRecord { Type = LogType.Start, T = D(2026, 11, 22, 20) });
            rec.Add(Chk(D(2026, 11, 22, 20, 6), la3, la4Raid, la3.Characters[1]));
            // 로아 레이드 11/26(목) 새벽 3시 → 게임 기준 수요일
            rec.Add(new LogRecord { Type = LogType.Start, T = D(2026, 11, 26, 1) });
            rec.Add(Chk(D(2026, 11, 26, 3), la3, la4Raid, la3.Characters[2]));
            // 이벤트
            var evId1 = Guid.NewGuid(); var evId2 = Guid.NewGuid(); var evId3 = Guid.NewGuid();
            rec.Add(new LogRecord { Type = LogType.EventDone, T = D(2026, 11, 20, 12), GameId = nk4.Id, Game = "니케", EventId = evId1, EventName = "A", EndDate = D(2026, 11, 24), DaysLeft = 4 });
            rec.Add(new LogRecord { Type = LogType.EventDone, T = D(2026, 11, 20, 12), GameId = nk4.Id, Game = "니케", EventId = evId2, EventName = "B", EndDate = D(2026, 11, 22), DaysLeft = 2 });
            rec.Add(new LogRecord { Type = LogType.EventUndone, T = D(2026, 11, 20, 13), GameId = nk4.Id, Game = "니케", EventId = evId2 });
            rec.Add(new LogRecord { Type = LogType.EventMissed, T = D(2026, 11, 23, 12), GameId = nk4.Id, Game = "니케", EventId = evId3, EventName = "C", EndDate = D(2026, 11, 22) });
            // 플레이: 11/21 니케 먼저(19:05), 로아 19:10 / 하스스톤 18:50 (숙제 없는 게임이라 '먼저' 비교에서 제외)
            LogRecord Play(DateTime s, Game g, double runMin, double fgMin) =>
                new() { Type = LogType.Play, T = s, End = s.AddMinutes(runMin), GameId = g.Id, Game = g.Name, RunSec = runMin * 60, FgSec = fgMin * 60, PlayOnly = g.Type == GameType.PlayOnly ? true : null };
            rec.Add(Play(D(2026, 11, 21, 18, 50), hs, 170, 30));
            rec.Add(Play(D(2026, 11, 21, 19, 5), nk4, 40, 30));
            rec.Add(Play(D(2026, 11, 21, 19, 10), la3, 120, 90));
            rec.Add(Play(D(2026, 11, 22, 20, 1), la3, 60, 50));
            rec.Add(Play(D(2026, 11, 23, 2, 0), nk4, 30, 30)); // 새벽 2시 → 11/22 하루로 침 (로아가 먼저)

            var st = StatsEngine.Compute(rec, sd, new StatsOptions());
            Eq("첫 체크까지 (20분, 6분, 120분)", "20,6,120", string.Join(",", st.FirstCheckMinutes));
            Eq("체크 간격 (해제한 체크 제외, 재시작 구간 제외)", "5,25", string.Join(",", st.GapMinutes));
            Eq("간격 구간: 5~15분 1, 15~30분 1", "0,1,1,0,0,0", string.Join(",", st.GapBuckets));
            var raidStat = st.Weekdays.Single(w => w.Name == "로스트아크 · 레이드 1");
            Eq("레이드 요일: 토1 일1 수1 (게임 기준)", "0,0,1,0,0,1,1", string.Join(",", raidStat.Counts));
            Eq("레이드 초기화 요일 = 수", 2, raidStat.ResetIndex);
            Check("일일 숙제는 요일 통계에서 제외", st.Weekdays.All(w => !w.Name.Contains("카던")));
            Eq("이벤트: 취소한 완료 제외 → A 완료, C 놓침", "A:4,C:놓침", string.Join(",", st.Events.Select(e => e.Name + ":" + (e.Missed ? "놓침" : e.DaysLeft.ToString()))));
            Eq("플레이 1위 = 로아 (180분)", "로스트아크", st.Play[0].Game);
            Eq("하스스톤 켜둔 170분 / 맨 앞 30분", "10200/1800",
                st.Play.Where(p => p.PlayOnly).Select(p => p.RunSec + "/" + p.FgSec).Single());
            Eq("플레이한 날 수", 2, st.PlayDays);
            // 11/21: 하스스톤 18:50~21:40, 니케 19:05~19:45, 로아 19:10~21:10 → 실제 18:50~21:40 = 170분
            // 11/22: 로아 20:01~21:01 (60분) + 니케 11/23 02:00~02:30 (30분)
            Eq("실제로 게임한 시간 = 겹친 시간 한 번만 (170+60+30분)", 260.0 * 60, st.ActualPlaySec);
            Eq("동시에 켜 둔 시간 (420-260분)", 160.0 * 60, st.OverlapSec);
            Check("게임별 합계보다 실제 시간이 짧음", st.ActualPlaySec < st.TotalRunSec);
            Eq("구간 합치기: 겹침/포함/떨어짐", 150.0 * 60, StatsEngine.UnionSeconds(new[]
            {
                (D(2026, 1, 1, 10, 0), D(2026, 1, 1, 11, 0)),
                (D(2026, 1, 1, 10, 30), D(2026, 1, 1, 11, 30)),   // 겹침 → 10:00~11:30
                (D(2026, 1, 1, 10, 40), D(2026, 1, 1, 10, 50)),   // 포함
                (D(2026, 1, 1, 13, 0), D(2026, 1, 1, 14, 0)),     // 떨어짐 → +60
            }));
            Eq("기간 시작 전 부분은 잘라냄 (11/22 이후: 60+30분)", 90.0 * 60,
                StatsEngine.Compute(rec, sd, new StatsOptions { From = D(2026, 11, 22) }).ActualPlaySec);
            Eq("먼저 시작: 니케 1일, 로아 1일 (하스스톤 제외)", "니케:1,로스트아크:1",
                string.Join(",", st.FirstGames.OrderBy(f => f.Game).Select(f => f.Game + ":" + f.Days)));
            var noFree = StatsEngine.Compute(rec, sd, new StatsOptions { IncludePlayOnly = false });
            Check("숙제 없는 게임 제외 옵션", noFree.Play.All(p => !p.PlayOnly) && noFree.TotalRunSec == (40 + 120 + 60 + 30) * 60);
            var onlyLa = StatsEngine.Compute(rec, sd, new StatsOptions { GameId = la3.Id });
            Check("게임 필터: 로아만", onlyLa.Play.Count == 1 && onlyLa.Events.Count == 0 && onlyLa.Weekdays.All(w => w.Name.StartsWith("로스트아크")));
            var recent = StatsEngine.Compute(rec, sd, new StatsOptions { From = D(2026, 11, 22) });
            Eq("기간 필터: 11/22 이후 첫 체크 (6분, 120분)", "6,120", string.Join(",", recent.FirstCheckMinutes));

            Console.WriteLine("[일일 숙제 순서]");
            var od = new List<LogRecord>();
            var sr = sd.Games.Single(g => g.Name == "붕괴: 스타레일");
            var mz = sd.Games.Single(g => g.Name == "명조");
            var laKadun = la3.Tasks.First(t => t.Name == "카던");
            var laRaid = la3.Tasks.First(t => t.Name == "레이드 1");
            // 11/20: 로아 → 스타레일 → 니케
            od.Add(Chk(D(2026, 11, 20, 19, 0), la3, laKadun, la3.Characters[0]));
            od.Add(Chk(D(2026, 11, 20, 19, 5), la3, laKadun, la3.Characters[1])); // 같은 게임 두 번째 체크는 순서에 영향 없음
            od.Add(Chk(D(2026, 11, 20, 19, 30), sr, sr.Tasks[0]));
            od.Add(Chk(D(2026, 11, 20, 20, 0), nk4, nk4.Tasks[0]));
            // 11/21: 로아 → 스타레일 → 니케 (주간 체크는 무시)
            od.Add(Chk(D(2026, 11, 21, 18, 0), mz, mz.Tasks[1]));       // 명조 주간퀘 → 무시
            od.Add(Chk(D(2026, 11, 21, 19, 0), la3, laKadun, la3.Characters[0]));
            od.Add(Chk(D(2026, 11, 21, 19, 10), la3, laRaid, la3.Characters[0])); // 주간 → 무시
            od.Add(Chk(D(2026, 11, 21, 19, 20), sr, sr.Tasks[0]));
            od.Add(Chk(D(2026, 11, 22, 1, 0), nk4, nk4.Tasks[0]));      // 새벽 1시 → 11/21로 침
            // 11/22: 스타레일 → 로아 (니케 실수 체크 후 해제)
            od.Add(Chk(D(2026, 11, 22, 18, 0), nk4, nk4.Tasks[0]));
            od.Add(Chk(D(2026, 11, 22, 18, 0), nk4, nk4.Tasks[0], null, LogType.Uncheck));
            od.Add(Chk(D(2026, 11, 22, 19, 0), sr, sr.Tasks[0]));
            od.Add(Chk(D(2026, 11, 22, 19, 30), la3, laKadun, la3.Characters[2]));
            var os = StatsEngine.Compute(od, sd, new StatsOptions());
            Eq("일일 순서 집계한 날", 3, os.DailyOrderDays);
            Eq("평균 순서: 로아 1.33, 스타레일 1.67, 니케 3",
                "로스트아크:1.33:3,붕괴: 스타레일:1.67:3,니케:3:2",
                string.Join(",", os.DailyOrderAvg.Select(a => a.Game + ":" + Math.Round(a.AvgRank, 2) + ":" + a.Days)));
            Eq("자주 나온 순서 1위 (2일)", "로스트아크>붕괴: 스타레일>니케:2",
                string.Join(">", os.DailyOrderSeq[0].Games) + ":" + os.DailyOrderSeq[0].Days);
            Eq("자주 나온 순서 2위 (1일)", "붕괴: 스타레일>로스트아크:1",
                string.Join(">", os.DailyOrderSeq[1].Games) + ":" + os.DailyOrderSeq[1].Days);
            Check("게임 필터를 걸어도 순서는 전체 비교",
                StatsEngine.Compute(od, sd, new StatsOptions { GameId = nk4.Id }).DailyOrderAvg.Count == 3);
            Console.WriteLine("[요일별 게임 시간]");
            // rec 플레이: 11/21(토) 하스스톤 170분(18:50~21:40), 니케 40, 로아 120 / 11/22(일) 로아 60 / 11/23 02:00 니케 30 → 일요일(새벽 4시 기준)
            var wst = StatsEngine.Compute(rec, sd, new StatsOptions { Now = D(2026, 11, 23, 12) });
            var wdla = wst.WeekdayPlay.Single(w => w.Game == "로스트아크");
            Eq("로아: 토 120분, 일 60분", "0,0,0,0,0,120,60", string.Join(",", wdla.RunSec.Select(x => Math.Round(x / 60))));
            var wnk = wst.WeekdayPlay.Single(w => w.Game == "니케");
            Eq("니케: 토 40분, 일 30분 (새벽 2시 → 전날)", "0,0,0,0,0,40,30", string.Join(",", wnk.RunSec.Select(x => Math.Round(x / 60))));
            Eq("실제 게임 시간: 토 170분, 일 90분", "0,0,0,0,0,170,90", string.Join(",", wst.WeekdayActualSec.Select(x => Math.Round(x / 60))));
            Eq("기간 안 요일 수 (11/21 토 ~ 11/23 월)", "1,0,0,0,0,1,1", string.Join(",", wst.WeekdayDayCount));
            Check("숙제 없는 게임 표시", wst.WeekdayPlay.Single(w => w.Game == "하스스톤").PlayOnly);
            // 날을 넘긴 세션: 금 22:00 ~ 토 06:00 (8시간) → 금 6시간(22~04), 토 2시간(04~06)
            var overnight = new List<LogRecord> { Play(D(2026, 11, 20, 22), la3, 480, 240) };
            var ost = StatsEngine.Compute(overnight, sd, new StatsOptions { Now = D(2026, 11, 21, 12) });
            Eq("날 넘긴 세션 나눔: 금 360분, 토 120분", "0,0,0,0,360,120,0",
                string.Join(",", ost.WeekdayPlay.Single().RunSec.Select(x => Math.Round(x / 60))));
            Eq("맨 앞 시간도 같은 비율로 (금 180, 토 60)", "0,0,0,0,180,60,0",
                string.Join(",", ost.WeekdayPlay.Single().FgSec.Select(x => Math.Round(x / 60))));
            var w7 = StatsEngine.Compute(rec, sd, new StatsOptions { From = D(2026, 11, 17, 4), Now = D(2026, 11, 23, 12) });
            Eq("최근 7일이면 요일마다 1번 (11/17 화 ~ 11/23 월)", "1,1,1,1,1,1,1", string.Join(",", w7.WeekdayDayCount));

            Eq("중간값", 12.0, StatsEngine.Median(new double[] { 5, 12, 40 }));
            Eq("시간 표시", "3시간 40분", StatsEngine.FormatDuration(220 * 60));

            Console.WriteLine("[테마]");
            Eq("테마 3개", "Dark,Light,Glass", string.Join(",", WidgetTheme.All.Select(t => t.Id)));
            Eq("모르는 테마 → 다크", "Dark", WidgetTheme.Get("없는테마").Id);
            foreach (var th in WidgetTheme.All)
            {
                // 이미지가 없을 때 (바탕화면 위) 기본 가림 정도로 덮인 배경 기준, 가장 나쁜 경우(흰/검은 바탕) 대비 확인
                foreach (var under in new[] { "#000000", "#FFFFFF", "#7F7F7F" })
                {
                    var bg = WidgetTheme.Blend(th.Overlay, th.DefaultOverlayWithImage, under);
                    bool shadowHelps = th.TextShadow;
                    double c = WidgetTheme.Contrast(th.Text, bg);
                    // 그림자 있는 글래스는 3:1, 나머지는 4.5:1 이상
                    Check($"{th.Name} 본문 글씨 대비 ({under} 위, 가림 {th.DefaultOverlayWithImage:P0}) = {c:F1}", c >= (shadowHelps ? 3.0 : 4.5));
                }
                var solid = WidgetTheme.Blend(th.Overlay, th.DefaultOverlayNoImage, th.Overlay == "#000000" ? "#3A4350" : th.Overlay);
                Check($"{th.Name} 보조 글씨 대비 ≥ 4.5 (이미지 없음)", WidgetTheme.Contrast(th.Muted, solid) >= 4.5 || th.TextShadow);
                Check($"{th.Name} 배지 4종", th.Badge.Length == 4);
            }
            var pl = new WidgetPlacement { Theme = "Light" };
            Eq("가림 기본값 (이미지 없음)", 0.94, pl.EffectiveOverlay);
            pl.BackgroundImage = "a.png";
            Eq("가림 기본값 (이미지 있음)", 0.75, pl.EffectiveOverlay);
            pl.OverlayOpacity = 0.5;
            Eq("직접 정한 가림 정도", 0.5, pl.EffectiveOverlay);

            Console.WriteLine("[완료한 것 아래로 정렬]");
            var sdd = DefaultData.Create();
            var wnow = D(2026, 11, 24, 12);
            var gl = sdd.Games.Single(g => g.Name == "로스트아크");
            var gnk = sdd.Games.Single(g => g.Name == "니케");
            var ghi3 = sdd.Games.Single(g => g.Name == "붕괴3rd");
            // 니케 일퀘 완료 → 니케 카드가 일일 위젯 맨 아래로
            sdd.Completions[ResetCalculator.Key(gnk.Tasks[0], null)] = wnow;
            var dps = sdd.Games.Select(g => ProgressBuilder.Build(g, sdd, wnow, WidgetKind.Daily)).Where(x => x.HasContent).ToList();
            var ordered = WidgetOrdering.Games(dps, true);
            Eq("끝낸 게임(니케)은 맨 아래", "니케", ordered.Last().Game.Name);
            Eq("나머지는 등록 순서 유지 (붕3이 맨 위)", "붕괴3rd", ordered.First().Game.Name);
            Eq("정렬 끄면 등록 순서", "니케", WidgetOrdering.Games(dps, false)[1].Game.Name);
            // 붕3 주간 위젯: 주간퀘 완료 → [요일] 심연 줄이 위, [주간] 줄이 아래
            sdd.Completions[ResetCalculator.Key(ghi3.Tasks[1], null)] = wnow;
            var hp = ProgressBuilder.Build(ghi3, sdd, wnow, WidgetKind.Weekly);
            var rows = WidgetOrdering.CommonRows(hp.Cells, true);
            Eq("다 끝낸 줄은 아래로", "심연|주간퀘", string.Join("|", rows.Select(r => string.Join(",", r.Select(c => c.Task.Name)))));
            // 로아 일일 표: 카던을 세 캐릭 다 끝냄 → 가토가 위로
            foreach (var ch in gl.Characters) sdd.Completions[ResetCalculator.Key(gl.Tasks.First(t => t.Name == "카던"), ch)] = wnow;
            var lp = ProgressBuilder.Build(gl, sdd, wnow, WidgetKind.Daily);
            var perC = lp.VisibleTasks.Where(t => ResetCalculator.IsPerCharacter(t, gl));
            Eq("RPG 표: 다 끝낸 줄(카던)은 아래로", "가토,카던", string.Join(",", WidgetOrdering.GridGroups(lp, perC, true).Single().Select(t => t.Name)));
            Eq("RPG 표: 한 캐릭만 남아도 위에 그대로", "카던,가토",
                string.Join(",", WidgetOrdering.GridGroups(lp, perC, false).Single().Select(t => t.Name)));
            // 이벤트: 완료한 것 아래로, 그 안에서 마감 순서 유지
            var e1 = new EventQuest { Name = "가", EndDate = D(2026, 11, 25), Done = true };
            var e2 = new EventQuest { Name = "나", EndDate = D(2026, 11, 26) };
            var e3 = new EventQuest { Name = "다", EndDate = D(2026, 11, 30) };
            Eq("이벤트: 완료는 아래로", "나,다,가", string.Join(",", WidgetOrdering.Events(new[] { e1, e2, e3 }, true).Select(e => e.Name)));
            Check("정렬 기본값 켜짐", new WidgetSettings().SortDoneToBottom);

            Console.WriteLine("[초기화 전날 경고]");
            var dd = DefaultData.Create();
            var dsr = dd.Games.Single(g => g.Name == "붕괴: 스타레일");   // 주간 월요일 05:00
            var dWeekly = dsr.Tasks.Single(t => t.Kind == ResetKind.Weekly);
            Check("토요일 → 경고 없음", !ResetCalculator.IsDueSoon(dWeekly, dsr, D(2026, 11, 28, 20)));
            Check("일요일 04:59 (게임상 토요일) → 경고 없음", !ResetCalculator.IsDueSoon(dWeekly, dsr, D(2026, 11, 29, 4, 59)));
            Check("일요일 05:00부터 경고", ResetCalculator.IsDueSoon(dWeekly, dsr, D(2026, 11, 29, 5, 0)));
            Check("월요일 04:59까지 경고", ResetCalculator.IsDueSoon(dWeekly, dsr, D(2026, 11, 30, 4, 59)));
            Check("월요일 05:00 초기화 후 경고 없음", !ResetCalculator.IsDueSoon(dWeekly, dsr, D(2026, 11, 30, 5, 0)));
            Check("일일 숙제는 경고 안 함", !ResetCalculator.IsDueSoon(dsr.Tasks[0], dsr, D(2026, 11, 29, 23)));
            var dla = dd.Games.Single(g => g.Name == "로스트아크");     // 수요일 06:00
            Check("로아 화요일 07:00 → 경고", ResetCalculator.IsDueSoon(dla.Tasks.First(t => t.Name == "레이드 1"), dla, D(2026, 11, 24, 7)));
            var dhi = dd.Games.Single(g => g.Name == "붕괴3rd");
            var dAbyss = dhi.Tasks.Single(t => t.Name == "심연");
            Check("심연(화·금)은 당일에만 보이므로 전날 경고 없음",
                !ResetCalculator.IsDueSoon(dAbyss, dhi, D(2026, 11, 23, 12)) && !ResetCalculator.IsDueSoon(dAbyss, dhi, D(2026, 11, 26, 12)));
            Console.WriteLine("[특정 요일 숙제는 그 요일에만 표시]");
            Check("화요일 낮 → 보임", ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 24, 12)));
            Check("수요일 03:59 (게임상 화요일) → 보임", ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 25, 3, 59)));
            Check("수요일 04:00 → 숨김", !ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 25, 4, 0)));
            Check("목요일 → 숨김", !ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 26, 12)));
            Check("금요일 → 보임", ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 27, 12)));
            Check("토·일·월 → 숨김", !ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 28, 12))
                && !ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 29, 12)) && !ResetCalculator.IsVisibleToday(dAbyss, dhi, D(2026, 11, 30, 12)));
            dd.Completions[ResetCalculator.Key(dAbyss, null)] = D(2026, 11, 24, 20);
            Check("화요일에 하면 그날 완료", ResetCalculator.IsDone(dAbyss, dhi, D(2026, 11, 24, 20), D(2026, 11, 24, 21)));
            Check("금요일에는 다시 해야 함", !ResetCalculator.IsDone(dAbyss, dhi, D(2026, 11, 24, 20), D(2026, 11, 27, 12)));
            Eq("수요일 주간 위젯 붕3 = 주간퀘만", "주간퀘",
                string.Join(",", ProgressBuilder.Build(dhi, dd, D(2026, 11, 25, 12), WidgetKind.Weekly).VisibleTasks.Select(t => t.Name)));
            Eq("규칙 설명", "화·금에만 (04:00 기준)", ResetCalculator.DescribeRule(dAbyss, dhi));
            Check("심연: 화요일 당일엔 경고 (오늘만 할 수 있음)", ResetCalculator.IsDueSoon(dAbyss, dhi, D(2026, 11, 24, 10)));
            Eq("심연 마감 = 수요일 04:00 (그 뒤엔 사라짐)", D(2026, 11, 25, 4), ResetCalculator.DueAt(dAbyss, dhi, D(2026, 11, 24, 10)));
            Eq("심연 안내 문구", "내일 04:00까지", ResetCalculator.DueLabel(ResetCalculator.DueAt(dAbyss, dhi, D(2026, 11, 24, 10)), D(2026, 11, 24, 10)));

            // 실제 사용자 데이터로 재현 (GH_REAL_DATA 환경변수가 있을 때만)
            var real = Environment.GetEnvironmentVariable("GH_REAL_DATA");
            if (!string.IsNullOrEmpty(real) && File.Exists(real))
            {
                var rd = DataStore.Deserialize(File.ReadAllText(real))!;
                var rnow = D(2026, 11, 24, 10, 6);
                foreach (var g in rd.Games)
                {
                    var rp = ProgressBuilder.Build(g, rd, rnow, WidgetKind.Weekly);
                    if (!rp.HasContent) continue;
                    Console.WriteLine($"    [실데이터] {g.Name}: {rp.DoneCount}/{rp.Total}, 경고 {rp.DueSoonUndone.Count}개" +
                        (rp.HasDueSoon ? $" ({ResetCalculator.DueLabel(rp.DueSoonUndone.Min(c => c.DueAt), rnow)})" : "") +
                        " — " + string.Join(", ", rp.VisibleTasks.Select(t => t.Name)));
                }
            }
            var dmh = dd.Games.Single(g => g.Name == "마비노기 영웅전");
            Check("미탑(매월 1일): 말일에 경고", ResetCalculator.IsDueSoon(dmh.Tasks.Single(t => t.Name == "미탑"), dmh, D(2026, 11, 30, 12)));
            Eq("안내 문구 (전날 저녁)", "내일 05:00까지", ResetCalculator.DueLabel(D(2026, 11, 30, 5), D(2026, 11, 29, 21)));
            Eq("안내 문구 (자정 넘어서)", "오늘 05:00까지", ResetCalculator.DueLabel(D(2026, 11, 30, 5), D(2026, 11, 30, 1)));
            var dp = ProgressBuilder.Build(dla, dd, D(2026, 11, 24, 12), WidgetKind.Weekly);
            Eq("로아 주간 위젯: 남은 칸 12개 모두 경고 대상", 12, dp.DueSoonUndone.Count);
            dd.Completions[ResetCalculator.Key(dla.Tasks.First(t => t.Name == "모래시계"), dla.Characters[0])] = D(2026, 11, 24, 11);
            dp = ProgressBuilder.Build(dla, dd, D(2026, 11, 24, 12), WidgetKind.Weekly);
            Eq("하나 체크하면 11개", 11, dp.DueSoonUndone.Count);
            Check("일일 위젯에는 경고 없음", !ProgressBuilder.Build(dla, dd, D(2026, 11, 24, 12), WidgetKind.Daily).HasDueSoon);
            foreach (var th in WidgetTheme.All)
            {
                var bg = WidgetTheme.Blend(th.Overlay, th.DefaultOverlayNoImage, th.Overlay == "#000000" ? "#3A4350" : th.Overlay);
                double c = WidgetTheme.Contrast(th.DueText, bg);
                Check($"{th.Name} 경고 글씨 대비 {c:F1}", c >= (th.TextShadow ? 3.0 : 4.5));
            }

            Console.WriteLine("[합쳐서 보기]");
            var cd = DefaultData.Create();
            var cla = cd.Games.Single(g => g.Name == "로스트아크");
            var cnk = cd.Games.Single(g => g.Name == "니케");
            cd.Events.Add(new EventQuest { GameId = cnk.Id, Name = "이벤트", EndDate = D(2026, 12, 5) });
            var tueC = D(2026, 11, 24, 12);
            var comb = ProgressBuilder.Build(cla, cd, tueC, WidgetKind.Combined);
            Eq("로아 합친 위젯 = 일일 7 + 주간 12", 19, comb.Total);
            var combNk = ProgressBuilder.Build(cnk, cd, tueC, WidgetKind.Combined);
            Eq("니케 합친 위젯 = 일퀘 + 주간퀘 + 이벤트", 3, combNk.Total);
            Eq("합친 위젯 다음 초기화 = 가장 빠른 것 (일일)", ResetCalculator.NextDailyReset(cnk, tueC), combNk.NextReset);
            Console.WriteLine("[항상 위]");
            var tws = new WidgetSettings();
            Check("기본은 꺼짐 (바탕화면 고정)", !tws.Get(WidgetKind.Daily).AlwaysOnTop);
            tws.Get(WidgetKind.Events).AlwaysOnTop = true;
            var tjson = DataStore.Serialize(new AppData { Widget = tws });
            var tload = DataStore.Deserialize(tjson)!;
            Check("위젯별로 저장 (이벤트만 켜짐)", tload.Widget.Get(WidgetKind.Events).AlwaysOnTop && !tload.Widget.Get(WidgetKind.Daily).AlwaysOnTop);

            Console.WriteLine("[위젯 칸 (드래그로 합치기)]");
            string Lay(WidgetSettings w) => string.Join(" | ", w.SlotList.Select(sl => sl.Count == 0 ? "-" : string.Join("+", sl)));
            var sw = new WidgetSettings();
            Eq("처음: 5칸에 하나씩", "Daily | Weekly | Cycle | Events | Pickup", Lay(sw));
            Eq("창 5개", 5, sw.ActiveSlots.Count);
            sw.Move(WidgetKind.Weekly, 0);
            Eq("주간을 1번 칸으로 끌면 합쳐짐", "Daily+Weekly | - | Cycle | Events | Pickup", Lay(sw));
            Eq("빈 칸이 생겨서 창 4개", "0,2,3,4", string.Join(",", sw.ActiveSlots));
            sw.Move(WidgetKind.Events, 0, 0);
            Eq("맨 앞에 끼워 넣기", "Events+Daily+Weekly | - | Cycle | - | Pickup", Lay(sw));
            sw.Move(WidgetKind.Events, 0, 3);
            Eq("같은 칸 안에서 순서 바꾸기", "Daily+Weekly+Events | - | Cycle | - | Pickup", Lay(sw));
            sw.GetSlot(1).Visible = false;
            sw.Move(WidgetKind.Cycle, 1);
            Check("빈 칸으로 빼면 그 칸 창이 다시 생기고 켜짐", Lay(sw) == "Daily+Weekly+Events | Cycle | - | - | Pickup" && sw.GetSlot(1).Visible);
            Eq("칸 이름", "위젯 1 — 일일 · 주간 · 이벤트", sw.SlotLabel(0));
            sw.ResetSlots();
            Eq("처음 상태로", "Daily | Weekly | Cycle | Events | Pickup", Lay(sw));

            var bad = new WidgetSettings { Slots = new() { new() { WidgetKind.Daily, WidgetKind.Daily, WidgetKind.Combined }, new() { WidgetKind.Weekly } } };
            Eq("망가진 배치 정리 (중복 제거, 빠진 종류는 빈 칸에)", "Daily | Weekly | Cycle | Events | Pickup", Lay(bad));

            // 예전 "합쳐서 보기" → 칸으로 옮기기
            var oldJson = DataStore.Deserialize("{\"Games\":[],\"Widget\":{\"CombinedMode\":true}}")!;
            Eq("예전 합쳐서 보기(합칠 칸 정보 없음) → 일일·주간·이벤트 한 칸", "Daily+Weekly+Events | Cycle | Pickup | - | -", Lay(oldJson.Widget));
            var oldW = new WidgetSettings { CombinedMode = true, CombinedKinds = new() { WidgetKind.Pickup, WidgetKind.Daily } };
            oldW.Get("Combined").Left = 500;
            oldW.Get(WidgetKind.Weekly).Theme = "Light";
            Eq("예전 합칠 칸 → 첫 칸", "Daily+Pickup | Weekly | Cycle | Events | -", Lay(oldW));
            Check("합친 위젯 위치 · 주간 위젯 테마 유지", oldW.GetSlot(0).Left == 500 && oldW.GetSlot(1).Theme == "Light");
            var oldSep = new WidgetSettings();
            oldSep.Placements["Events"] = new WidgetPlacement { Left = 77 };
            Eq("예전 따로 보기 → 칸마다 하나, 위치 유지", 77.0, oldSep.GetSlot(3).Left);
            var rtW = DataStore.Deserialize(DataStore.Serialize(new AppData { Widget = sw }))!;
            sw.Move(WidgetKind.Pickup, 0);
            var rtW2 = DataStore.Deserialize(DataStore.Serialize(new AppData { Widget = sw }))!;
            Eq("저장 왕복", Lay(sw), Lay(rtW2.Widget));

            Console.WriteLine("[오늘 요약]");
            var td = DefaultData.Create();
            var tla = td.Games.Single(g => g.Name == "로스트아크");
            var ths = td.Games.Single(g => g.Name == "하스스톤");
            var tnk = td.Games.Single(g => g.Name == "니케");
            var tnow = D(2026, 11, 24, 21, 0);
            var trec = new List<LogRecord>
            {
                new() { Type = LogType.Start, T = D(2026, 11, 23, 19, 0) },          // 어제 켠 기록
                new() { Type = LogType.Start, T = D(2026, 11, 24, 19, 2) },          // 오늘 19:02 켬
                // 어제 밤 ~ 오늘 새벽 5시 세션 → 오늘(04:00~) 부분 1시간만
                new() { Type = LogType.Play, T = D(2026, 11, 24, 2, 0), End = D(2026, 11, 24, 5, 0), GameId = tla.Id, Game = "로스트아크", RunSec = 3 * 3600, FgSec = 3 * 3600 },
                // 오늘 19:10~19:40 니케 (끝남)
                new() { Type = LogType.Play, T = D(2026, 11, 24, 19, 10), End = D(2026, 11, 24, 19, 40), GameId = tnk.Id, Game = "니케", RunSec = 1800, FgSec = 1800 },
                new() { Type = LogType.Check, T = D(2026, 11, 24, 19, 30), GameId = tnk.Id, TaskId = tnk.Tasks[0].Id, Kind = ResetKind.Daily },
            };
            td.Completions[ResetCalculator.Key(tnk.Tasks[0], null)] = D(2026, 11, 24, 19, 30);
            // 지금 실행 중: 로아 20:00~, 하스스톤 20:00~ (둘 다 1시간째)
            var topen = new List<PlaySession>
            {
                new() { GameId = tla.Id, Game = "로스트아크", Start = D(2026, 11, 24, 20, 0), LastSeen = tnow, RunSec = 3600, FgSec = 2400 },
                new() { GameId = ths.Id, Game = "하스스톤", PlayOnly = true, Start = D(2026, 11, 24, 20, 0), LastSeen = tnow, RunSec = 3600, FgSec = 1200 },
            };
            var ts = TodaySummary.Build(trec, topen, td, tnow);
            Eq("PC 켠 시각 = 마지막 시작", D(2026, 11, 24, 19, 2), ts.PcOnAt);
            Eq("켜진 지 1시간 58분", 118.0, Math.Round(ts.OnFor.TotalMinutes));
            // 실제: 04:00~05:00 (60) + 19:10~19:40 (30) + 20:00~21:00 (60, 로아·하스스톤 동시) = 150분
            Eq("오늘 실제 게임 시간 (실행 중 포함, 동시 실행 한 번만)", 150.0, Math.Round(ts.ActualPlaySec / 60));
            Eq("로아 오늘 = 새벽 1시간 + 실행 중 1시간", 120.0, Math.Round(ts.Games.Single(g => g.Game == "로스트아크").RunSec / 60));
            Check("실행 중 표시", ts.Games.Single(g => g.Game == "로스트아크").Running && !ts.Games.Single(g => g.Game == "니케").Running);
            Eq("실행 중 게임", "로스트아크,하스스톤", string.Join(",", ts.RunningGames.OrderBy(x => x)));
            Eq("오늘 체크 수", 1, ts.ChecksToday);
            Check("일일 진행 (니케 일퀘 1개 완료)", ts.DailyDone == 1 && ts.DailyTotal > 1);
            Eq("트레이 한 줄", $"오늘 게임 2시간 30분 · 일일 1/{ts.DailyTotal}", ts.ShortLine());

            // ===== 주기 컨텐츠 (N주마다) =====
            var cg = new Game { Name = "스타레일", ResetTime = "05:00" };
            var cyc = new HomeworkTask
            {
                Name = "허구 이야기", Kind = ResetKind.Cycle, Scope = TaskScope.Common,
                CycleWeeks = 6, CycleFirstEnd = new DateTime(2026, 12, 7), CycleEndDay = DayOfWeek.Monday
            };
            cg.Tasks.Add(cyc);
            var cdata = new AppData();
            cdata.Games.Add(cg);
            Eq("첫 회차 마감 12/7", new DateTime(2026, 12, 7), ResetCalculator.CycleCurrentEnd(cyc, cg, D(2026, 11, 25, 12)));
            Eq("첫 회차 D-12", 12, ResetCalculator.CycleDaysLeft(cyc, cg, D(2026, 11, 25, 12)));
            Check("D-12엔 경고 없음", !ResetCalculator.IsDueSoon(cyc, cg, D(2026, 11, 25, 12)));
            Check("D-7(11/30)부터 경고", ResetCalculator.IsDueSoon(cyc, cg, D(2026, 11, 30, 12)));
            Check("D-8(11/29)엔 경고 없음", !ResetCalculator.IsDueSoon(cyc, cg, D(2026, 11, 29, 12)));
            Eq("경고 문구", "D-7 · 12/7(월)까지", ResetCalculator.DueText(cyc, cg, D(2026, 11, 30, 12)));
            Check("첫 회차: 오늘 체크 → 완료", ResetCalculator.IsDone(cyc, cg, D(2026, 11, 25, 13), D(2026, 11, 26, 12)));
            Eq("12/8 04:00 (게임상 12/7) = 아직 첫 회차 D-DAY", 0, ResetCalculator.CycleDaysLeft(cyc, cg, D(2026, 12, 8, 4)));
            Check("12/8 04:00 완료 유지", ResetCalculator.IsDone(cyc, cg, D(2026, 11, 25, 13), D(2026, 12, 8, 4)));
            Check("12/8 05:00 초기화 → 미완료", !ResetCalculator.IsDone(cyc, cg, D(2026, 11, 25, 13), D(2026, 12, 8, 5)));
            Eq("두 번째 회차 마감 = 6주 뒤 월요일 1/18", new DateTime(2027, 1, 18), ResetCalculator.CycleCurrentEnd(cyc, cg, D(2026, 12, 8, 5)));
            Eq("다음 초기화 1/19 05:00", D(2027, 1, 19, 5), ResetCalculator.NextReset(cyc, cg, D(2026, 12, 8, 5)));
            Eq("세 번째 회차 시작 = 1/19 05:00", D(2027, 1, 19, 5), ResetCalculator.LastReset(cyc, cg, D(2027, 1, 20, 12)));
            Eq("세 번째 회차 마감 3/1", new DateTime(2027, 3, 1), ResetCalculator.CycleCurrentEnd(cyc, cg, D(2027, 1, 20, 12)));
            Eq("1년 뒤도 6주 간격 유지", 0, (ResetCalculator.CycleCurrentEnd(cyc, cg, D(2027, 12, 1, 12)) - new DateTime(2027, 1, 18)).Days % 42);
            Eq("미리보기 3개", "12/7,1/18,3/1", string.Join(",", ResetCalculator.CycleUpcomingEnds(cyc, new DateTime(2026, 11, 25), 3).Select(d => d.ToString("M'/'d"))));
            var cyc2 = new HomeworkTask { Kind = ResetKind.Cycle, CycleWeeks = 4, CycleFirstEnd = new DateTime(2026, 12, 7), CycleEndDay = DayOfWeek.Wednesday };
            Eq("다음부터 수요일: 4주 뒤 월요일 직전 수요일 12/30", new DateTime(2026, 12, 30), ResetCalculator.CycleEnd1(cyc2));
            Eq("그다음은 4주 간격 1/27", new DateTime(2027, 1, 27), ResetCalculator.CycleEndOnOrAfter(cyc2, new DateTime(2026, 12, 31)));
            Eq("주기 컨텐츠는 전용 위젯", WidgetKind.Cycle, ResetCalculator.WidgetFor(ResetKind.Cycle));
            Eq("주기 위젯에 표시", 1, ProgressBuilder.Build(cg, cdata, D(2026, 11, 25, 12), WidgetKind.Cycle).Cells.Count);
            Eq("주간 위젯엔 없음", 0, ProgressBuilder.Build(cg, cdata, D(2026, 11, 25, 12), WidgetKind.Weekly).Cells.Count);
            Eq("일일 위젯엔 없음", 0, ProgressBuilder.Build(cg, cdata, D(2026, 11, 25, 12), WidgetKind.Daily).Cells.Count);
            Check("규칙 설명", ResetCalculator.DescribeRule(cyc, cg).StartsWith("6주마다 월요일까지"));

            // 등록/수정: 회차 시작
            var cg2 = new Game { Name = "젠존제", ResetTime = "05:00" };
            var ct = new HomeworkTask { Name = "시유 방어전", Scope = TaskScope.Common };
            ResetCalculator.ApplyCycle(ct, cg2, new DateTime(2026, 12, 7), 4, DayOfWeek.Monday, D(2026, 11, 25, 12), isNew: true);
            Eq("새로 등록: 회차 시작 = 오늘 05:00", D(2026, 11, 25, 5), ct.CycleStart);
            Check("등록 전(어제) 체크는 인정 안 함", !ResetCalculator.IsDone(ct, cg2, D(2026, 11, 24, 20), D(2026, 11, 25, 12)));
            // 다음 회차 중에 수정 → 지금 회차 시작(12/8 05:00)부터 한 체크는 유지, 지난 회차 체크는 인정 안 함
            ResetCalculator.ApplyCycle(ct, cg2, new DateTime(2027, 1, 4), 4, DayOfWeek.Monday, D(2026, 12, 20, 12), isNew: false);
            Eq("수정: 회차 시작 유지", D(2026, 12, 8, 5), ct.CycleStart);
            Check("수정 후 이번 회차 체크 유지", ResetCalculator.IsDone(ct, cg2, D(2026, 12, 10, 12), D(2026, 12, 20, 13)));
            Check("수정 후 지난 회차 체크는 미완료", !ResetCalculator.IsDone(ct, cg2, D(2026, 12, 1, 12), D(2026, 12, 20, 13)));

            // ===== 픽업 =====
            var pkg = new Game { Name = "니케", ResetTime = "04:00" };
            var pk = new PickupBanner { GameId = pkg.Id, Name = "레드후드", StartDate = new DateTime(2026, 11, 27), EndDate = new DateTime(2026, 12, 10) };
            var pk2 = new PickupBanner { GameId = pkg.Id, Name = "지금 픽업", StartDate = new DateTime(2026, 11, 20), EndDate = new DateTime(2026, 11, 26) };
            var pdata = new AppData();
            pdata.Games.Add(pkg);
            pdata.Pickups.Add(pk);
            pdata.Pickups.Add(pk2);
            Eq("시작 전 = 예정", PickupStatus.Upcoming, PickupCalc.Status(pk, pkg, D(2026, 11, 25, 12)));
            Eq("예정 문구", "11/27 시작 (D-2)", PickupCalc.PeriodText(pk, pkg, D(2026, 11, 25, 12)));
            Eq("11/27 03:00 (게임상 11/26) 아직 예정", PickupStatus.Upcoming, PickupCalc.Status(pk, pkg, D(2026, 11, 27, 3)));
            Eq("11/27 04:00 진행", PickupStatus.Active, PickupCalc.Status(pk, pkg, D(2026, 11, 27, 4)));
            Eq("진행 문구", "D-1 · ~12/10", PickupCalc.PeriodText(pk, pkg, D(2026, 12, 9, 12)));
            Eq("12/11 03:59 마지막 날", PickupStatus.Active, PickupCalc.Status(pk, pkg, D(2026, 12, 11, 3, 59)));
            Eq("12/11 04:00 종료", PickupStatus.Ended, PickupCalc.Status(pk, pkg, D(2026, 12, 11, 4)));
            var pw = PickupCalc.ForWidget(pdata, D(2026, 11, 25, 12));
            Check("위젯: 진행 1 · 예정 1", pw.Count == 1 && pw[0].Active.Count == 1 && pw[0].Upcoming.Count == 1);
            Check("11/27 정오: 끝난 픽업 자동 삭제", DataStore.PurgeExpired(pdata, D(2026, 11, 27, 12)) && pdata.Pickups.Count == 1 && pdata.Pickups[0] == pk);
            pdata.Games.Clear();
            Check("게임 삭제 → 픽업도 삭제", PickupCalc.Purge(pdata, D(2026, 11, 27, 12)) && pdata.Pickups.Count == 0);

            // 저장 왕복
            cdata.Pickups.Add(new PickupBanner { GameId = cg.Id, Name = "반디", StartDate = new DateTime(2026, 11, 25), EndDate = new DateTime(2026, 12, 16) });
            var rt = DataStore.Deserialize(DataStore.Serialize(cdata))!;
            var rtc = rt.Games[0].Tasks[0];
            Check("저장 왕복: 주기 설정", rtc.Kind == ResetKind.Cycle && rtc.CycleWeeks == 6 && rtc.CycleFirstEnd == new DateTime(2026, 12, 7) && rtc.CycleEndDay == DayOfWeek.Monday);
            Check("저장 왕복: 픽업", rt.Pickups.Count == 1 && rt.Pickups[0].Name == "반디");
            Check("JSON에 Cycle 글자로 저장", DataStore.Serialize(cdata).Contains("\"Cycle\""));

            // ===== 공유 (탭별 내보내기 / 추가로 가져오기) =====
            Console.WriteLine("[공유]");
            var snow = D(2026, 11, 25, 12);
            var sender = DefaultData.Create();
            var sNikke = sender.Games.Single(g => g.Name == "니케");
            var sLa = sender.Games.Single(g => g.Name == "로스트아크");
            sender.Pickups.Add(new PickupBanner { GameId = sNikke.Id, Name = "레드후드", StartDate = new DateTime(2026, 11, 27), EndDate = new DateTime(2026, 12, 10) });
            sender.Pickups.Add(new PickupBanner { GameId = sNikke.Id, Name = "끝난 픽업", StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 20) });
            var pkgP = ShareCodec.Parse(ShareCodec.Serialize(ShareCodec.ExportPickups(sender.Pickups, sender)))!;
            Check("픽업 파일 읽기", pkgP != null && pkgP.Type == ShareType.Pickups && pkgP.Count == 2 && pkgP.Pickups[0].Game == "니케");
            Check("백업 파일은 공유 파일 아님", ShareCodec.Parse(DataStore.Serialize(sender)) == null);
            Check("JSON 아님", ShareCodec.Parse("hello") == null);

            var recv = new AppData();
            var rNikke = new Game { Name = "승리의 여신: 니케", ResetTime = "04:00" };
            var rWw = new Game { Name = "명조 : 워더링 웨이브", ResetTime = "05:00" };
            var rLa = new Game { Name = "로스트 아크", Type = GameType.Rpg, ResetTime = "06:00" };
            rLa.Characters.Add(new Character { Name = "바드" });
            recv.Games.AddRange(new[] { rNikke, rWw, rLa });
            Check("자동 연결: 공백·기호 무시 (로스트아크 → 로스트 아크)", ShareCodec.MatchGame("로스트아크", recv) == rLa);
            Check("자동 연결: 앞부분 같은 게임 하나 (명조 → 명조 : 워더링 웨이브)", ShareCodec.MatchGame("명조", recv) == rWw);
            Check("자동 연결 못 함 (니케 ≠ 승리의 여신: 니케)", ShareCodec.MatchGame("니케", recv) == null);

            var itemsP = ShareCodec.Items(pkgP);
            Eq("게임 안 고르면 건너뜀", ImportState.NoGame, ShareCodec.State(itemsP[0], null, recv, snow));
            Eq("끝난 픽업", ImportState.Expired, ShareCodec.State(itemsP[1], rNikke, recv, snow));
            var map = new Dictionary<string, Game?> { ["니케"] = rNikke };   // 직접 연결
            var resP = ShareCodec.Apply(itemsP, map, recv, snow);
            Check("픽업 1개 추가, 끝난 1개 건너뜀", resP.Added == 1 && resP.Skipped == 1 && recv.Pickups.Single().GameId == rNikke.Id);
            Eq("같은 파일 다시 가져오면 이미 있음", ImportState.Duplicate, ShareCodec.State(itemsP[0], rNikke, recv, snow));
            Check("다시 가져와도 안 늘어남", ShareCodec.Apply(itemsP, map, recv, snow).Added == 0 && recv.Pickups.Count == 1);

            // 이벤트·목표
            sender.Events.Add(new EventQuest { GameId = sNikke.Id, Name = "할로윈", EndDate = new DateTime(2026, 12, 3), Done = true });
            sender.Events.Add(new EventQuest { GameId = sLa.Id, Name = "에스더 무기", NoDeadline = true });
            var pkgE = ShareCodec.Parse(ShareCodec.Serialize(ShareCodec.ExportEvents(sender.Events, sender)))!;
            var itemsE = ShareCodec.Items(pkgE);
            Eq("기한 이벤트를 RPG에 넣을 수 없음", ImportState.NotAllowed, ShareCodec.State(itemsE[0], rLa, recv, snow));
            var resE = ShareCodec.Apply(itemsE, new Dictionary<string, Game?> { ["니케"] = rNikke, ["로스트아크"] = rLa }, recv, snow);
            Check("이벤트 + 목표 추가", resE.Added == 2 && recv.Events.Count == 2);
            Check("완료 체크는 안 넘어옴", recv.Events.All(e => !e.Done));
            Check("목표는 무기한", recv.Events.Single(e => e.Name == "에스더 무기").NoDeadline);

            // 주기 컨텐츠
            var sHsr = sender.Games.Single(g => g.Name == "붕괴: 스타레일");
            var sc = new HomeworkTask { Name = "허구 이야기", Kind = ResetKind.Cycle, Scope = TaskScope.Common, CycleWeeks = 6, CycleFirstEnd = new DateTime(2026, 10, 26), CycleEndDay = DayOfWeek.Monday };
            sHsr.Tasks.Add(sc);
            var lc = new HomeworkTask { Name = "군단장", Kind = ResetKind.Cycle, Scope = TaskScope.PerCharacter, CycleWeeks = 4, CycleFirstEnd = new DateTime(2026, 12, 2), CycleEndDay = DayOfWeek.Wednesday };
            sLa.Tasks.Add(lc);
            var pkgC = ShareCodec.Parse(ShareCodec.Serialize(ShareCodec.ExportCycles(new[] { (sHsr, sc), (sLa, lc) }, snow)))!;
            Eq("내보낼 때 이번 회차 마감 (10/26 + 6주 = 12/7)", new DateTime(2026, 12, 7), pkgC.Cycles[0].CurrentEnd);
            var rHsr = new Game { Name = "붕괴 스타레일", ResetTime = "05:00" };
            recv.Games.Add(rHsr);
            // 3주 뒤에 가져옴 → 12/7 회차는 지났으니 다음 회차(1/18)로
            var later = D(2026, 12, 16, 12);
            var resC = ShareCodec.Apply(ShareCodec.Items(pkgC), new Dictionary<string, Game?> { ["붕괴: 스타레일"] = ShareCodec.MatchGame("붕괴: 스타레일", recv), ["로스트아크"] = rLa }, recv, later);
            Check("주기 2개 추가 (붕괴: 스타레일 → 붕괴 스타레일 자동 연결)", resC.Added == 2 && rHsr.Tasks.Count == 1);
            Eq("지난 회차는 넘겨서 다음 마감 1/18", new DateTime(2027, 1, 18), ResetCalculator.CycleCurrentEnd(rHsr.Tasks[0], rHsr, later));
            Check("받은 쪽 캐릭터 전체에 캐릭터마다", rLa.Tasks.Single().Scope == TaskScope.PerCharacter && rLa.Tasks.Single().CharacterIds.Count == 0);
            Check("가져온 주기는 미완료 (체크 기록 없음)", !recv.Completions.Keys.Any(k => k.StartsWith(rHsr.Tasks[0].Id.ToString())) && !ResetCalculator.IsDone(rHsr.Tasks[0], rHsr, null, later));
            Eq("주기 다시 가져오면 이미 있음", ImportState.Duplicate, ShareCodec.State(ShareCodec.Items(pkgC)[0], rHsr, recv, later));

            Console.WriteLine("[처음 실행 안내]");
            Check("새로 설치 → 안내 한 번", DefaultData.Create().WelcomeShown == false);
            var wdir = Path.Combine(Path.GetTempPath(), "gh-welcome-" + Guid.NewGuid());
            var wstore = new DataStore(wdir);
            Check("처음 실행 (파일 없음) → 안내 띄움", wstore.Load().WelcomeShown == false);
            var wd2 = wstore.Load(); wd2.WelcomeShown = true; wstore.Save(wd2);
            Check("보여 준 뒤엔 다시 안 띄움", wstore.Load().WelcomeShown == true);
            File.WriteAllText(wstore.FilePath, "{\"Games\":[]}");
            Check("예전부터 쓰던 데이터(값 없음) → 안 띄움", wstore.Load().WelcomeShown == true);
            Directory.Delete(wdir, true);

            Console.WriteLine();
            Console.WriteLine($"결과: {_pass} 통과, {_fail} 실패");
            return _fail == 0 ? 0 : 1;
        }
    }
}

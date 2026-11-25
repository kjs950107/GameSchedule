using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GameHomework.Core
{
    public enum GameType
    {
        Mobile,
        Rpg,
        /// <summary>숙제 없는 게임 (하스스톤, 롤 등) — 위젯에 안 나오고 플레이 시간만 기록</summary>
        PlayOnly
    }

    /// <summary>숙제 초기화 규칙 종류</summary>
    public enum ResetKind
    {
        /// <summary>매일 (게임 초기화 시각)</summary>
        Daily,
        /// <summary>매주 (게임의 주간 초기화 요일)</summary>
        Weekly,
        /// <summary>특정 요일마다 (예: 화·금)</summary>
        Weekdays,
        /// <summary>매월 N일</summary>
        Monthly,
        /// <summary>N주마다 (주기 컨텐츠: 첫 마감일을 정하고, 그 뒤로는 N주마다 같은 요일에 마감)</summary>
        Cycle
    }

    /// <summary>RPG 숙제 적용 범위</summary>
    public enum TaskScope
    {
        /// <summary>원정대(계정) 공통 — 체크 1개</summary>
        Common,
        /// <summary>캐릭터마다 따로 체크</summary>
        PerCharacter
    }

    public class AppData
    {
        public int Version { get; set; } = 1;
        public List<Game> Games { get; set; } = new();
        public List<EventQuest> Events { get; set; } = new();

        /// <summary>게임별 픽업 (기간만 표시, 체크 없음)</summary>
        public List<PickupBanner> Pickups { get; set; } = new();

        /// <summary>완료 기록. 키: "taskId" 또는 "taskId:charId", 값: 마지막으로 체크한 시각</summary>
        public Dictionary<string, DateTime> Completions { get; set; } = new();

        public WidgetSettings Widget { get; set; } = new();

        /// <summary>처음 실행 안내를 보여 줬는지. null = 이 기능 전부터 쓰던 데이터 (안내 안 함)</summary>
        public bool? WelcomeShown { get; set; }
    }

    public class Game
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "새 게임";
        public GameType Type { get; set; } = GameType.Mobile;

        /// <summary>게임 자체 초기화 시각 (HH:mm)</summary>
        public string ResetTime { get; set; } = "04:00";

        /// <summary>주간 숙제 초기화 요일</summary>
        public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Monday;

        public List<Character> Characters { get; set; } = new();
        public List<HomeworkTask> Tasks { get; set; } = new();

        /// <summary>플레이 시간 측정용 실행 파일 이름 (예: "LOSTARK.exe"). 여러 개 가능</summary>
        public List<string> ExeNames { get; set; } = new();

        public TimeSpan GetResetTimeOfDay()
        {
            return TimeParser.TryParse(ResetTime, out var t) ? t : new TimeSpan(4, 0, 0);
        }

        public override string ToString() => Name;
    }

    public class Character
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    public class HomeworkTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "새 숙제";
        public ResetKind Kind { get; set; } = ResetKind.Daily;

        /// <summary>Weekdays 규칙일 때 초기화되는 요일들</summary>
        public List<DayOfWeek> ResetDays { get; set; } = new();

        /// <summary>Monthly 규칙일 때 매월 며칠 (1~31, 해당 월에 없으면 말일)</summary>
        public int MonthDay { get; set; } = 1;

        /// <summary>이 요일(게임 기준 날짜)에는 위젯에 표시하지 않음</summary>
        public List<DayOfWeek> HiddenDays { get; set; } = new();

        /// <summary>RPG 전용: 공통 / 캐릭터별</summary>
        public TaskScope Scope { get; set; } = TaskScope.PerCharacter;

        /// <summary>캐릭터별 숙제의 적용 캐릭터. 비어 있으면 전체 캐릭터.</summary>
        public List<Guid> CharacterIds { get; set; } = new();

        /// <summary>캐릭터별 표시 이름 (예: 레이드 칸마다 캐릭터별로 다른 레이드 이름)</summary>
        public Dictionary<Guid, string> CharacterLabels { get; set; } = new();

        /// <summary>Cycle: 몇 주마다 (1~26)</summary>
        public int CycleWeeks { get; set; } = 4;

        /// <summary>Cycle: 이번(첫) 회차의 마지막 날 (게임 기준 날짜, 날짜만 사용)</summary>
        public DateTime? CycleFirstEnd { get; set; }

        /// <summary>Cycle: 첫 회차 시작 시각 (이 뒤로 체크한 것만 완료로 인정). null = 제한 없음</summary>
        public DateTime? CycleStart { get; set; }

        /// <summary>Cycle: 다음 회차부터 마감 요일 (그 요일이 마지막 날)</summary>
        public DayOfWeek CycleEndDay { get; set; } = DayOfWeek.Monday;

        public override string ToString() => Name;
    }

    public class EventQuest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GameId { get; set; }
        public string Name { get; set; } = "";

        /// <summary>마지막 날 (이 날짜까지 유효, 날짜만 사용). 무기한 목표면 사용 안 함</summary>
        public DateTime EndDate { get; set; }

        /// <summary>무기한 목표 (예: 메인스토리 깨기) — 기한 없이 완료할 때까지 표시</summary>
        public bool NoDeadline { get; set; }

        public bool Done { get; set; }

        /// <summary>완료 체크한 시각 (무기한 목표는 완료한 날까지만 흐리게 보이고 다음 날 사라짐)</summary>
        public DateTime? DoneAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public void SetDone(bool done, DateTime now)
        {
            Done = done;
            DoneAt = done ? now : null;
        }
    }

    /// <summary>게임 픽업 (가챠 배너) — 기간만 관리. 끝나면 자동 삭제</summary>
    public class PickupBanner
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GameId { get; set; }
        public string Name { get; set; } = "";
        /// <summary>시작일 (날짜만)</summary>
        public DateTime StartDate { get; set; }
        /// <summary>마지막 날 (날짜만, 이 날까지 진행)</summary>
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>위젯 종류 — 일일 / 주간(요일·월간 포함) / 주기 컨텐츠 / 이벤트·목표 / 픽업</summary>
    public enum WidgetKind
    {
        Daily,
        Weekly,
        Events,
        /// <summary>합쳐서 보기: 일일 + 주간 + 이벤트·목표를 위젯 하나에</summary>
        Combined,
        /// <summary>게임별 픽업 기간</summary>
        Pickup,
        /// <summary>주기 컨텐츠 (N주마다)</summary>
        Cycle
    }

    public class WidgetPlacement
    {
        /// <summary>null이면 아직 위치를 정하지 않음 (주 모니터 오른쪽 위에 나란히 배치)</summary>
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double Width { get; set; } = 340;
        public double Height { get; set; } = 600;
        public bool Visible { get; set; } = true;

        /// <summary>항상 다른 창 위에 표시 (끄면 바탕화면에 붙어 있음)</summary>
        public bool AlwaysOnTop { get; set; }

        /// <summary>색 테마: Dark / Light / Glass</summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>배경 이미지 (backgrounds 폴더 안 파일 이름, 또는 전체 경로). null = 없음</summary>
        public string? BackgroundImage { get; set; }

        /// <summary>배경 가림 정도 0~1 (테마 색을 이미지 위에 얼마나 덮을지). null = 테마 기본값</summary>
        public double? OverlayOpacity { get; set; }

        /// <summary>이미지 세로 위치: Top / Center / Bottom</summary>
        public string ImageAlign { get; set; } = "Center";

        [JsonIgnore]
        public double EffectiveOverlay =>
            Math.Clamp(OverlayOpacity ?? WidgetTheme.Get(Theme).DefaultOverlay(!string.IsNullOrEmpty(BackgroundImage)), 0, 1);
    }

    public class WidgetSettings
    {
        public double Opacity { get; set; } = 0.92;
        public bool LockPosition { get; set; }

        /// <summary>위젯 칸(창) 개수</summary>
        public const int SlotCount = 5;

        /// <summary>위젯에 넣을 수 있는 종류 (기본 순서)</summary>
        public static readonly WidgetKind[] AllKinds = { WidgetKind.Daily, WidgetKind.Weekly, WidgetKind.Cycle, WidgetKind.Events, WidgetKind.Pickup };

        /// <summary>
        /// 위젯 배치: 칸 5개, 칸마다 들어 있는 종류 (왼쪽부터 열 순서).
        /// 한 칸에 여러 개 = 넓은 위젯 하나에 나란히. 빈 칸 = 창 없음.
        /// </summary>
        public List<List<WidgetKind>>? Slots { get; set; }

        // ---- 예전 "합쳐서 보기" 설정 (v1.10 이하) — 처음 한 번 Slots로 옮길 때만 씀 ----
        public bool CombinedMode { get; set; }
        public List<WidgetKind>? CombinedKinds { get; set; }

        /// <summary>체크한 숙제/끝낸 게임을 아래로, 남은 것을 위로 정렬</summary>
        public bool SortDoneToBottom { get; set; } = true;

        /// <summary>위치/크기/표시 여부/테마 (키: "Slot0"~"Slot4", 예전 키: WidgetKind 이름)</summary>
        public Dictionary<string, WidgetPlacement> Placements { get; set; } = new();

        public static string SlotKey(int slot) => "Slot" + slot;

        public WidgetPlacement Get(string key)
        {
            if (!Placements.TryGetValue(key, out var p) || p == null)
            {
                p = new WidgetPlacement();
                Placements[key] = p;
            }
            return p;
        }

        /// <summary>칸(창)의 위치·테마 등</summary>
        public WidgetPlacement GetSlot(int slot)
        {
            EnsureSlots(); // 처음이면 예전 설정(위치·테마)을 칸으로 옮긴 뒤
            return Get(SlotKey(slot));
        }

        /// <summary>예전 방식(종류별) 설정 — 옮기기용</summary>
        public WidgetPlacement Get(WidgetKind kind) => Get(kind.ToString());

        [JsonIgnore]
        public List<List<WidgetKind>> SlotList { get { EnsureSlots(); return Slots!; } }

        public List<WidgetKind> KindsIn(int slot) =>
            slot >= 0 && slot < SlotCount ? SlotList[slot] : new List<WidgetKind>();

        /// <summary>뭔가 들어 있는 칸 (= 창이 있는 칸)</summary>
        [JsonIgnore]
        public List<int> ActiveSlots => Enumerable.Range(0, SlotCount).Where(i => SlotList[i].Count > 0).ToList();

        public int SlotOf(WidgetKind kind) => SlotList.FindIndex(s => s.Contains(kind));

        /// <summary>
        /// 칸 정리: 5칸, 종류마다 정확히 한 번. 처음이면 예전 "합쳐서 보기" 설정에서 옮겨 온다.
        /// </summary>
        public void EnsureSlots()
        {
            if (Slots == null || Slots.Count == 0) MigrateFromCombined();
            var slots = Slots!;
            for (int i = 0; i < slots.Count; i++) slots[i] ??= new List<WidgetKind>();
            var extra = slots.Skip(SlotCount).SelectMany(x => x).ToList();
            if (slots.Count > SlotCount) slots.RemoveRange(SlotCount, slots.Count - SlotCount);
            while (slots.Count < SlotCount) slots.Add(new List<WidgetKind>());
            if (extra.Count > 0) slots[SlotCount - 1].AddRange(extra);

            var seen = new HashSet<WidgetKind>();
            foreach (var s in slots) s.RemoveAll(k => !AllKinds.Contains(k) || !seen.Add(k));
            foreach (var k in AllKinds.Where(k => !seen.Contains(k)))
            {
                var target = slots.FirstOrDefault(s => s.Count == 0) ?? slots[SlotCount - 1];
                target.Add(k);
            }
        }

        private void MigrateFromCombined()
        {
            var slots = new List<List<WidgetKind>>();
            // 합칠 칸 정보가 없던 더 예전 버전은 일일·주간·이벤트 셋 다
            var ck = CombinedKinds ?? new List<WidgetKind> { WidgetKind.Daily, WidgetKind.Weekly, WidgetKind.Events };
            var combined = CombinedMode ? AllKinds.Where(ck.Contains).ToList() : new List<WidgetKind>();
            if (combined.Count >= 2)
            {
                slots.Add(combined);
                if (Placements.TryGetValue("Combined", out var cp) && cp != null) Placements[SlotKey(0)] = cp;
            }
            else combined.Clear();
            foreach (var k in AllKinds.Where(k => !combined.Contains(k)))
            {
                if (Placements.TryGetValue(k.ToString(), out var kp) && kp != null) Placements[SlotKey(slots.Count)] = kp;
                slots.Add(new List<WidgetKind> { k });
            }
            Slots = slots;
        }

        /// <summary>
        /// 종류를 다른 칸으로 옮긴다 (index: 그 칸 안에서 몇 번째 열로, -1이면 맨 뒤).
        /// 빈 칸으로 옮기면 그 칸 창이 새로 생긴다.
        /// </summary>
        public void Move(WidgetKind kind, int toSlot, int index = -1)
        {
            if (toSlot < 0 || toSlot >= SlotCount) return;
            var slots = SlotList;
            int from = SlotOf(kind);
            int fromIndex = from >= 0 ? slots[from].IndexOf(kind) : -1;
            bool targetWasEmpty = slots[toSlot].Count == 0;
            if (from >= 0) slots[from].Remove(kind);
            var target = slots[toSlot];
            // 같은 칸 안에서 뒤로 옮길 때, 빠진 자리만큼 당김
            if (from == toSlot && index > fromIndex) index--;
            if (index < 0 || index > target.Count) index = target.Count;
            target.Insert(index, kind);
            if (targetWasEmpty) GetSlot(toSlot).Visible = true;
        }

        /// <summary>처음 상태: 칸마다 하나씩</summary>
        public void ResetSlots()
        {
            Slots = AllKinds.Select(k => new List<WidgetKind> { k }).ToList();
            for (int i = 0; i < SlotCount; i++) GetSlot(i).Visible = true;
        }

        /// <summary>"일일 · 주간"</summary>
        public static string ShortName(WidgetKind k) => k switch
        {
            WidgetKind.Daily => "일일",
            WidgetKind.Weekly => "주간",
            WidgetKind.Cycle => "주기",
            WidgetKind.Pickup => "픽업",
            _ => "이벤트"
        };

        public string SlotLabel(int slot)
        {
            var kinds = KindsIn(slot);
            return $"위젯 {slot + 1}" + (kinds.Count == 0 ? " (비어 있음)" : " — " + string.Join(" · ", kinds.Select(ShortName)));
        }
    }

    public static class TimeParser
    {
        public static bool TryParse(string? text, out TimeSpan value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim().Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            value = new TimeSpan(h, m, 0);
            return true;
        }
    }
}

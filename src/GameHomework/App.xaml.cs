using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using GameHomework.Core;
using GameHomework.Native;
using Microsoft.Win32;

namespace GameHomework
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private TrayIcon? _tray;
        private DispatcherTimer? _purgeTimer;
        private static PlayMonitor? _playMonitor;

        /// <summary>앱 버전 (예: "1.7.0")</summary>
        public static string Version
        {
            get
            {
                var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        /// <summary>오늘 요약 (기록 + 지금 켜져 있는 게임)</summary>
        public static TodaySummary BuildToday()
        {
            var now = DateTime.Now;
            List<LogRecord> records;
            try { records = AppState.Log.ReadAll(now.AddDays(-3)); }
            catch (Exception ex) { LogError("오늘 요약: 기록 읽기 실패", ex); records = new List<LogRecord>(); }
            var open = _playMonitor?.OpenSessions ?? new List<PlaySession>();
            return TodaySummary.Build(records, open, AppState.Data, now);
        }

        /// <summary>위젯 칸 5개 (칸 번호 → 창). 빈 칸의 창은 숨겨 둔다</summary>
        public static Dictionary<int, WidgetWindow> Widgets { get; } = new();
        private static SettingsWindow? _settings;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "GameHomework_SingleInstance_v1", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("게임 숙제 관리가 이미 실행 중입니다.\n작업표시줄 오른쪽 트레이 아이콘을 확인해 주세요.",
                    "게임 숙제 관리", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex) LogError("처리되지 않은 오류", ex);
            };

            base.OnStartup(e);

            AppState.Load();

            for (int slot = 0; slot < WidgetSettings.SlotCount; slot++)
            {
                var w = new WidgetWindow(slot);
                Widgets[slot] = w;
                if (ShouldShow(slot)) w.Show();
            }

            _tray = new TrayIcon();

            // 끝난 이벤트 / 완료 후 초기화가 지난 무기한 목표를 1분마다 삭제
            _purgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _purgeTimer.Tick += (s, ev) =>
            {
                if (DataStore.PurgeExpired(AppState.Data, DateTime.Now)) AppState.Save();
            };
            _purgeTimer.Start();

            // ---- 통계 기록 ----
            // 앱 시작 = (자동 실행이면) PC를 켠 시각
            AppState.LogSafe(new LogRecord { Type = LogType.Start, T = DateTime.Now });
            _playMonitor = new PlayMonitor();
            _playMonitor.Start();
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SessionEnding += (s, ev) => _playMonitor?.CloseAll(); // 윈도우 종료/로그오프
        }

        private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Mode == PowerModes.Suspend)
                {
                    _playMonitor?.CloseAll();
                }
                else if (e.Mode == PowerModes.Resume)
                {
                    AppState.LogSafe(new LogRecord { Type = LogType.Resume, T = DateTime.Now });
                }
            }));
        }

        /// <param name="preset">이벤트 탭: "event"(기한 있음) / "goal"(무기한 목표)</param>
        public static void OpenSettings(int tabIndex = 0, string? preset = null)
        {
            if (_settings == null)
            {
                _settings = new SettingsWindow();
                _settings.Closed += (s, e) => _settings = null;
                _settings.Show();
            }
            _settings.SelectTab(tabIndex);
            if (preset != null) _settings.PresetEventType(preset == "goal");
            if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
            _settings.Activate();
        }

        /// <summary>주기 컨텐츠 등록 창 (위젯 ＋ / 트레이에서)</summary>
        public static void OpenCycleWindow(Game? game = null)
        {
            try
            {
                var w = new CycleWindow(game, null);
                if (_settings != null && _settings.IsVisible) w.Owner = _settings;
                w.Loaded += (s, e) => w.Activate();
                if (w.ShowDialog() == true) _settings?.ReloadTasks(); // 설정 창이 열려 있으면 주기 컨텐츠 목록 갱신
            }
            catch (Exception ex)
            {
                LogError("주기 컨텐츠 창", ex);
            }
        }

        /// <summary>뭔가 들어 있는 칸이고, 켜 둔 상태인지</summary>
        private static bool ShouldShow(int slot) =>
            AppState.Data.Widget.KindsIn(slot).Count > 0 && AppState.Data.Widget.GetSlot(slot).Visible;

        /// <summary>설정에서 칸 배치를 바꾼 뒤: 빈 칸 창은 숨기고, 새로 생긴 칸 창은 띄우고, 열을 다시 그림</summary>
        public static void RefreshWidgetModes()
        {
            foreach (var pair in Widgets)
            {
                if (ShouldShow(pair.Key))
                {
                    if (!pair.Value.IsVisible) pair.Value.ShowPinned();
                    pair.Value.Relayout();
                }
                else if (pair.Value.IsVisible)
                {
                    pair.Value.SavePlacement();
                    pair.Value.Hide();
                }
            }
        }

        /// <summary>종류를 다른 칸으로 옮기기 (설정 → 위젯 탭에서 끌어다 놓기)</summary>
        public static void MoveWidgetKind(WidgetKind kind, int toSlot, int index = -1)
        {
            AppState.Data.Widget.Move(kind, toSlot, index);
            AppState.SaveSilently();
            RefreshWidgetModes();
        }

        /// <summary>처음 상태 (칸마다 하나씩)</summary>
        public static void ResetWidgetSlots()
        {
            AppState.Data.Widget.ResetSlots();
            AppState.SaveSilently();
            RefreshWidgetModes();
        }

        public static void SetWidgetVisible(int slot, bool visible)
        {
            if (!Widgets.TryGetValue(slot, out var w)) return;
            if (AppState.Data.Widget.KindsIn(slot).Count == 0) visible = false;
            if (visible) w.ShowPinned();
            else w.Hide();
            AppState.Data.Widget.GetSlot(slot).Visible = visible;
            AppState.SaveSilently();
        }

        /// <summary>트레이 아이콘 클릭: 켜져 있는 위젯을 모두 잠깐 맨 앞으로 (하나도 없으면 전부 켬)</summary>
        public static void RevealWidgets()
        {
            var active = AppState.Data.Widget.ActiveSlots;
            var shown = active.Where(i => Widgets.ContainsKey(i) && Widgets[i].IsVisible).Select(i => Widgets[i]).ToList();
            if (shown.Count == 0)
            {
                foreach (var i in active) SetWidgetVisible(i, true);
                shown = active.Where(Widgets.ContainsKey).Select(i => Widgets[i]).ToList();
            }
            foreach (var w in shown) w.Reveal();
        }

        public static void ResetWidgetPositions()
        {
            foreach (var i in AppState.Data.Widget.ActiveSlots)
            {
                if (!Widgets.ContainsKey(i)) continue;
                SetWidgetVisible(i, true);
                Widgets[i].ResetPosition();
            }
        }

        public static void ExitApp()
        {
            _playMonitor?.Dispose();
            _playMonitor = null;
            foreach (var w in Widgets.Values) w.SavePlacement();
            if (Current is App app) app._tray?.Dispose();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _playMonitor?.Dispose();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _tray?.Dispose();
            try { _mutex?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError("오류", e.Exception);
            MessageBox.Show("오류가 발생했습니다.\n" + e.Exception.Message + "\n\n자세한 내용: " + LogPath,
                "게임 숙제 관리", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        public static string LogPath => Path.Combine(AppState.Store.FolderPath, "error.log");

        public static void LogError(string title, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppState.Store.FolderPath);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{ex}\n\n");
            }
            catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameHomework.Core;
using GameHomework.Native;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GameHomework
{
    public partial class WidgetWindow : Window
    {
        // ---- 색상 (테마에 따라 ApplyTheme에서 바뀜) ----
        private Brush TextMain = Hex("#E6E8EC");
        private Brush TextMuted = Hex("#9AA0AA");
        private Brush TextDim = Hex("#6B717B");
        private Brush CardBg = Hex("#10FFFFFF");
        private Brush CardBorder = Hex("#16FFFFFF");
        private Brush Accent = Hex("#8FA6FF");
        private Brush WarnBorder = Hex("#E8625C");
        private Brush WarnBg = Hex("#16FF5A5A");
        private Brush CriticalBorder = Hex("#FF4040");
        private Brush CriticalBg = Hex("#2EFF4040");
        private Brush WarnText = Hex("#FF8F87");
        private Brush EventBorder = Hex("#26FFFFFF");
        private Brush MissedBorder = Hex("#33B07A7A");
        private Brush MissedBg = Hex("#0DFFFFFF");
        private Brush MissedText = Hex("#C9A3A3");
        private Brush ButtonBg = Hex("#1AFFFFFF");
        private Brush ButtonHoverBg = Hex("#33FFFFFF");
        private Brush DueBorder = Hex("#F59F00");
        private Brush DueBg = Hex("#1FF59F00");
        private Brush DueText = Hex("#FFC078");
        private WidgetTheme _theme = WidgetTheme.Dark;
        private string? _imageKey;

        private readonly DesktopPinner _pinner;
        private readonly DispatcherTimer _tick;
        private readonly DispatcherTimer _placementTimer;
        /// <summary>사용자가 헤더를 눌러 접기/펼치기를 바꾼 게임 (키: 게임ID + 완료여부)</summary>
        private readonly HashSet<string> _toggled = new();
        private bool _ready;
        private bool _renderQueued;

        /// <summary>위젯 칸 번호 (0~4). 칸에 든 종류(일일·주간·…)를 나란히 보여 준다</summary>
        public int Slot { get; }

        /// <summary>이 칸에 들어 있는 종류 (왼쪽부터)</summary>
        private List<WidgetKind> Kinds => AppState.Data.Widget.KindsIn(Slot);

        /// <summary>지금 그려진 열 개수 (합치거나 뺄 때 너비 조절용)</summary>
        private int _layoutCount;

        /// <summary>끝낸 것은 아래로 정렬할지 (설정 → 위젯 탭)</summary>
        private static bool SortDone => AppState.Data.Widget.SortDoneToBottom;

        private WidgetPlacement Placement => AppState.Data.Widget.GetSlot(Slot);

        public static string KindTitle(WidgetKind kind) => kind switch
        {
            WidgetKind.Daily => "일일 숙제",
            WidgetKind.Weekly => "주간 숙제",
            WidgetKind.Events => "이벤트 · 목표",
            WidgetKind.Pickup => "픽업",
            WidgetKind.Cycle => "주기 컨텐츠",
            _ => "게임 숙제"
        };

        /// <summary>＋ 버튼: 주기 컨텐츠 · 이벤트 · 픽업이 들어 있거나 여러 개를 합친 위젯</summary>
        private bool HasAddButton => Kinds.Count > 1 || Kinds.Any(k => k is WidgetKind.Cycle or WidgetKind.Events or WidgetKind.Pickup);

        /// <summary>제목: "일일 숙제" / "일일 · 주간 숙제" / "일일 · 주간 · 이벤트"</summary>
        public static string SlotTitle(List<WidgetKind> kinds) =>
            kinds.Count == 0 ? "게임 숙제"
            : kinds.Count == 1 ? KindTitle(kinds[0])
            : string.Join(" · ", kinds.Select(WidgetSettings.ShortName)) +
              (kinds.All(k => k is WidgetKind.Daily or WidgetKind.Weekly) ? " 숙제" : "");

        public WidgetWindow(int slot)
        {
            Slot = slot;
            InitializeComponent();
            _pinner = new DesktopPinner(this);

            UpdateTitle();
            BtnAddEvent.Visibility = HasAddButton ? Visibility.Visible : Visibility.Collapsed;

            // 최소화되면(바탕화면 보기 등) 바로 원래대로 되돌린다 — 위젯은 항상 바탕화면에 있어야 함
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                    _pinner.SendToBottom();
                }
            };

            ApplyPlacement();
            ApplySettings();

            AppState.Changed += QueueRender;

            _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _tick.Tick += (s, e) => Render();
            _tick.Start();

            _placementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _placementTimer.Tick += (s, e) => { _placementTimer.Stop(); SavePlacement(); };
            LocationChanged += (s, e) => SchedulePlacementSave();
            SizeChanged += (s, e) => SchedulePlacementSave();

            Loaded += (s, e) =>
            {
                EnsureOnScreen();
                _ready = true;
                Render();
            };

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            Closed += (s, e) =>
            {
                AppState.Changed -= QueueRender;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _tick.Stop();
            };
        }

        // ================= 창 위치 / 설정 =================

        private void ApplyPlacement()
        {
            var w = Placement;
            int n = Kinds.Count;
            MinWidth = n > 1 ? 220 * n : 260;
            // 처음 만드는 합친 위젯은 열이 다 들어가게 넓게
            if (n > 1 && w.Left == null && Math.Abs(w.Width - 340) < 1)
            {
                w.Width = 345 * n + 10;
                w.Height = 640;
            }
            _layoutCount = n;
            Width = Math.Max(MinWidth, w.Width);
            Height = Math.Max(MinHeight, w.Height);
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (w.Left is double l && w.Top is double t && !double.IsNaN(l) && !double.IsNaN(t))
            {
                Left = l;
                Top = t;
            }
            else
            {
                PlaceDefault();
            }
        }

        /// <summary>주 모니터 오른쪽 위에 칸 순서대로 (1번 칸이 맨 오른쪽) 나란히</summary>
        private void PlaceDefault()
        {
            var wa = SystemParameters.WorkArea; // 주 모니터
            const double gap = 12;
            var ws = AppState.Data.Widget;
            double offset = 0; // 내 오른쪽에 놓일 창들 너비
            foreach (var i in ws.ActiveSlots.Where(i => i < Slot))
                offset += ws.GetSlot(i).Width + gap;
            Left = Math.Max(wa.Left, wa.Right - 24 - offset - Width);
            Top = wa.Top + 24;
            if (Height > wa.Height - 48) Height = Math.Max(MinHeight, wa.Height - 48);
        }

        /// <summary>모니터가 빠졌거나 위치가 화면 밖이면 주 모니터로 옮긴다</summary>
        public void EnsureOnScreen()
        {
            var h = _pinner.Handle;
            if (h == IntPtr.Zero) return;
            if (!DesktopPinner.GetWindowRect(h, out var r)) return;
            var win = new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            bool visible = Forms.Screen.AllScreens.Any(s =>
            {
                var i = System.Drawing.Rectangle.Intersect(s.WorkingArea, win);
                return i.Width >= 80 && i.Height >= 40;
            });
            if (!visible)
            {
                PlaceDefault();
                SavePlacement();
            }
        }

        public void ResetPosition()
        {
            PlaceDefault();
            SavePlacement();
        }

        private void ApplySettings()
        {
            var w = AppState.Data.Widget;
            Opacity = Math.Clamp(w.Opacity, 0.3, 1.0);
            ResizeMode = w.LockPosition ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
            _pinner.SetTopmost(Placement.AlwaysOnTop);
            ChkPin.IsChecked = Placement.AlwaysOnTop;
            BtnAddEvent.Visibility = HasAddButton ? Visibility.Visible : Visibility.Collapsed;
            ApplyTheme();
        }

        // ================= 테마 / 배경 이미지 =================

        private void ApplyTheme()
        {
            var pl = Placement;
            var t = WidgetTheme.Get(pl.Theme);
            _theme = t;

            TextMain = Hex(t.Text);
            TextMuted = Hex(t.Muted);
            TextDim = Hex(t.Dim);
            CardBg = Hex(t.CardBg);
            CardBorder = Hex(t.CardBorder);
            Accent = Hex(t.Accent);
            WarnBorder = Hex(t.WarnBorder);
            WarnBg = Hex(t.WarnBg);
            CriticalBorder = Hex(t.CriticalBorder);
            CriticalBg = Hex(t.CriticalBg);
            WarnText = Hex(t.WarnText);
            EventBorder = Hex(t.EventBorder);
            MissedBorder = Hex(t.MissedBorder);
            MissedBg = Hex(t.MissedBg);
            MissedText = Hex(t.MissedText);
            ButtonBg = Hex(t.ButtonBg);
            ButtonHoverBg = Hex(t.ButtonHoverBg);
            DueBorder = Hex(t.DueBorder);
            DueBg = Hex(t.DueBg);
            DueText = Hex(t.DueText);

            Resources["ThemeTitle"] = Hex(t.Title);
            Resources["ThemeText"] = Hex(t.Text);
            Resources["ThemeMuted"] = Hex(t.Muted);
            Resources["ThemeIcon"] = Hex(t.Icon);
            Resources["ThemeIconHover"] = Hex(t.IconHover);
            Resources["ThemeIconHoverBg"] = Hex(t.IconHoverBg);
            Resources["ThemeCheckBorder"] = Hex(t.CheckBoxBorder);
            Resources["ThemeCheckBg"] = Hex(t.CheckBoxBg);
            Resources["ThemeCheckFill"] = Hex(t.CheckFill);
            Resources["ThemeCheckHover"] = Hex(t.CheckHover);
            Resources["ThemeScroll"] = Hex(t.Scroll);
            Resources["ThemeWindowBorder"] = Hex(t.WindowBorder);

            // 이미지 위에 테마 색을 "가림 정도"만큼 덮는다
            var (_, r, g, b) = WidgetTheme.Parse(t.Overlay);
            var overlay = new SolidColorBrush(Color.FromArgb((byte)Math.Round(pl.EffectiveOverlay * 255), r, g, b));
            overlay.Freeze();
            Root.Background = overlay;

            // 글래스: 글씨 그림자로 화려한 이미지 위에서도 읽히게
            ContentRoot.Effect = t.TextShadow
                ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Direction = 270, Opacity = 0.9 }
                : null;

            ApplyBackgroundImage(pl);
        }

        private void ApplyBackgroundImage(WidgetPlacement pl)
        {
            var path = BackgroundImages.Resolve(pl.BackgroundImage);
            var key = (path ?? "") + "|" + pl.ImageAlign;
            if (key == _imageKey) return; // 같은 이미지면 다시 읽지 않음
            _imageKey = key;

            if (path == null || !System.IO.File.Exists(path))
            {
                BgImage.Background = null;
                return;
            }
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;        // 파일을 잡고 있지 않도록
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.DecodePixelWidth = 1000;                       // 메모리 절약 (위젯 크기면 충분)
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                BgImage.Background = new ImageBrush(bmp)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = pl.ImageAlign switch
                    {
                        "Top" => AlignmentY.Top,
                        "Bottom" => AlignmentY.Bottom,
                        _ => AlignmentY.Center
                    }
                };
            }
            catch (Exception ex)
            {
                App.LogError("배경 이미지를 불러오지 못함: " + path, ex);
                BgImage.Background = null;
            }
        }

        private void SchedulePlacementSave()
        {
            if (!_ready) return;
            _placementTimer.Stop();
            _placementTimer.Start();
        }

        public void SavePlacement()
        {
            if (!_ready || WindowState != WindowState.Normal) return;
            var w = Placement;
            w.Left = Left;
            w.Top = Top;
            w.Width = Width;
            w.Height = Height;
            AppState.SaveSilently();
        }

        public void ShowPinned()
        {
            Show();
            _pinner.AttachToDesktop();
            Render();
        }

        /// <summary>트레이에서 "찾기" — 잠깐 맨 앞으로 보여준다</summary>
        public void Reveal()
        {
            if (!IsVisible) ShowPinned();
            _pinner.Peek();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
            Dispatcher.BeginInvoke(new Action(EnsureOnScreen), DispatcherPriority.Background);

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
                Dispatcher.BeginInvoke(new Action(Render), DispatcherPriority.Background);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AppState.Data.Widget.LockPosition || e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => App.OpenSettings(0);

        private void UpdateTitle()
        {
            var t = SlotTitle(Kinds);
            Title = t;          // 작업표시줄에 보이는 이름
            TxtTitle.Text = t;
        }

        /// <summary>
        /// 칸 구성이 바뀌었을 때 (설정에서 끌어서 합치거나 뺌): 제목 · 열 · 너비를 다시 맞춘다.
        /// 열이 늘면 한 열만큼 넓히고, 줄면 좁힌다.
        /// </summary>
        public void Relayout()
        {
            int n = Kinds.Count;
            if (n == 0) return;
            UpdateTitle();
            MinWidth = n > 1 ? 220 * n : 260;
            if (_layoutCount > 0 && n != _layoutCount)
            {
                double maxW = SystemParameters.VirtualScreenWidth - 40;
                Width = Math.Clamp(Width + (n - _layoutCount) * 345, MinWidth, Math.Max(MinWidth, maxW));
                _layoutCount = n;
                SavePlacement();
                EnsureOnScreen();
            }
            _layoutCount = n;
            BtnAddEvent.Visibility = HasAddButton ? Visibility.Visible : Visibility.Collapsed;
            Render();
        }
        private void ChkPin_Click(object sender, RoutedEventArgs e)
        {
            Placement.AlwaysOnTop = ChkPin.IsChecked == true;
            AppState.Save();
        }

        /// <summary>＋ : 무엇을 추가할지 고르는 메뉴</summary>
        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            var kinds = Kinds;
            bool Has(WidgetKind k) => kinds.Contains(k);
            bool events = Has(WidgetKind.Events);
            bool cycle = Has(WidgetKind.Cycle);
            bool pickup = Has(WidgetKind.Pickup);
            bool tasks = Has(WidgetKind.Weekly) || Has(WidgetKind.Daily);

            var menu = new ContextMenu { PlacementTarget = BtnAddEvent, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            void Add(string text, Action action)
            {
                var item = new MenuItem { Header = text };
                item.Click += (s, ev) => action();
                menu.Items.Add(item);
            }
            if (events)
            {
                Add("이벤트 등록 (기한 있음)", () => App.OpenSettings(1, "event"));
                Add("무기한 목표 등록", () => App.OpenSettings(1, "goal"));
            }
            if (pickup)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                Add("픽업 등록", () => App.OpenSettings(SettingsWindow.PickupTabIndex));
            }
            if (cycle)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                Add("주기 컨텐츠 등록 (N주마다)", () => App.OpenCycleWindow());
            }
            if (tasks)
            {
                if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                Add("숙제 추가 / 수정 (게임 · 숙제 설정)", () => App.OpenSettings(0));
            }
            if (menu.Items.Count == 1 && menu.Items[0] is MenuItem only)
            {
                only.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); // 하나뿐이면 바로 실행
                return;
            }
            menu.IsOpen = true;
        }

        // ================= 그리기 =================

        private void QueueRender()
        {
            if (_renderQueued) return;
            _renderQueued = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _renderQueued = false;
                ApplySettings();
                Render();
            }), DispatcherPriority.Background);
        }

        /// <summary>합친 위젯의 칸 3개 (일일 | 주간 | 이벤트)</summary>
        private readonly List<(WidgetKind Kind, ScrollViewer Scroll, StackPanel Panel, TextBlock Summary, Border Divider)> _columns = new();
        /// <summary>지금 그리고 있는 칸 (접기 상태를 칸마다 따로 기억하려고)</summary>
        private WidgetKind _renderKind;

        /// <summary>합친 위젯: 기존 스크롤 영역 자리에 3칸을 나란히 만든다</summary>
        private Grid? _columnsHost;
        private string _columnsKey = "";


        private void BuildCombinedColumns(List<WidgetKind> kinds)
        {
            var dock = (Scroller.Parent as DockPanel) ?? (_columnsHost?.Parent as DockPanel);
            if (dock == null) return;
            if (Scroller.Parent == dock) dock.Children.Remove(Scroller);
            if (_columnsHost != null) dock.Children.Remove(_columnsHost);
            _columns.Clear();
            _columnsKey = string.Join(",", kinds);

            var grid = new Grid { Margin = new Thickness(8, 0, 4, 14) };
            _columnsHost = grid;
            for (int i = 0; i < kinds.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var col = new DockPanel { Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0) };
                var head = new StackPanel { Margin = new Thickness(4, 0, 4, 6) };
                var title = new TextBlock { Text = KindTitle(kinds[i]), FontSize = 13, FontWeight = FontWeights.Bold };
                title.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTitle");
                var summary = new TextBlock { FontSize = 11, Margin = new Thickness(0, 1, 0, 0) };
                summary.SetResourceReference(TextBlock.ForegroundProperty, "ThemeMuted");
                head.Children.Add(title);
                head.Children.Add(summary);
                DockPanel.SetDock(head, Dock.Top);
                col.Children.Add(head);
                var panel = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
                var scroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Focusable = false,
                    Content = panel
                };
                col.Children.Add(scroll);
                // 칸 사이 구분선
                var divider = new Border { BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0), Padding = new Thickness(i == 0 ? 0 : 6, 0, 0, 0), Child = col };
                Grid.SetColumn(divider, i);
                grid.Children.Add(divider);
                _columns.Add((kinds[i], scroll, panel, summary, divider));
            }
            dock.Children.Add(grid);
        }

        /// <summary>열 하나짜리로 되돌리기 (합쳤던 칸에서 하나만 남았을 때)</summary>
        private void EnsureSingleLayout()
        {
            if (_columnsHost == null) return;
            if (_columnsHost.Parent is DockPanel dock)
            {
                dock.Children.Remove(_columnsHost);
                if (Scroller.Parent == null) dock.Children.Add(Scroller);
            }
            _columnsHost = null;
            _columns.Clear();
            _columnsKey = "";
        }

        public void Render()
        {
            var now = DateTime.Now;
            int remaining;
            var kinds = Kinds.ToList();
            if (kinds.Count == 0) return;
            if (kinds.Count > 1)
            {
                if (_columns.Count == 0 || _columnsKey != string.Join(",", kinds)) BuildCombinedColumns(kinds);
                remaining = 0;
                foreach (var c in _columns)
                {
                    c.Divider.BorderBrush = CardBorder;
                    int r = RenderColumn(c.Kind, c.Panel, c.Scroll, now);
                    c.Summary.Text = c.Kind == WidgetKind.Pickup ? PickupSummary(now)
                                   : r == 0 ? DoneText(c.Kind) : $"남은 {r}개";
                    remaining += r;
                }
            }
            else
            {
                EnsureSingleLayout();
                remaining = RenderColumn(kinds[0], CardsPanel, Scroller, now);
            }
            var only = kinds.Count == 1 ? kinds[0] : WidgetKind.Combined;
            TxtSummary.Text = $"{now:M'/'d} ({ResetCalculator.DayName(now.DayOfWeek)}) · " +
                              (only == WidgetKind.Pickup ? PickupSummary(now)
                               : remaining == 0 ? DoneText(only) : $"남은 {remaining}개");
        }

        private static string DoneText(WidgetKind kind) => kind switch
        {
            WidgetKind.Daily => "오늘 숙제 끝!",
            WidgetKind.Weekly => "이번 주 숙제 끝!",
            WidgetKind.Events => "남은 이벤트 · 목표 없음",
            WidgetKind.Pickup => "등록된 픽업 없음",
            WidgetKind.Cycle => "이번 회차 다 끝!",
            _ => "남은 숙제 없음!"
        };

        /// <summary>"진행 3 · 예정 1"</summary>
        private static string PickupSummary(DateTime now)
        {
            var list = PickupCalc.ForWidget(AppState.Data, now);
            int a = list.Sum(g => g.Active.Count), u = list.Sum(g => g.Upcoming.Count);
            if (a + u == 0) return DoneText(WidgetKind.Pickup);
            return u == 0 ? $"진행 {a}" : $"진행 {a} · 예정 {u}";
        }

        /// <summary>한 칸(일일/주간/이벤트) 그리기. 남은 개수를 돌려준다</summary>
        private int RenderColumn(WidgetKind kind, StackPanel panel, ScrollViewer scroll, DateTime now)
        {
            _renderKind = kind;
            double offset = scroll.VerticalOffset;
            panel.Children.Clear();

            if (kind == WidgetKind.Pickup)
            {
                RenderPickups(panel, now);
                Dispatcher.BeginInvoke(new Action(() => scroll.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
                return 0;
            }

            int remaining = 0;
            var progresses = AppState.Data.Games
                .Select(game => ProgressBuilder.Build(game, AppState.Data, now, kind))
                .Where(p => p.HasContent);
            foreach (var p in WidgetOrdering.Games(progresses, SortDone))
            {
                remaining += p.Remaining;
                panel.Children.Add(BuildCard(p, now));
            }

            if (panel.Children.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = kind == WidgetKind.Events
                        ? "＋ 버튼으로 이벤트(기한)나 무기한 목표를 등록하세요."
                        : kind == WidgetKind.Cycle
                        ? "＋ 버튼으로 몇 주마다 돌아오는 컨텐츠(4주·6주 등)를 등록하세요."
                        : "설정(⚙)에서 게임과 숙제를 추가하세요.",
                    Foreground = TextMuted,
                    Margin = new Thickness(6, 8, 6, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            Dispatcher.BeginInvoke(new Action(() => scroll.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
            return remaining;
        }

        private UIElement BuildCard(GameProgress p, DateTime now)
        {
            // 놓친 이벤트가 있으면 확인할 수 있게 접지 않는다
            bool complete = p.IsComplete && p.Missed.Count == 0;
            // 접기 상태는 칸(일일/주간/이벤트)마다 따로
            string baseKey = _renderKind + ":" + p.Game.Id;
            string toggleKey = baseKey + (complete ? ":done" : ":open");
            // 완료 ↔ 미완료가 바뀌면 이전 상태에서 누른 접기/펼치기는 버린다 (초기화되면 다시 펼쳐지도록)
            _toggled.Remove(baseKey + (complete ? ":open" : ":done"));
            bool collapsed = complete ^ _toggled.Contains(toggleKey);
            bool urgentEvent = p.Events.Any(e => !e.Done && !e.NoDeadline && ResetCalculator.EventDaysLeft(e, p.Game, now) <= 7);

            // 초기화 전날인데 남은 숙제가 있으면 주황 테두리 (이벤트 빨간 경고가 우선)
            bool due = p.HasDueSoon;
            var card = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = CardBg,
                BorderBrush = collapsed && urgentEvent ? WarnBorder : due ? DueBorder : CardBorder,
                BorderThickness = new Thickness((collapsed && urgentEvent) || due ? 1.5 : 1),
                Padding = new Thickness(10, 7, 10, collapsed ? 7 : 9),
                Margin = new Thickness(0, 0, 0, 7),
                Opacity = complete ? 0.45 : 1.0
            };
            var stack = new StackPanel();
            card.Child = stack;

            // ---- 헤더 (클릭하면 접기/펼치기) ----
            var header = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.Hand, LastChildFill = true };
            string statusText;
            string? statusTip = null;
            if (p.NextReset is DateTime next)
            {
                string resetIn = ProgressBuilder.FormatRemaining(next - now);
                statusText = complete ? $"✓ 완료 · 초기화 {resetIn}" : $"{p.DoneCount}/{p.Total} · 초기화 {resetIn}";
                statusTip = $"다음 초기화: {next:M'/'d (ddd) HH:mm}";
            }
            else
            {
                statusText = complete ? "✓ 완료" : p.Total > 0 ? $"{p.Remaining}개 남음" : "";
                if (p.Missed.Count > 0)
                    statusText = (statusText.Length > 0 ? statusText + " · " : "") + $"놓침 {p.Missed.Count}";
            }
            var status = new TextBlock
            {
                FontSize = 11,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Text = statusText,
                ToolTip = statusTip
            };
            DockPanel.SetDock(status, Dock.Right);
            header.Children.Add(status);

            var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            title.Inlines.Add(new System.Windows.Documents.Run(collapsed ? "▸ " : "▾ ") { Foreground = TextDim, FontSize = 10 });
            title.Inlines.Add(new System.Windows.Documents.Run(p.Game.Name) { Foreground = TextMain, FontWeight = FontWeights.Bold, FontSize = 13 });
            header.Children.Add(title);

            header.MouseLeftButtonUp += (s, e) =>
            {
                if (!_toggled.Remove(toggleKey)) _toggled.Add(toggleKey);
                e.Handled = true;
                QueueRender();
            };
            stack.Children.Add(header);

            if (collapsed) return card;

            // ---- 초기화 전날 안내: "내일 04:00 초기화 · 3개 남음" ----
            if (due)
            {
                var dueCells = p.DueSoonUndone;
                var soonest = dueCells.OrderBy(c => c.DueAt).First();
                stack.Children.Add(new Border
                {
                    Background = DueBg,
                    BorderBrush = DueBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = $"{soonest.DueText} · {dueCells.Count}개 남음",
                        Foreground = DueText,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold
                    }
                });
            }

            // ---- 본문 ----
            bool grid = p.Game.Type == GameType.Rpg && p.Game.Characters.Count > 0 &&
                        p.VisibleTasks.Any(t => ResetCalculator.IsPerCharacter(t, p.Game));

            var commonCells = p.Cells.Where(c => c.Character == null).ToList();
            if (commonCells.Count > 0)
            {
                if (grid) stack.Children.Add(SectionLabel("원정대 공통"));
                // 일일 / 요일 / 주간 / 월간 을 줄별로 나눠서 표시
                foreach (var group in WidgetOrdering.CommonRows(commonCells, SortDone))
                {
                    var wrap = new WrapPanel();
                    foreach (var cell in group)
                    {
                        var text = TaskText(cell.Task.Name + Hint(cell.Task, p.Game), cell.Done);
                        if (cell.DueSoon && !cell.Done) { text.Foreground = DueText; text.FontWeight = FontWeights.SemiBold; }
                        var cb = MakeCheck(p.Game, cell, text,
                            RuleTip(cell.Task, p.Game));
                        cb.Margin = new Thickness(0, 3, 16, 3);
                        wrap.Children.Add(cb);
                    }
                    var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
                    if (_renderKind != WidgetKind.Cycle) // 주기 컨텐츠 위젯엔 배지 필요 없음
                    {
                        var badge = KindBadge(group.First().Task.Kind);
                        badge.VerticalAlignment = VerticalAlignment.Top;
                        badge.Margin = new Thickness(0, 3, 8, 0);
                        DockPanel.SetDock(badge, Dock.Left);
                        row.Children.Add(badge);
                    }
                    row.Children.Add(wrap);
                    stack.Children.Add(row);
                }
            }

            if (grid) stack.Children.Add(BuildCharacterGrid(p));

            foreach (var ev in WidgetOrdering.Events(p.Events, SortDone))
                stack.Children.Add(BuildEventRow(ev, p.Game, now));

            foreach (var ev in p.Missed)
                stack.Children.Add(BuildMissedRow(ev));

            return card;
        }

        /// <summary>RPG: 행 = 숙제, 열 = 캐릭터</summary>
        private UIElement BuildCharacterGrid(GameProgress p)
        {
            var game = p.Game;
            var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 58 });
            foreach (var _ in game.Characters)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            void AddRow() { g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); row = g.RowDefinitions.Count - 1; }

            // 캐릭터 이름 헤더
            AddRow();
            for (int i = 0; i < game.Characters.Count; i++)
            {
                var ch = game.Characters[i];
                bool chDone = p.CharacterComplete(ch);
                var tb = new TextBlock
                {
                    Text = chDone ? ch.Name + " ✓" : ch.Name,
                    Foreground = chDone ? TextDim : Accent,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(2, 0, 2, 2)
                };
                Grid.SetRow(tb, row);
                Grid.SetColumn(tb, i + 1);
                g.Children.Add(tb);
            }

            var perChar = p.VisibleTasks.Where(t => ResetCalculator.IsPerCharacter(t, game)).ToList();
            foreach (var group in WidgetOrdering.GridGroups(p, perChar, SortDone))
            {
                // 구분 헤더: [일일] ─────────
                AddRow();
                var head = new DockPanel { Margin = new Thickness(0, 7, 0, 2) };
                var badge = KindBadge(group.First().Kind);
                DockPanel.SetDock(badge, Dock.Left);
                head.Children.Add(badge);
                head.Children.Add(new Border
                {
                    Height = 1,
                    Background = CardBorder,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetRow(head, row);
                Grid.SetColumnSpan(head, game.Characters.Count + 1);
                g.Children.Add(head);

                foreach (var task in group)
                {
                    AddRow();
                    var cells = game.Characters.Select(c => p.Cell(task, c)).ToList();
                    bool rowDone = cells.Where(c => c != null).All(c => c!.Done);

                    var name = new TextBlock
                    {
                        Text = task.Name + Hint(task, game),
                        Foreground = rowDone ? TextDim : cells.Any(c => c != null && c.DueSoon && !c.Done) ? DueText : TextMain,
                        FontWeight = !rowDone && cells.Any(c => c != null && c.DueSoon && !c.Done) ? FontWeights.SemiBold : FontWeights.Normal,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 120,
                        Margin = new Thickness(0, 3, 8, 3),
                        ToolTip = RuleTip(task, game)
                    };
                    Grid.SetRow(name, row);
                    g.Children.Add(name);

                    for (int i = 0; i < game.Characters.Count; i++)
                    {
                        var cell = cells[i];
                        if (cell == null) continue; // 이 캐릭터는 해당 없음
                        var ch = game.Characters[i];
                        TextBlock? content = cell.Label != null ? TaskText(cell.Label, cell.Done, 11) : null;
                        if (content != null && cell.DueSoon && !cell.Done) content.Foreground = DueText;
                        string tip = $"{ch.Name} · {task.Name}" + (cell.Label != null ? $" ({cell.Label})" : "");
                        var cb = MakeCheck(game, cell, content, tip);
                        cb.HorizontalAlignment = cell.Label != null ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                        cb.Margin = new Thickness(cell.Label != null ? 6 : 0, 2, 2, 2);
                        Grid.SetRow(cb, row);
                        Grid.SetColumn(cb, i + 1);
                        g.Children.Add(cb);
                    }
                }
            }
            return g;
        }

        private UIElement BuildEventRow(EventQuest ev, Game game, DateTime now)
        {
            int left = ev.NoDeadline ? int.MaxValue : ResetCalculator.EventDaysLeft(ev, game, now);
            bool urgent = !ev.Done && !ev.NoDeadline && left <= 7;
            bool critical = urgent && left <= 1;

            var row = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(critical ? 2 : urgent ? 1.5 : 1),
                BorderBrush = critical ? CriticalBorder : urgent ? WarnBorder : EventBorder,
                Background = critical ? CriticalBg : urgent ? WarnBg : Brushes.Transparent,
                Padding = new Thickness(7, 4, 8, 4),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var dock = new DockPanel();
            var badge = new TextBlock
            {
                Text = ev.NoDeadline ? "무기한" : $"{ProgressBuilder.FormatDDay(left)} · ~{ev.EndDate:M'/'d}",
                Foreground = urgent ? WarnText : TextMuted,
                FontWeight = urgent ? FontWeights.Bold : FontWeights.Normal,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(badge, Dock.Right);
            dock.Children.Add(badge);

            string name = string.IsNullOrWhiteSpace(ev.Name) ? (ev.NoDeadline ? "목표" : "이벤트") : ev.Name.Trim();
            string prefix = ev.NoDeadline ? "[목표] " : "[이벤트] ";
            var cb = new CheckBox
            {
                IsChecked = ev.Done,
                Content = TaskText(prefix + name, ev.Done),
                ToolTip = ev.NoDeadline
                    ? $"{game.Name} 무기한 목표 · 완료하면 다음 초기화 때 삭제됩니다"
                    : $"{game.Name} 이벤트 · {ev.EndDate:yyyy-M-d}까지 (다음 날 {game.ResetTime} 초기화 때 삭제)"
            };
            cb.Click += (s, e) => AppState.SetEventDone(ev, cb.IsChecked == true);
            dock.Children.Add(cb);
            row.Child = dock;
            return row;
        }

        /// <summary>놓친 이벤트 — 회색으로 표시, [확인] 누르면 삭제</summary>
        private UIElement BuildMissedRow(EventQuest ev)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = MissedBorder,
                Background = MissedBg,
                Padding = new Thickness(7, 3, 4, 3),
                Margin = new Thickness(0, 6, 0, 0)
            };
            var dock = new DockPanel();

            var btnText = new TextBlock { Text = "확인 ✕", FontSize = 11, Foreground = TextMuted };
            var btn = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = ButtonBg,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "확인했으면 목록에서 삭제",
                Child = btnText
            };
            btn.MouseEnter += (s, e) => { btn.Background = ButtonHoverBg; btnText.Foreground = TextMain; };
            btn.MouseLeave += (s, e) => { btn.Background = ButtonBg; btnText.Foreground = TextMuted; };
            btn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                AppState.DeleteEvent(ev); // 놓침으로 기록하고 삭제
            };
            DockPanel.SetDock(btn, Dock.Right);
            dock.Children.Add(btn);

            string name = string.IsNullOrWhiteSpace(ev.Name) ? "이벤트" : ev.Name.Trim();
            dock.Children.Add(new TextBlock
            {
                Text = $"[놓침] {name} · ~{ev.EndDate:M'/'d}",
                Foreground = MissedText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = $"{ev.EndDate:yyyy-M-d}에 끝난 이벤트 (완료 못 함)"
            });
            row.Child = dock;
            return row;
        }

        // ================= 픽업 =================

        private void RenderPickups(StackPanel panel, DateTime now)
        {
            foreach (var gp in PickupCalc.ForWidget(AppState.Data, now))
                panel.Children.Add(BuildPickupCard(gp, now));
            if (panel.Children.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "＋ 버튼으로 게임별 픽업(기간)을 등록하세요.",
                    Foreground = TextMuted,
                    Margin = new Thickness(6, 8, 6, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        private UIElement BuildPickupCard(GamePickups gp, DateTime now)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 9),
                Margin = new Thickness(0, 0, 0, 7)
            };
            var stack = new StackPanel();
            card.Child = stack;

            var header = new DockPanel();
            var status = new TextBlock
            {
                FontSize = 11,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Text = gp.Upcoming.Count == 0 ? $"진행 {gp.Active.Count}" : $"진행 {gp.Active.Count} · 예정 {gp.Upcoming.Count}"
            };
            DockPanel.SetDock(status, Dock.Right);
            header.Children.Add(status);
            header.Children.Add(new TextBlock
            {
                Text = gp.Game.Name,
                Foreground = TextMain,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(header);

            foreach (var p in gp.Active) stack.Children.Add(BuildPickupRow(p, gp.Game, now, upcoming: false));
            foreach (var p in gp.Upcoming) stack.Children.Add(BuildPickupRow(p, gp.Game, now, upcoming: true));
            return card;
        }

        private UIElement BuildPickupRow(PickupBanner p, Game game, DateTime now, bool upcoming)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 6, 0, 0), Opacity = upcoming ? 0.6 : 1.0 };

            var badgeText = new TextBlock
            {
                Text = upcoming ? "예정" : "진행",
                Foreground = upcoming ? TextMuted : Accent,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = upcoming ? CardBorder : Accent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 0, 5, 0),
                MinWidth = 32,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = badgeText
            };
            DockPanel.SetDock(badge, Dock.Left);
            dock.Children.Add(badge);

            var period = new TextBlock
            {
                Text = PickupCalc.PeriodText(p, game, now),
                Foreground = TextMuted,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(period, Dock.Right);
            dock.Children.Add(period);

            dock.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(p.Name) ? "픽업" : p.Name.Trim(),
                Foreground = TextMain,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = $"{game.Name} · {p.Name} · {p.StartDate:yyyy-M-d} ~ {p.EndDate:yyyy-M-d}"
            });
            return dock;
        }

        // ================= 작은 도우미 =================

        private static CheckBox MakeCheck(Game game, TaskCell cell, object? content, string tooltip)
        {
            var cb = new CheckBox { IsChecked = cell.Done, Content = content, ToolTip = tooltip };
            cb.Click += (s, e) => AppState.SetDone(game, cell.Task, cell.Character, cb.IsChecked == true);
            return cb;
        }

        private TextBlock TaskText(string text, bool done, double size = 12)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = done ? TextDim : TextMain,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (done) tb.TextDecorations = TextDecorations.Strikethrough;
            return tb;
        }

        private TextBlock SectionLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = TextDim,
            Margin = new Thickness(0, 6, 0, 1)
        };

        private static string Hint(HomeworkTask t, Game g)
        {
            switch (t.Kind)
            {
                case ResetKind.Weekdays:
                    // 그 요일에만 보이므로 "오늘 하는 날"
                    return " · 오늘 하는 날";
                case ResetKind.Monthly:
                    return g.Type == GameType.Rpg ? $" ({t.MonthDay}일)" : $" (매월 {t.MonthDay}일)";
                case ResetKind.Cycle:
                    // 마감까지 남은 날: "허구 이야기 · D-12"
                    return " · " + ProgressBuilder.FormatDDay(ResetCalculator.CycleDaysLeft(t, g, DateTime.Now));
                default:
                    return "";
            }
        }

        /// <summary>일일 / 요일 / 주간 / 월간 색 배지 (테마 색)</summary>
        private Border KindBadge(ResetKind k)
        {
            int idx = k switch
            {
                ResetKind.Daily => 0,
                ResetKind.Weekdays => 1,
                ResetKind.Weekly => 2,
                _ => 3     // 월간 · 주기
            };
            var (_, r, g, b) = WidgetTheme.Parse(_theme.Badge[idx]);
            var fg = new SolidColorBrush(Color.FromRgb(r, g, b));
            fg.Freeze();
            var bg = new SolidColorBrush(Color.FromArgb(_theme.BadgeBgAlpha, r, g, b));
            bg.Freeze();
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = bg,
                Padding = new Thickness(5, 1, 5, 1),
                MinWidth = 32,
                Child = new TextBlock
                {
                    Text = KindLabel(k),
                    Foreground = fg,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
        }

        private static string RuleTip(HomeworkTask t, Game g)
        {
            if (t.Kind == ResetKind.Weekdays)
                return ResetCalculator.DescribeRule(t, g) + " 하는 숙제 — 그 요일에만 표시돼요";
            if (t.Kind == ResetKind.Cycle)
            {
                var end = ResetCalculator.CycleCurrentEnd(t, g, DateTime.Now);
                return $"{ResetCalculator.DescribeRule(t, g)} · 이번 회차 {end:M'/'d}({ResetCalculator.DayName(end.DayOfWeek)})까지";
            }
            return ResetCalculator.DescribeRule(t, g) + " 초기화";
        }

        private static string KindLabel(ResetKind k) => k switch
        {
            ResetKind.Daily => "일일",
            ResetKind.Weekdays => "요일",
            ResetKind.Weekly => "주간",
            ResetKind.Monthly => "월간",
            _ => "주기"
        };

        private static Brush Hex(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            b.Freeze();
            return b;
        }
    }
}

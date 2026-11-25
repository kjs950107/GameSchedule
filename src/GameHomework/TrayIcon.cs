using System;
using System.Collections.Generic;
using System.Linq;
using GameHomework.Core;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace GameHomework
{
    /// <summary>작업표시줄 트레이 아이콘과 메뉴</summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly Forms.NotifyIcon _icon;
        private readonly Dictionary<int, Forms.ToolStripMenuItem> _widgetItems = new();
        private readonly Forms.ToolStripMenuItem _lockItem;
        private readonly Forms.ToolStripMenuItem _layoutItem;
        private readonly Forms.ToolStripMenuItem _topmostMenu = new("항상 위에 표시");
        private readonly Dictionary<int, Forms.ToolStripMenuItem> _topmostItems = new();
        private readonly Forms.ToolStripMenuItem _autoStartItem;
        private readonly Icon _appIcon;

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public TrayIcon()
        {
            _appIcon = CreateIcon();

            var menu = new Forms.ContextMenuStrip();
            var findItem = new Forms.ToolStripMenuItem("위젯 찾기 (맨 앞으로)", null, (s, e) => App.RevealWidgets());
            for (int slot = 0; slot < WidgetSettings.SlotCount; slot++)
            {
                var i = slot;
                _widgetItems[i] = new Forms.ToolStripMenuItem("", null, (s, e) => ToggleWidget(i));
                var item = new Forms.ToolStripMenuItem("", null, (s, e) =>
                {
                    var pl = AppState.Data.Widget.GetSlot(i);
                    pl.AlwaysOnTop = !pl.AlwaysOnTop;
                    AppState.Save();
                });
                _topmostItems[i] = item;
                _topmostMenu.DropDownItems.Add(item);
            }
            _layoutItem = new Forms.ToolStripMenuItem("위젯 배치 바꾸기 (합치기 · 나누기)…", null,
                (s, e) => App.OpenSettings(SettingsWindow.WidgetTabIndex));
            var settingsItem = new Forms.ToolStripMenuItem("설정", null, (s, e) => App.OpenSettings(0));
            var eventItem = new Forms.ToolStripMenuItem("이벤트 등록", null, (s, e) => App.OpenSettings(1));
            var cycleItem = new Forms.ToolStripMenuItem("주기 컨텐츠 등록 (N주마다)", null, (s, e) => App.OpenCycleWindow());
            var pickupItem = new Forms.ToolStripMenuItem("픽업 등록", null, (s, e) => App.OpenSettings(SettingsWindow.PickupTabIndex));
            _lockItem = new Forms.ToolStripMenuItem("위치 잠금", null, (s, e) => ToggleLock());
            _autoStartItem = new Forms.ToolStripMenuItem("윈도우 시작 시 실행", null, (s, e) => ToggleAutoStart());
            var exitItem = new Forms.ToolStripMenuItem("종료", null, (s, e) => App.ExitApp());

            var header = new Forms.ToolStripMenuItem($"게임 숙제 관리 v{App.Version}") { Enabled = false };
            menu.Items.Add(header);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(findItem);
            foreach (var item in _widgetItems.Values) menu.Items.Add(item);
            menu.Items.Add(_layoutItem);
            menu.Items.Add(_topmostMenu);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(settingsItem);
            menu.Items.Add(new Forms.ToolStripMenuItem("사용법 (웹)", null, (s, e) => App.OpenGuide()));
            menu.Items.Add(eventItem);
            menu.Items.Add(cycleItem);
            menu.Items.Add(pickupItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(_lockItem);
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);
            menu.Opening += (s, e) => RefreshMenu();

            _icon = new Forms.NotifyIcon
            {
                Icon = _appIcon,
                Text = "게임 숙제 관리",
                ContextMenuStrip = menu,
                Visible = true
            };
            _icon.MouseClick += (s, e) =>
            {
                // 한 번 클릭: 위젯을 잠깐 맨 앞으로 (다른 곳을 클릭하면 다시 바탕화면으로)
                if (e.Button == Forms.MouseButtons.Left) App.RevealWidgets();
            };
            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) App.OpenSettings(0);
            };

            AutoStart.RefreshPathIfEnabled();

            // 마우스를 올리면: 버전 + 오늘 요약 (1분마다 갱신)
            _tipTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _tipTimer.Tick += (s, e) => UpdateTooltip();
            _tipTimer.Start();
            UpdateTooltip();
        }

        private readonly System.Windows.Threading.DispatcherTimer _tipTimer;

        private void UpdateTooltip()
        {
            try
            {
                var text = $"게임 숙제 v{App.Version}\n{App.BuildToday().ShortLine()}";
                _icon.Text = text.Length > 127 ? text[..127] : text; // 트레이 툴팁 최대 길이
            }
            catch (Exception ex)
            {
                App.LogError("트레이 툴팁 갱신 실패", ex);
            }
        }

        private void RefreshMenu()
        {
            var ws = AppState.Data.Widget;
            var active = ws.ActiveSlots;
            foreach (var pair in _widgetItems)
            {
                // 뭔가 들어 있는 칸만 메뉴에 보임: "일일 · 주간 숙제 위젯"
                pair.Value.Visible = active.Contains(pair.Key);
                pair.Value.Text = WidgetWindow.SlotTitle(ws.KindsIn(pair.Key)) + " 위젯";
                pair.Value.Checked = App.Widgets.TryGetValue(pair.Key, out var w) && w.IsVisible;
            }
            foreach (var pair in _topmostItems)
            {
                pair.Value.Visible = active.Contains(pair.Key);
                pair.Value.Text = WidgetWindow.SlotTitle(ws.KindsIn(pair.Key));
                pair.Value.Checked = ws.GetSlot(pair.Key).AlwaysOnTop;
            }
            _lockItem.Checked = AppState.Data.Widget.LockPosition;
            _autoStartItem.Checked = AutoStart.IsEnabled;
        }

        private static void ToggleWidget(int slot)
        {
            if (!App.Widgets.TryGetValue(slot, out var w)) return;
            App.SetWidgetVisible(slot, !w.IsVisible);
        }

        private static void ToggleLock()
        {
            AppState.Data.Widget.LockPosition = !AppState.Data.Widget.LockPosition;
            AppState.Save();
        }

        private static void ToggleAutoStart() => AutoStart.Set(!AutoStart.IsEnabled);

        /// <summary>체크 표시가 있는 둥근 사각형 아이콘을 직접 그린다 (별도 .ico 파일 불필요)</summary>
        private static Icon CreateIcon()
        {
            // 앱 아이콘(app.ico)을 우선 사용, 실패하면 직접 그린다
            try
            {
                var res = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
                if (res != null)
                {
                    using var stream = res.Stream;
                    return new Icon(stream, Forms.SystemInformation.SmallIconSize);
                }
            }
            catch { }

            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var path = new GraphicsPath();
                var r = new Rectangle(2, 2, 27, 27);
                int d = 9;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using var bg = new SolidBrush(Color.FromArgb(255, 108, 140, 255));
                g.FillPath(bg, path);
                using var pen = new Pen(Color.White, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(pen, new[] { new Point(9, 16), new Point(14, 21), new Point(23, 11) });
            }
            IntPtr h = bmp.GetHicon();
            try
            {
                using var tmp = Icon.FromHandle(h);
                return (Icon)tmp.Clone();
            }
            finally
            {
                DestroyIcon(h);
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _tipTimer.Stop();
            _icon.Visible = false;
            _icon.Dispose();
            _appIcon.Dispose();
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace GameHomework.Native
{
    /// <summary>
    /// 위젯을 바탕화면에 고정한다.
    /// - 창 소유자를 바탕화면(Progman)으로 지정 → Win+D(바탕화면 보기)를 눌러도 사라지지 않음
    /// - z-order를 항상 맨 아래로 강제 → 클릭해도 다른 프로그램 위로 올라오지 않음
    /// - 위젯 안을 클릭해도 활성화되지 않음 (WM_MOUSEACTIVATE) → 게임/다른 창의 포커스를 뺏지 않음
    /// - 작업표시줄에 표시 (WS_EX_APPWINDOW). 작업표시줄 버튼을 누르면 잠깐 맨 앞으로 나왔다가
    ///   다른 곳을 클릭하면(또는 버튼을 한 번 더 누르면) 다시 맨 아래로 돌아감
    /// </summary>
    public sealed class DesktopPinner
    {
        private const int GWL_EXSTYLE = -20;
        private const int GWLP_HWNDPARENT = -8;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const long WS_EX_APPWINDOW = 0x00040000;
        private const long WS_EX_NOACTIVATE = 0x08000000;

        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int WM_ACTIVATE = 0x0006;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int MA_NOACTIVATE = 3;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        /// <summary>항상 위에 표시 (켜면 바탕화면 고정 대신 다른 창 위에 계속 떠 있음)</summary>
        private bool _topmost;
        public bool IsTopmost => _topmost;

        public void SetTopmost(bool on)
        {
            if (_topmost == on && _hwnd != IntPtr.Zero) return;
            _topmost = on;
            _peeking = false;
            _peekWatch?.Stop();
            if (_hwnd == IntPtr.Zero) return; // 창이 만들어지면 AttachToDesktop에서 적용
            if (on)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                SetWindowPos(_hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                SendToBottom();
            }
        }

        /// <summary>작업표시줄로 불러내서 잠깐 맨 앞에 나와 있는 상태</summary>
        private bool _peeking;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int value);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        private const uint GA_ROOT = 2;

        /// <summary>작업표시줄·미리보기·트레이 넘침 창 등 — 여기로 포커스가 가도 "다른 프로그램"으로 치지 않음</summary>
        private static readonly string[] ShellClasses =
        {
            "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "TaskListThumbnailWnd", "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow", "MultitaskingViewFrame",
            "ForegroundStaging", "Windows.UI.Core.CoreWindow"
        };

        private static bool IsShellWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return true;
            var root = GetAncestor(hwnd, GA_ROOT);
            foreach (var h in new[] { hwnd, root })
            {
                if (h == IntPtr.Zero) continue;
                var sb = new System.Text.StringBuilder(128);
                GetClassName(h, sb, sb.Capacity);
                var cls = sb.ToString();
                foreach (var c in ShellClasses)
                    if (string.Equals(cls, c, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static IntPtr GetLong(IntPtr h, int i) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(h, i) : new IntPtr(GetWindowLong32(h, i));

        private static void SetLong(IntPtr h, int i, IntPtr v)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(h, i, v);
            else SetWindowLong32(h, i, v.ToInt32());
        }

        private readonly Window _window;
        private IntPtr _hwnd;
        private uint _taskbarCreatedMsg;

        private readonly DispatcherTimer _peekWatch;
        private IntPtr _peekForeground;
        private static readonly uint OurProcessId = (uint)Environment.ProcessId;

        public DesktopPinner(Window window)
        {
            _window = window;
            _window.SourceInitialized += OnSourceInitialized;

            // 잠깐 앞으로 나온 동안, 다른 프로그램으로 포커스가 넘어가면 다시 맨 아래로
            _peekWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _peekWatch.Tick += (s, e) =>
            {
                if (!_peeking) { _peekWatch.Stop(); return; }
                var fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == _peekForeground || fg == _hwnd) return;
                if (IsOurWindow(fg)) return;      // 다른 위젯이나 설정 창
                if (IsShellWindow(fg)) return;    // 작업표시줄 (다른 위젯 버튼을 누르는 중)
                SendToBottom();
            };
        }

        public IntPtr Handle => _hwnd;

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(_window).Handle;
            _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

            // 작업표시줄에 나오도록: APPWINDOW 켜고 TOOLWINDOW 끔.
            // NOACTIVATE는 끈다 (켜 두면 작업표시줄 버튼을 눌러도 창이 반응하지 않음).
            // 대신 위젯 안 클릭으로는 WM_MOUSEACTIVATE에서 활성화를 막는다.
            long ex = GetLong(_hwnd, GWL_EXSTYLE).ToInt64();
            ex |= WS_EX_APPWINDOW;
            ex &= ~(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            SetLong(_hwnd, GWL_EXSTYLE, new IntPtr(ex));

            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
            AttachToDesktop();
        }

        /// <summary>바탕화면 창(Progman)을 소유자로 지정. 탐색기가 재시작되면 다시 호출된다.</summary>
        public void AttachToDesktop()
        {
            if (_hwnd == IntPtr.Zero) return;
            var progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
                SetLong(_hwnd, GWLP_HWNDPARENT, progman);
            if (_topmost) SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            else SendToBottom();
        }

        public void SendToBottom()
        {
            if (_hwnd == IntPtr.Zero || _topmost) return;
            _peeking = false;
            _peekWatch.Stop();
            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>잠깐 맨 앞으로 (다른 창을 클릭하면 다시 맨 아래로)</summary>
        public void Peek()
        {
            if (_hwnd == IntPtr.Zero || _topmost) return; // 항상 위면 이미 맨 앞
            _peeking = true;
            _peekForeground = GetForegroundWindow();
            SetWindowPos(_hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            _peekWatch.Start();
        }

        /// <summary>이 앱(다른 위젯, 설정 창)의 창인지</summary>
        private static bool IsOurWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == OurProcessId;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_WINDOWPOSCHANGING)
            {
                // 잠깐 불러낸 상태가 아니면, z-order가 바뀌려 할 때마다 맨 아래로 고정
                var wp = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if ((wp.flags & SWP_NOZORDER) == 0)
                {
                    if (_topmost)
                    {
                        // 항상 위: 무엇이 와도 맨 위 그룹 유지
                        wp.hwndInsertAfter = HWND_TOPMOST;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                    else if (!_peeking)
                    {
                        wp.hwndInsertAfter = HWND_BOTTOM;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
            }
            else if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            else if (msg == WM_ACTIVATE && _topmost)
            {
                // 항상 위: 활성/비활성과 상관없이 그대로
            }
            else if (msg == WM_ACTIVATE)
            {
                // 작업표시줄 버튼/Alt+Tab으로 활성화되면 맨 앞으로.
                // 비활성화될 때: 다른 위젯(또는 설정 창)으로 넘어간 거면 그대로 떠 있고,
                // 다른 프로그램으로 넘어갔으면 맨 아래로.
                int state = (int)(wParam.ToInt64() & 0xFFFF);
                if (state != 0)
                {
                    Peek();
                }
                else if (_peeking && (IsOurWindow(lParam) || IsShellWindow(lParam)))
                {
                    // 다른 위젯 / 작업표시줄로 넘어감 → 그대로 떠 있고,
                    // 나중에 다른 프로그램으로 포커스가 가면 그때 내려감 (0.25초마다 확인)
                    _peekForeground = IntPtr.Zero;
                    _peekWatch.Start();
                }
                else
                {
                    SendToBottom();
                }
            }
            else if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_MINIMIZE)
            {
                // 작업표시줄 버튼을 다시 누르면 최소화 대신 맨 아래로 ↔ 맨 앞으로 전환 (바탕화면에서 사라지지 않게)
                handled = true;
                if (_topmost) return IntPtr.Zero; // 항상 위: 최소화하지 않음
                if (_peeking) SendToBottom();
                else Peek();
                return IntPtr.Zero;
            }
            else if (_taskbarCreatedMsg != 0 && msg == (int)_taskbarCreatedMsg)
            {
                // explorer.exe 재시작 → Progman이 새로 만들어짐
                AttachToDesktop();
            }
            return IntPtr.Zero;
        }
    }
}

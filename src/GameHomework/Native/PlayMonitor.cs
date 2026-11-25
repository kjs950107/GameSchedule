using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using GameHomework.Core;

namespace GameHomework.Native
{
    /// <summary>
    /// 15초마다 실행 중인 프로그램 목록과 맨 앞 창을 확인해서 게임별 플레이 시간을 기록한다.
    /// 프로세스 이름만 읽고 게임 내부는 건드리지 않는다.
    /// </summary>
    public sealed class PlayMonitor : IDisposable
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

        private readonly PlayTracker _tracker = new(Interval);
        private readonly DispatcherTimer _timer;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public PlayMonitor()
        {
            _timer = new DispatcherTimer { Interval = Interval };
            _timer.Tick += (s, e) => Tick();
        }

        public void Start()
        {
            try { AppState.Log.RecoverOpenSessions(); }
            catch (Exception ex) { App.LogError("플레이 기록 복구 실패", ex); }
            Tick();
            _timer.Start();
        }

        private void Tick()
        {
            try
            {
                var games = AppState.Data.Games.Where(g => g.ExeNames.Count > 0).ToList();
                if (games.Count == 0 && _tracker.OpenSessions.Count == 0) return;

                var names = new List<string>();
                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        try { names.Add(p.ProcessName); } catch { }
                    }
                }

                var ended = _tracker.Sample(DateTime.Now, games, names, ForegroundProcessName());
                Flush(ended);
            }
            catch (Exception ex)
            {
                App.LogError("플레이 시간 측정 실패", ex);
            }
        }

        private void Flush(IEnumerable<PlaySession> ended)
        {
            var list = ended.ToList();
            if (list.Count > 0) AppState.Log.AppendMany(list.Select(x => x.ToRecord()));
            AppState.Log.SaveOpenSessions(_tracker.OpenSessions);
        }

        private static string? ForegroundProcessName()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return null;
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>지금 켜져 있는 게임 세션 (오늘 요약에 실행 중 시간 포함용)</summary>
        public List<PlaySession> OpenSessions => _tracker.OpenSessions.ToList();

        /// <summary>절전 들어갈 때 / 앱 종료할 때: 지금 세션을 모두 끝난 것으로 기록</summary>
        public void CloseAll()
        {
            try { Flush(_tracker.CloseAll()); }
            catch (Exception ex) { App.LogError("플레이 기록 저장 실패", ex); }
        }

        public void Dispose()
        {
            _timer.Stop();
            CloseAll();
        }
    }
}

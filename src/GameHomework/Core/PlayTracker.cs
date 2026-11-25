using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameHomework.Core
{
    /// <summary>게임 한 번 실행 (켜서 끌 때까지)</summary>
    public class PlaySession
    {
        public Guid GameId { get; set; }
        public string Game { get; set; } = "";
        public bool PlayOnly { get; set; }
        public DateTime Start { get; set; }
        /// <summary>마지막으로 실행 중인 걸 확인한 시각 (= 끝난 시각)</summary>
        public DateTime LastSeen { get; set; }
        /// <summary>켜져 있던 시간(초)</summary>
        public double RunSec { get; set; }
        /// <summary>화면 맨 앞(포커스)에 있던 시간(초)</summary>
        public double FgSec { get; set; }

        public LogRecord ToRecord() => new()
        {
            Type = LogType.Play,
            T = Start,
            End = LastSeen,
            GameId = GameId,
            Game = Game,
            RunSec = Math.Round(RunSec),
            FgSec = Math.Round(FgSec),
            PlayOnly = PlayOnly ? true : null
        };
    }

    /// <summary>
    /// 일정 간격으로 "지금 실행 중인 프로그램 목록"과 "맨 앞 창의 프로그램"을 받아서 게임별 시간을 쌓는다.
    /// 프로세스를 직접 읽지 않으므로(받기만 함) 테스트 가능.
    /// </summary>
    public class PlayTracker
    {
        private readonly Dictionary<Guid, PlaySession> _open = new();
        private DateTime? _lastSample;

        /// <summary>이 간격의 2.5배 넘게 샘플이 없으면(절전 등) 그 사이는 시간에 넣지 않고 세션을 끊는다</summary>
        public TimeSpan Interval { get; }

        public PlayTracker(TimeSpan interval)
        {
            Interval = interval;
        }

        public IReadOnlyCollection<PlaySession> OpenSessions => _open.Values;

        /// <summary>"LOSTARK.exe", "lostark", " LostArk.EXE " → "lostark"</summary>
        public static string NormalizeExe(string? name)
        {
            var s = (name ?? "").Trim().ToLowerInvariant();
            if (s.EndsWith(".exe", StringComparison.Ordinal)) s = s[..^4];
            return s;
        }

        /// <summary>
        /// 샘플 한 번. runningExe: 실행 중인 프로세스 이름들, foregroundExe: 맨 앞 창의 프로세스 이름.
        /// 끝난 세션 목록을 돌려준다 (기록에 추가할 것).
        /// </summary>
        public List<PlaySession> Sample(DateTime now, IEnumerable<Game> games, IEnumerable<string> runningExe, string? foregroundExe)
        {
            var ended = new List<PlaySession>();
            double dt = _lastSample == null ? 0 : (now - _lastSample.Value).TotalSeconds;
            if (dt < 0 || dt > Interval.TotalSeconds * 2.5)
            {
                // 절전/시계 변경 등으로 끊긴 구간 → 지금까지 세션은 마지막 확인 시각에 끝난 것으로
                ended.AddRange(CloseAll());
                dt = 0;
            }
            _lastSample = now;

            var running = new HashSet<string>(runningExe.Select(NormalizeExe));
            string fg = NormalizeExe(foregroundExe);

            var runningGames = new List<Game>();
            Game? fgGame = null;
            foreach (var g in games)
            {
                var exes = g.ExeNames.Select(NormalizeExe).Where(x => x.Length > 0).ToList();
                if (exes.Count == 0) continue;
                if (exes.Any(running.Contains)) runningGames.Add(g);
                if (fg.Length > 0 && exes.Contains(fg)) fgGame = g;
            }

            foreach (var g in runningGames)
            {
                if (_open.TryGetValue(g.Id, out var s))
                {
                    s.RunSec += dt;
                    if (fgGame?.Id == g.Id) s.FgSec += dt;
                    s.LastSeen = now;
                    s.Game = g.Name;
                }
                else
                {
                    _open[g.Id] = new PlaySession
                    {
                        GameId = g.Id,
                        Game = g.Name,
                        PlayOnly = g.Type == GameType.PlayOnly,
                        Start = now,
                        LastSeen = now
                    };
                }
            }

            var runningIds = runningGames.Select(g => g.Id).ToHashSet();
            foreach (var id in _open.Keys.Where(id => !runningIds.Contains(id)).ToList())
            {
                ended.Add(_open[id]);
                _open.Remove(id);
            }
            return ended;
        }

        /// <summary>모든 세션 종료 (앱 종료, 절전 들어갈 때)</summary>
        public List<PlaySession> CloseAll()
        {
            var list = _open.Values.ToList();
            _open.Clear();
            _lastSample = null;
            return list;
        }
    }
}

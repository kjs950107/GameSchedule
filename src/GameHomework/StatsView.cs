using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameHomework.Core;

namespace GameHomework
{
    /// <summary>설정 창 "통계" 탭 내용을 그린다 (StatsResult → 화면)</summary>
    public static class StatsView
    {
        private static readonly Brush Ink = Hex("#1F2328");
        private static readonly Brush Muted = Hex("#5B6270");
        private static readonly Brush Border_ = Hex("#E3E5E9");
        private static readonly Brush Track = Hex("#F1F3F5");
        private static readonly Brush BarLight = Hex("#BAC8FF");
        private static readonly Brush BarDark = Hex("#3B5BDB");
        private static readonly Brush BarMid = Hex("#4C6EF5");
        private static readonly Brush Warn = Hex("#C92A2A");
        private static readonly Brush ResetMark = Hex("#E8590C");
        /// <summary>일일 순서 보기 방식 (0 = 평균 순서, 1 = 자주 나온 순서) — 새로고침해도 유지</summary>
        private static int _orderMode;
        /// <summary>요일별 게임 시간 보기 (0 = 하루 평균, 1 = 합계)</summary>
        private static int _weekdayMode;

        private static readonly Brush[] Heat = { Hex("#F1F3F5"), Hex("#DBE4FF"), Hex("#BAC8FF"), Hex("#748FFC"), Hex("#3B5BDB") };

        public static void Render(Panel host, StatsResult r, int periodDays, TodaySummary? today = null)
        {
            host.Children.Clear();

            // ---- 오늘 (기간 필터와 상관없이 항상 오늘 기준, 실행 중인 게임 포함) ----
            if (today != null) host.Children.Add(TodayCard(today));

            if (r.IsEmpty)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "아직 쌓인 기록이 없어요.\n\n숙제를 체크하고, 게임 정보에 실행 파일을 등록해 두면 여기에 통계가 쌓입니다.\n" +
                           "PC를 켜고 첫 체크까지 걸린 시간은 '윈도우 시작 시 자동 실행'을 켜 두어야 정확해요.",
                    Foreground = Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 12, 4, 0)
                });
                return;
            }

            // ---- 요약 카드 ----
            var cards = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            if (r.FirstCheckMinutes.Count > 0)
                cards.Children.Add(Card("PC를 켜고 첫 체크까지",
                    $"평균 {Minutes(r.FirstCheckMinutes.Average())}",
                    $"가장 빠름 {Minutes(r.FirstCheckMinutes.Min())} · 가장 늦음 {Minutes(r.FirstCheckMinutes.Max())} · {r.FirstCheckMinutes.Count}회"));
            else
                cards.Children.Add(Card("PC를 켜고 첫 체크까지", "기록 없음", "앱 시작 후 체크한 기록이 아직 없어요"));

            if (r.Play.Count > 0 && r.PlayDays > 0)
            {
                // 큰 숫자 = 실제로 게임한 시간 (동시에 켠 시간은 한 번만)
                string sub = $"화면 맨 앞 {StatsEngine.FormatDuration(r.TotalFgSec / r.PlayDays)}";
                if (r.OverlapSec >= 60)
                    sub += $" · 여러 게임 동시에 켜 둔 시간 {StatsEngine.FormatDuration(r.OverlapSec / r.PlayDays)}";
                cards.Children.Add(Card("하루 평균 게임 시간 (게임한 날 기준)", StatsEngine.FormatDuration(r.ActualPlaySec / r.PlayDays), sub));
                var top = r.Play[0];
                cards.Children.Add(Card("가장 오래 한 게임", top.Game,
                    $"켜둔 시간 {StatsEngine.FormatDuration(top.RunSec)} · 게임한 시간의 {Math.Min(100, Math.Round(top.RunSec / Math.Max(1, r.ActualPlaySec) * 100))}% 동안 켜져 있었음"));
            }
            else
            {
                cards.Children.Add(Card("플레이 시간", "기록 없음", "게임 정보 → 실행 파일을 등록하세요"));
            }

            var doneEvents = r.Events.Where(e => !e.Missed).ToList();
            if (r.Events.Count > 0)
                cards.Children.Add(Card("이벤트 완료 시점",
                    doneEvents.Count > 0 ? $"마감 {Math.Round(doneEvents.Average(e => e.DaysLeft ?? 0), 1)}일 전" : "완료 없음",
                    $"{r.Events.Count}개 중 {doneEvents.Count}개 완료 · 놓침 {r.Events.Count - doneEvents.Count}개"));
            host.Children.Add(cards);

            // ---- 게임별 플레이 시간 ----
            if (r.Play.Count > 0)
            {
                var sec = Section("게임별 플레이 시간", out var body, "게임마다 따로 잰 시간 · 동시에 켜 둔 게임은 각각 들어가서, 다 더하면 실제 게임 시간보다 길 수 있어요");
                var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                legend.Children.Add(Legend(BarLight, "켜둔 시간"));
                legend.Children.Add(Legend(BarDark, "화면 맨 앞에 있던 시간"));
                body.Children.Add(legend);
                double max = r.Play.Max(p => p.RunSec);
                foreach (var p in r.Play)
                {
                    var name = new StackPanel { Orientation = Orientation.Horizontal };
                    name.Children.Add(new TextBlock { Text = p.Game, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140 });
                    if (p.PlayOnly)
                        name.Children.Add(new Border
                        {
                            BorderBrush = Hex("#C9CDD4"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock { Text = "숙제 없음", FontSize = 10.5, Foreground = Muted }
                        });
                    var bar = new Grid { Height = 16, Background = Track };
                    bar.Children.Add(BarRect(BarLight, max == 0 ? 0 : p.RunSec / max));
                    bar.Children.Add(BarRect(BarDark, max == 0 ? 0 : p.FgSec / max));
                    var val = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    val.Inlines.Add(new System.Windows.Documents.Run(StatsEngine.FormatDuration(p.RunSec) + " / "));
                    val.Inlines.Add(new System.Windows.Documents.Run(StatsEngine.FormatDuration(p.FgSec)) { Foreground = Ink, FontWeight = FontWeights.SemiBold });
                    body.Children.Add(Row3(name, bar, val, 210, 150));
                }
                host.Children.Add(sec);
            }

            // ---- 요일별 게임 시간 ----
            if (r.WeekdayPlay.Count > 0)
            {
                var sec = Section("요일별 게임 시간", out var body,
                    "그 요일에 게임을 켜 둔 시간 · 하루는 새벽 4시 기준 · 맨 윗줄은 실제로 게임한 시간(동시에 켠 시간은 한 번만) · 칸에 마우스를 올리면 화면 맨 앞 시간");
                var mode = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
                mode.Items.Add("하루 평균");
                mode.Items.Add("합계");
                mode.SelectedIndex = _weekdayMode;
                var content = new StackPanel();
                mode.SelectionChanged += (s, e) =>
                {
                    _weekdayMode = Math.Max(0, mode.SelectedIndex);
                    FillWeekdayPlay(content, r);
                };
                body.Children.Add(mode);
                body.Children.Add(content);
                FillWeekdayPlay(content, r);
                host.Children.Add(sec);
            }

            // ---- 먼저 시작하는 게임 + 체크 간격 ----
            var two = new WrapPanel();
            if (r.FirstGames.Count > 0)
            {
                var sec = Section("먼저 시작하는 게임", out var body, $"게임을 켠 {r.FirstGameDays}일 중 그날 가장 먼저 실행한 비율 · 숙제 없는 게임 제외 · 하루는 새벽 4시 기준", 470);
                int rank = 1;
                foreach (var f in r.FirstGames.Take(8))
                {
                    var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    Add(g, new TextBlock { Text = rank++.ToString(), FontWeight = FontWeights.Bold, Foreground = BarDark, HorizontalAlignment = HorizontalAlignment.Center }, 0);
                    Add(g, new TextBlock { Text = f.Game, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis }, 1);
                    var track = new Grid { Height = 10, Background = Track, VerticalAlignment = VerticalAlignment.Center };
                    track.Children.Add(BarRect(BarMid, f.Ratio));
                    Add(g, track, 2);
                    Add(g, new TextBlock { Text = $"{f.Days}일 ({Math.Round(f.Ratio * 100)}%)", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right }, 3);
                    body.Children.Add(g);
                }
                two.Children.Add(sec);
            }
            if (r.GapMinutes.Count > 0)
            {
                var sec = Section("체크 사이 간격", out var body, $"체크하고 다음 체크까지 · 중간값 {Minutes(StatsEngine.Median(r.GapMinutes))}", 470);
                int gmax = Math.Max(1, r.GapBuckets.Max());
                var chart = new Grid { Height = 150 };
                for (int i = 0; i < 6; i++)
                {
                    chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5, 0, 5, 0) };
                    col.Children.Add(new TextBlock { Text = r.GapBuckets[i].ToString(), FontSize = 11, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center });
                    col.Children.Add(new Border { Height = Math.Max(3, r.GapBuckets[i] / (double)gmax * 110), Background = BarMid, CornerRadius = new CornerRadius(3, 3, 0, 0), Margin = new Thickness(0, 3, 0, 0) });
                    col.Children.Add(new Border { Height = 1, Background = Hex("#C9CDD4") });
                    col.Children.Add(new TextBlock { Text = StatsResult.GapLabels[i], FontSize = 11, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
                    Add(chart, col, i);
                }
                body.Children.Add(chart);
                two.Children.Add(sec);
            }
            if (two.Children.Count > 0) host.Children.Add(two);

            // ---- 일일 숙제 하는 순서 ----
            if (r.DailyOrderDays > 0)
            {
                var sec = Section("일일 숙제 하는 순서", out var body,
                    $"일일 숙제를 한 {r.DailyOrderDays}일 동안, 게임별로 그날 처음 체크한 순서 · 하루는 새벽 4시 기준 · 게임 필터와 상관없이 전체 비교");
                var mode = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
                mode.Items.Add("평균 순서");
                mode.Items.Add("자주 나온 순서");
                mode.SelectedIndex = _orderMode;
                var content = new StackPanel();
                mode.SelectionChanged += (s, e) =>
                {
                    _orderMode = Math.Max(0, mode.SelectedIndex);
                    FillOrder(content, r);
                };
                body.Children.Add(mode);
                body.Children.Add(content);
                FillOrder(content, r);
                host.Children.Add(sec);
            }

            // ---- 주간 숙제 요일 ----
            if (r.Weekdays.Count > 0)
            {
                var sec = Section("주간 숙제를 주로 깨는 요일", out var body, "진할수록 그 요일에 많이 완료 · 주황 밑줄 = 그 숙제의 초기화 요일 · 게임 초기화 시각 기준 요일");
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                for (int i = 0; i < 7; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 40 });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int i = 0; i < 7; i++)
                    Add(g, new TextBlock { Text = StatsEngine.DayNames[i], Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) }, i + 1, 0);
                Add(g, new TextBlock { Text = "주로", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right }, 8, 0);
                int row = 1;
                foreach (var w in r.Weekdays)
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    Add(g, new TextBlock { Text = w.Name, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = w.Name }, 0, row);
                    int cmax = Math.Max(1, w.Counts.Max());
                    for (int i = 0; i < 7; i++)
                    {
                        int n = w.Counts[i];
                        int level = n == 0 ? 0 : 1 + (int)Math.Round((n / (double)cmax) * 3);
                        level = Math.Min(level, 4);
                        var cell = new Border
                        {
                            Height = 28,
                            Margin = new Thickness(2),
                            CornerRadius = new CornerRadius(4),
                            Background = Heat[level],
                            BorderBrush = ResetMark,
                            BorderThickness = new Thickness(0, 0, 0, i == w.ResetIndex ? 3 : 0),
                            Child = new TextBlock
                            {
                                Text = n == 0 ? "" : n.ToString(),
                                Foreground = level >= 4 ? Brushes.White : Ink,
                                FontWeight = FontWeights.SemiBold,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        Add(g, cell, i + 1, row);
                    }
                    Add(g, new TextBlock
                    {
                        Text = w.TopIndex >= 0 ? StatsEngine.DayNames[w.TopIndex] + "요일" : "",
                        FontWeight = FontWeights.Bold, Foreground = Ink,
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
                    }, 8, row);
                    row++;
                }
                body.Children.Add(g);
                host.Children.Add(sec);
            }

            // ---- 이벤트 ----
            if (r.Events.Count > 0)
            {
                var sec = Section("이벤트 — 마감 며칠 전에 깼는지", out var body, "기한 있는 이벤트만 · 놓친 이벤트는 [확인 ✕]로 삭제할 때 기록돼요");
                var g = new Grid();
                double[] widths = { 150, 0, 90, 90, 110 };
                foreach (var w in widths)
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = w == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
                string[] heads = { "게임", "이벤트", "마감", "완료", "결과" };
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int i = 0; i < heads.Length; i++)
                    Add(g, new TextBlock { Text = heads[i], Foreground = Muted, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 6), HorizontalAlignment = i == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left }, i, 0);
                int row = 1;
                foreach (var e in r.Events.Take(30))
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    string result = e.Missed ? "놓침" : e.DaysLeft == 0 ? "마감 당일" : $"마감 {e.DaysLeft}일 전";
                    Add(g, Cell(e.Game, Ink), 0, row);
                    Add(g, Cell(string.IsNullOrWhiteSpace(e.Name) ? "이벤트" : e.Name, Ink), 1, row);
                    Add(g, Cell(e.EndDate.ToString("M'/'d"), Muted), 2, row);
                    Add(g, Cell(e.DoneAt?.ToString("M'/'d") ?? "—", Muted), 3, row);
                    var res = Cell(result, e.Missed ? Warn : Ink);
                    res.FontWeight = FontWeights.SemiBold;
                    res.HorizontalAlignment = HorizontalAlignment.Right;
                    Add(g, res, 4, row);
                    row++;
                }
                body.Children.Add(g);
                host.Children.Add(sec);
            }
        }

        private static void FillWeekdayPlay(StackPanel content, StatsResult r)
        {
            content.Children.Clear();
            bool avg = _weekdayMode == 0;
            double Val(double[] arr, int i) => avg ? (r.WeekdayDayCount[i] == 0 ? 0 : arr[i] / r.WeekdayDayCount[i]) : arr[i];

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            for (int i = 0; i < 7; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 52 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 7; i++)
            {
                string head = StatsEngine.DayNames[i] + (avg ? $" ({r.WeekdayDayCount[i]}일)" : "");
                Add(g, new TextBlock { Text = head, Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) }, i + 1, 0);
            }
            Add(g, new TextBlock { Text = avg ? "주 평균 합" : "합계", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }, 8, 0);

            // 게임 줄 색 진하기 기준 (전체 줄은 따로)
            double gameMax = 1;
            foreach (var w in r.WeekdayPlay)
                for (int i = 0; i < 7; i++) gameMax = Math.Max(gameMax, Val(w.RunSec, i));
            double actualMax = 1;
            for (int i = 0; i < 7; i++) actualMax = Math.Max(actualMax, Val(r.WeekdayActualSec, i));

            int row = 1;
            void AddRowCells(string name, bool bold, bool playOnly, double[] run, double[]? fg, double max)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var label = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                label.Children.Add(new TextBlock { Text = name, Foreground = Ink, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 120 });
                if (playOnly)
                    label.Children.Add(new TextBlock { Text = " · 숙제 없음", Foreground = Muted, FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center });
                Add(g, label, 0, row);
                double sum = 0;
                for (int i = 0; i < 7; i++)
                {
                    double v = Val(run, i);
                    sum += v;
                    int level = v < 60 ? 0 : 1 + Math.Min(3, (int)Math.Floor(v / max * 4 - 0.0001));
                    var cell = new Border
                    {
                        Height = 26,
                        Margin = new Thickness(2),
                        CornerRadius = new CornerRadius(4),
                        Background = Heat[Math.Clamp(level, 0, 4)],
                        Child = new TextBlock
                        {
                            Text = Short(v),
                            FontSize = 11.5,
                            Foreground = level >= 4 ? Brushes.White : Ink,
                            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        ToolTip = fg != null
                            ? $"{name} · {StatsEngine.DayNames[i]}요일 — 켜둔 {StatsEngine.FormatDuration(v)} / 화면 맨 앞 {StatsEngine.FormatDuration(Val(fg, i))}"
                            : $"{StatsEngine.DayNames[i]}요일 — 실제로 게임한 시간 {StatsEngine.FormatDuration(v)}"
                    };
                    Add(g, cell, i + 1, row);
                }
                Add(g, new TextBlock
                {
                    Text = StatsEngine.FormatDuration(sum),
                    Foreground = Ink,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                }, 8, row);
                row++;
            }

            AddRowCells("전체 (실제)", true, false, r.WeekdayActualSec, null, actualMax);
            foreach (var w in r.WeekdayPlay)
                AddRowCells(w.Game, false, w.PlayOnly, w.RunSec, w.FgSec, gameMax);
            content.Children.Add(g);
            if (avg)
                content.Children.Add(new TextBlock
                {
                    Text = "하루 평균 = 그 요일 합계 ÷ 기간 안 그 요일 수 (게임 안 한 날도 포함). '주 평균 합'은 한 주에 평균 얼마나 하는지예요.",
                    Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
                });
        }

        /// <summary>표 칸용 짧은 시간: "45분", "1.5시간"</summary>
        private static string Short(double sec)
        {
            if (sec < 60) return "";
            double m = sec / 60;
            if (m < 60) return $"{Math.Round(m)}분";
            double h = m / 60;
            return h < 10 ? $"{Math.Round(h, 1)}시간" : $"{Math.Round(h)}시간";
        }

        private static void FillOrder(StackPanel content, StatsResult r)
        {
            content.Children.Clear();
            if (_orderMode == 0)
            {
                // 평균 몇 번째로 하는지 (작을수록 먼저)
                int maxRank = Math.Max(1, (int)Math.Ceiling(r.DailyOrderAvg.Max(a => a.AvgRank)));
                int rank = 1;
                foreach (var a in r.DailyOrderAvg)
                {
                    var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                    Add(g, new TextBlock { Text = rank++.ToString(), FontWeight = FontWeights.Bold, Foreground = BarDark, HorizontalAlignment = HorizontalAlignment.Center }, 0);
                    Add(g, new TextBlock { Text = a.Game, Foreground = Ink, TextTrimming = TextTrimming.CharacterEllipsis }, 1);
                    // 먼저 할수록 막대가 길다
                    var track = new Grid { Height = 10, Background = Track, VerticalAlignment = VerticalAlignment.Center };
                    track.Children.Add(BarRect(BarMid, 1 - (a.AvgRank - 1) / maxRank));
                    Add(g, track, 2);
                    var val = new TextBlock { Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Right };
                    val.Inlines.Add(new System.Windows.Documents.Run($"평균 {Math.Round(a.AvgRank, 1)}번째") { Foreground = Ink, FontWeight = FontWeights.SemiBold });
                    val.Inlines.Add(new System.Windows.Documents.Run($" · {a.Days}일"));
                    Add(g, val, 3);
                    content.Children.Add(g);
                }
            }
            else
            {
                // 하루 순서가 통째로 같았던 날 수 (상위 5개)
                foreach (var seq in r.DailyOrderSeq)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                    var days = new TextBlock
                    {
                        Text = $"{seq.Days}일 ({Math.Round(seq.Days / (double)r.DailyOrderDays * 100)}%)",
                        Foreground = Muted,
                        Width = 90,
                        TextAlignment = TextAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(days, Dock.Right);
                    row.Children.Add(days);
                    var chain = new WrapPanel();
                    for (int i = 0; i < seq.Games.Count; i++)
                    {
                        if (i > 0) chain.Children.Add(new TextBlock { Text = "→", Foreground = Muted, Margin = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center });
                        chain.Children.Add(new Border
                        {
                            Background = Hex("#EDF2FF"),
                            BorderBrush = BarLight,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(7, 2, 7, 2),
                            Margin = new Thickness(0, 2, 0, 2),
                            Child = new TextBlock { Text = seq.Games[i], Foreground = Ink }
                        });
                    }
                    row.Children.Add(chain);
                    content.Children.Add(row);
                }
                if (r.DailyOrderSeq.Count > 0 && r.DailyOrderSeq[0].Days == 1)
                    content.Children.Add(new TextBlock
                    {
                        Text = "아직 같은 순서가 두 번 이상 나온 날이 없어요. 기록이 쌓이면 자주 하는 순서가 보여요.",
                        Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
                    });
            }
        }

        private static Border TodayCard(TodaySummary t)
        {
            var sp = new StackPanel();
            var head = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Ink };
            head.Inlines.Add(new System.Windows.Documents.Run($"오늘 ({t.Day:M'/'d} {StatsEngine.DayNames[StatsEngine.MondayIndex(t.Day.DayOfWeek)]})"));
            head.Inlines.Add(new System.Windows.Documents.Run("  · 새벽 4시 기준, 지금 켜져 있는 게임 포함") { FontSize = 11.5, FontWeight = FontWeights.Normal, Foreground = Muted });
            sp.Children.Add(head);

            var cards = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            cards.Children.Add(Card("PC 켠 시각",
                t.PcOnAt is DateTime on ? on.ToString("HH:mm") : "기록 없음",
                t.PcOnAt != null ? $"켜진 지 {StatsEngine.FormatDuration(t.OnFor.TotalSeconds)}" : "앱이 켜진 시각을 기준으로 해요"));
            string sub = $"화면 맨 앞 {StatsEngine.FormatDuration(t.FgSec)}";
            if (t.PcOnAt != null && t.OnFor.TotalSeconds > 60)
                sub += $" · 켜진 시간의 {Math.Min(100, Math.Round(t.ActualPlaySec / t.OnFor.TotalSeconds * 100))}%";
            cards.Children.Add(Card("실제로 게임한 시간", StatsEngine.FormatDuration(t.ActualPlaySec), sub));
            cards.Children.Add(Card("숙제", $"일일 {t.DailyDone}/{t.DailyTotal}", $"주간 {t.WeeklyDone}/{t.WeeklyTotal} · 오늘 체크 {t.ChecksToday}번"));
            sp.Children.Add(cards);

            if (t.Games.Count > 0)
            {
                foreach (var g in t.Games)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    row.Children.Add(new TextBlock { Text = g.Game, Foreground = Ink, Width = 170, TextTrimming = TextTrimming.CharacterEllipsis });
                    row.Children.Add(new TextBlock { Text = $"켜둔 {StatsEngine.FormatDuration(g.RunSec)} / 맨 앞 {StatsEngine.FormatDuration(g.FgSec)}", Foreground = Muted, Width = 220 });
                    if (g.Running)
                        row.Children.Add(new Border
                        {
                            Background = Hex("#EBFBEE"), BorderBrush = Hex("#2B8A3E"), BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 0, 5, 0),
                            Child = new TextBlock { Text = "실행 중", FontSize = 11, Foreground = Hex("#2B8A3E"), FontWeight = FontWeights.SemiBold }
                        });
                    if (g.PlayOnly)
                        row.Children.Add(new TextBlock { Text = "  숙제 없음", FontSize = 11, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center });
                    sp.Children.Add(row);
                }
            }
            else
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "오늘 켠 게임이 없어요. (게임 정보에 실행 파일을 등록해야 측정돼요)",
                    Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0)
                });
            }

            return new Border
            {
                BorderBrush = BarLight,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 14),
                Margin = new Thickness(0, 0, 10, 14),
                Background = Hex("#F8F9FF"),
                Child = sp
            };
        }

        // ================= 도우미 =================

        private static string Minutes(double min)
        {
            int m = (int)Math.Round(min);
            return m < 60 ? $"{m}분" : $"{m / 60}시간 {m % 60}분";
        }

        private static Border Card(string title, string value, string sub)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, Foreground = Muted, FontSize = 12 });
            sp.Children.Add(new TextBlock { Text = value, Foreground = Ink, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            return new Border
            {
                Width = 240,
                MinHeight = 96,
                BorderBrush = Border_,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 10, 10),
                Background = Brushes.White,
                Child = sp
            };
        }

        private static Border Section(string title, out StackPanel body, string? sub = null, double width = double.NaN)
        {
            body = new StackPanel();
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Ink });
            if (sub != null)
                sp.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            body.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(body);
            return new Border
            {
                Width = width,
                BorderBrush = Border_,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 14),
                Margin = new Thickness(0, 0, 10, 12),
                Background = Brushes.White,
                Child = sp
            };
        }

        private static UIElement Legend(Brush color, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
            sp.Children.Add(new Border { Width = 12, Height = 10, CornerRadius = new CornerRadius(2), Background = color, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = text, Foreground = Muted, FontSize = 11.5 });
            return sp;
        }

        /// <summary>비율(0~1)만큼 채운 막대 (Grid 안에서 별 비율 칸으로)</summary>
        private static UIElement BarRect(Brush brush, double ratio)
        {
            ratio = Math.Clamp(ratio, 0, 1);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });
            if (ratio > 0)
                g.Children.Add(new Border { Background = brush, CornerRadius = new CornerRadius(3) });
            return g;
        }

        private static Grid Row3(UIElement a, UIElement b, UIElement c, double aw, double cw)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(aw) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cw) });
            Add(g, a, 0);
            Add(g, b, 1);
            Add(g, c, 2);
            return g;
        }

        private static TextBlock Cell(string text, Brush fg) => new()
        {
            Text = text,
            Foreground = fg,
            Margin = new Thickness(4, 3, 4, 3),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        private static void Add(Grid g, UIElement el, int col, int row = 0)
        {
            Grid.SetColumn(el, col);
            Grid.SetRow(el, row);
            g.Children.Add(el);
        }

        private static Brush Hex(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            b.Freeze();
            return b;
        }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameHomework.Core;

namespace GameHomework
{
    /// <summary>
    /// 주기 컨텐츠 (N주마다) 등록 / 수정 창.
    /// 처음: "오늘부터 ○월 ○일까지" → 다음부터: "N주마다 ○요일까지"
    /// </summary>
    public sealed class CycleWindow : Window
    {
        private readonly HomeworkTask? _existing;
        private readonly ComboBox _cmbGame = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBox _txtName = new() { Width = 220, Padding = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Left };
        private readonly DatePicker _dpEnd = new() { Width = 130 };
        private readonly ComboBox _cmbWeeks = new() { Width = 70 };
        private readonly ComboBox _cmbDay = new() { Width = 80 };
        private readonly ComboBox _cmbScope = new() { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBlock _lblScope = MakeLabel("적용 범위");
        private readonly TextBlock _txtToday = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        private readonly TextBlock _txtPreview = new() { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        /// <summary>요일을 직접 고르기 전까지는 마감일 요일을 따라간다</summary>
        private bool _dayTouched;
        private bool _loading = true;

        public HomeworkTask? Result { get; private set; }
        public Game? ResultGame { get; private set; }

        /// <param name="game">미리 고를 게임 (수정이면 그 숙제의 게임)</param>
        /// <param name="existing">수정할 숙제 (null이면 새로 등록)</param>
        public CycleWindow(Game? game, HomeworkTask? existing)
        {
            _existing = existing;
            Title = existing == null ? "주기 컨텐츠 등록" : "주기 컨텐츠 수정";
            Width = 520;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
            FontSize = 12.5;
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/app.ico")); } catch { }

            var grid = new Grid { Margin = new Thickness(16, 14, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            void AddRow(UIElement label, UIElement content)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(label, row); grid.Children.Add(label);
                if (content is FrameworkElement fe) fe.Margin = new Thickness(0, 4, 0, 4);
                Grid.SetRow(content, row); Grid.SetColumn(content, 1); grid.Children.Add(content);
                row++;
            }

            // 게임
            if (existing == null)
            {
                foreach (var g in AppState.Data.Games.Where(g => g.Type != GameType.PlayOnly))
                    _cmbGame.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g });
                int idx = game == null ? -1 : AppState.Data.Games.Where(g => g.Type != GameType.PlayOnly).ToList().IndexOf(game);
                _cmbGame.SelectedIndex = idx >= 0 ? idx : (_cmbGame.Items.Count > 0 ? 0 : -1);
                _cmbGame.SelectionChanged += (s, e) => { UpdateScope(); UpdatePreview(); };
                AddRow(MakeLabel("게임"), _cmbGame);
            }
            else
            {
                AddRow(MakeLabel("게임"), new TextBlock { Text = game?.Name ?? "?", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            }

            AddRow(MakeLabel("이름"), _txtName);

            // 이번 회차: 오늘부터 [날짜]까지
            var first = new StackPanel { Orientation = Orientation.Horizontal };
            first.Children.Add(_txtToday);
            first.Children.Add(_dpEnd);
            first.Children.Add(new TextBlock { Text = "까지", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
            AddRow(MakeLabel("이번 회차"), first);

            // 다음부터: [N]주마다 [요일]까지
            for (int w = 1; w <= 12; w++) _cmbWeeks.Items.Add(new ComboBoxItem { Content = w + "주", Tag = w });
            foreach (var d in ResetCalculator.WeekOrder)
                _cmbDay.Items.Add(new ComboBoxItem { Content = ResetCalculator.DayName(d) + "요일", Tag = d });
            var next = new StackPanel { Orientation = Orientation.Horizontal };
            next.Children.Add(_cmbWeeks);
            next.Children.Add(new TextBlock { Text = "마다", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 10, 0) });
            next.Children.Add(_cmbDay);
            next.Children.Add(new TextBlock { Text = "까지", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 12, 0) });
            foreach (var w in new[] { 4, 6 })
            {
                var b = new Button { Content = w + "주", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 4, 0), Tag = w };
                b.Click += (s, e) => SelectWeeks((int)((Button)s).Tag);
                next.Children.Add(b);
            }
            AddRow(MakeLabel("다음부터"), next);

            // RPG: 공통 / 캐릭터마다
            _cmbScope.Items.Add(new ComboBoxItem { Content = "원정대 공통 (체크 1개)" });
            _cmbScope.Items.Add(new ComboBoxItem { Content = "캐릭터마다" });
            AddRow(_lblScope, _cmbScope);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(_txtPreview, row); Grid.SetColumnSpan(_txtPreview, 2); grid.Children.Add(_txtPreview);
            row++;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 6, 16, 14) };
            var ok = new Button { Content = existing == null ? "등록" : "저장", Padding = new Thickness(18, 3, 18, 3), Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            ok.Click += (s, e) => Accept();
            var cancel = new Button { Content = "취소", Padding = new Thickness(18, 3, 18, 3), IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(grid);
            Content = root;

            // ---- 값 채우기 ----
            var now = DateTime.Now;
            if (existing != null && game != null)
            {
                _txtName.Text = existing.Name;
                // 이미 쓰던 주기 컨텐츠면 "지금 회차 마감일"을 보여준다
                _dpEnd.SelectedDate = existing.Kind == ResetKind.Cycle && existing.CycleFirstEnd != null
                    ? ResetCalculator.CycleCurrentEnd(existing, game, now)
                    : DateTime.Today.AddDays(13);
                SelectWeeks(existing.Kind == ResetKind.Cycle ? ResetCalculator.CycleWeeksOf(existing) : 4);
                SelectDay(existing.Kind == ResetKind.Cycle ? existing.CycleEndDay : _dpEnd.SelectedDate!.Value.DayOfWeek);
                _dayTouched = existing.Kind == ResetKind.Cycle && existing.CycleEndDay != _dpEnd.SelectedDate!.Value.DayOfWeek;
                _cmbScope.SelectedIndex = existing.Scope == TaskScope.Common ? 0 : 1;
            }
            else
            {
                _txtName.Text = "";
                _dpEnd.SelectedDate = DateTime.Today.AddDays(13);
                SelectWeeks(4);
                SelectDay(_dpEnd.SelectedDate.Value.DayOfWeek);
                _cmbScope.SelectedIndex = 1;
            }
            _loading = false;

            _dpEnd.SelectedDateChanged += (s, e) =>
            {
                if (!_dayTouched && _dpEnd.SelectedDate is DateTime d) { _loading = true; SelectDay(d.DayOfWeek); _loading = false; }
                UpdatePreview();
            };
            _cmbDay.SelectionChanged += (s, e) => { if (!_loading) _dayTouched = true; UpdatePreview(); };
            _cmbWeeks.SelectionChanged += (s, e) => UpdatePreview();

            _fixedGame = existing != null ? game : null;
            UpdateScope();
            UpdatePreview();
            Loaded += (s, e) => { _txtName.Focus(); _txtName.SelectAll(); };
        }

        private readonly Game? _fixedGame;
        private Game? SelectedGame => _fixedGame ?? (_cmbGame.SelectedItem as ComboBoxItem)?.Tag as Game;
        private int Weeks => (_cmbWeeks.SelectedItem as ComboBoxItem)?.Tag is int w ? w : 4;
        private DayOfWeek EndDay => (_cmbDay.SelectedItem as ComboBoxItem)?.Tag is DayOfWeek d ? d : DayOfWeek.Monday;

        private static TextBlock MakeLabel(string text) => new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44))
        };

        private void SelectWeeks(int w)
        {
            w = Math.Clamp(w, 1, 12);
            for (int i = 0; i < _cmbWeeks.Items.Count; i++)
                if ((_cmbWeeks.Items[i] as ComboBoxItem)?.Tag is int x && x == w) _cmbWeeks.SelectedIndex = i;
        }

        private void SelectDay(DayOfWeek d)
        {
            for (int i = 0; i < _cmbDay.Items.Count; i++)
                if ((_cmbDay.Items[i] as ComboBoxItem)?.Tag is DayOfWeek x && x == d) _cmbDay.SelectedIndex = i;
        }

        private void UpdateScope()
        {
            bool rpg = SelectedGame?.Type == GameType.Rpg && SelectedGame.Characters.Count > 0;
            _lblScope.Visibility = _cmbScope.Visibility = rpg ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePreview()
        {
            var game = SelectedGame;
            if (game == null || _dpEnd.SelectedDate is not DateTime end)
            {
                _txtPreview.Text = "게임과 마감일을 골라 주세요.";
                return;
            }
            var t = game.GetResetTimeOfDay();
            var today = ResetCalculator.GameDate(DateTime.Now, t);
            _txtToday.Text = _existing?.Kind == ResetKind.Cycle ? "지금 회차는" : $"오늘({today:M'/'d})부터";
            var probe = new HomeworkTask { Kind = ResetKind.Cycle, CycleFirstEnd = end.Date, CycleWeeks = Weeks, CycleEndDay = EndDay };
            var ends = ResetCalculator.CycleUpcomingEnds(probe, end.Date, 4);
            string list = string.Join("  →  ", ends.Select(d => $"{d:M'/'d}({ResetCalculator.DayName(d.DayOfWeek)})"));
            _txtPreview.Text =
                $"마감 예정: {list}  …\n" +
                $"마감일 다음 날 {t:hh\\:mm}({game.Name} 초기화 시각)에 새 회차가 시작돼요. " +
                $"마감 {ResetCalculator.CycleWarnDays}일 전(D-{ResetCalculator.CycleWarnDays})부터 주기 컨텐츠 위젯에 주황색으로 표시됩니다.";
        }

        private void Accept()
        {
            var game = SelectedGame;
            if (game == null)
            {
                MessageBox.Show(this, "게임을 골라 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var name = _txtName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "이름을 입력해 주세요. (예: 허구 이야기, 시유 방어전)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                _txtName.Focus();
                return;
            }
            if (_dpEnd.SelectedDate is not DateTime end)
            {
                MessageBox.Show(this, "이번 회차 마감일을 골라 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var now = DateTime.Now;
            if (end.Date < ResetCalculator.GameDate(now, game.GetResetTimeOfDay()))
            {
                MessageBox.Show(this, "마감일이 이미 지났어요. 오늘 이후 날짜를 골라 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool isNew = _existing == null;
            var task = _existing ?? new HomeworkTask { Kind = ResetKind.Cycle };
            task.Name = name;
            ResetCalculator.ApplyCycle(task, game, end, Weeks, EndDay, now, isNew || _existing!.Kind != ResetKind.Cycle);
            if (game.Type == GameType.Rpg && game.Characters.Count > 0)
                task.Scope = _cmbScope.SelectedIndex == 0 ? TaskScope.Common : TaskScope.PerCharacter;
            else
                task.Scope = TaskScope.Common;
            if (isNew) game.Tasks.Add(task);
            AppState.Save();

            Result = task;
            ResultGame = game;
            DialogResult = true;
        }
    }
}

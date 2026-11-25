using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameHomework.Core;
using Microsoft.Win32;

namespace GameHomework
{
    public partial class SettingsWindow : Window
    {
        /// <summary>컨트롤 값을 코드에서 채우는 중이면 true (변경 이벤트 무시)</summary>
        private bool _loading = true;
        private Game? _game;
        private HomeworkTask? _task;
        private readonly List<CheckBox> _resetDayChecks = new();
        private readonly List<CheckBox> _hiddenDayChecks = new();
        private readonly Brush _normalBorder;
        private static readonly Brush ErrorBorder = Brushes.IndianRed;
        private static readonly Brush WarnText = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly Brush GrayText = Brushes.Gray;
        private static readonly Brush MissedText = new SolidColorBrush(Color.FromRgb(0x9A, 0x60, 0x60));

        private static List<Game> Games => AppState.Data.Games;

        public SettingsWindow()
        {
            InitializeComponent();
            Title = $"게임 숙제 설정 — v{App.Version}";
            _normalBorder = TxtResetTime.BorderBrush;

            foreach (var d in ResetCalculator.WeekOrder)
            {
                CmbWeeklyDay.Items.Add(new ComboBoxItem { Content = ResetCalculator.DayName(d) + "요일", Tag = d });

                var rc = DayCheck(d);
                rc.Click += ResetDay_Click;
                _resetDayChecks.Add(rc);
                PnlResetDays.Children.Add(rc);

                var hc = DayCheck(d);
                hc.Click += HiddenDay_Click;
                _hiddenDayChecks.Add(hc);
                PnlHiddenDays.Children.Add(hc);
            }

            _loading = false;

            RefreshGameList(Games.FirstOrDefault());
            InitEventsTab();
            RefreshCycleList(null);
            InitPickupTab();
            InitWidgetTab();

            AppState.Changed += OnDataChanged;
            Activated += (s, e) => RefreshWidgetChecks(); // 트레이에서 위젯을 켜고 끈 경우 반영
            Closed += (s, e) => AppState.Changed -= OnDataChanged;
        }

        /// <summary>이벤트 탭에서 "기한 있는 이벤트" / "무기한 목표" 미리 선택</summary>
        public void PresetEventType(bool goal)
        {
            RdoNoDeadline.IsChecked = goal;
            RdoDeadline.IsChecked = !goal;
            UpdateEventTypeUi();
            RefreshEventGames();
            TxtEventName.Focus();
        }

        public void SelectTab(int index)
        {
            if (index >= 0 && index < Tabs.Items.Count) Tabs.SelectedIndex = index;
            if (index == 1) TxtEventName.Focus();
            if (index == PickupTabIndex) { RefreshPickupGames(); TxtPickupName.Focus(); }
            if (index == CycleTabIndex) RefreshCycleList(SelectedCycle()?.Task.Id);
        }

        public const int CycleTabIndex = 2;
        public const int PickupTabIndex = 3;
        public const int WidgetTabIndex = 5;

        /// <summary>바깥(위젯 ＋, 트레이)에서 주기 컨텐츠를 등록했을 때 목록 다시 읽기</summary>
        public void ReloadTasks() => RefreshCycleList(SelectedCycle()?.Task.Id);

        /// <summary>위젯에서 이벤트를 체크하는 등 바깥에서 바뀐 경우 이벤트 목록만 갱신</summary>
        private void OnDataChanged()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshEventList(SelectedEvent()?.Id);
                RefreshPickupList(SelectedPickup()?.Id); // 끝난 픽업 자동 삭제 반영
                RefreshCycleList(SelectedCycle()?.Task.Id); // 위젯에서 체크하면 완료 표시
            }));
        }

        private static CheckBox DayCheck(DayOfWeek d) => new CheckBox
        {
            Content = ResetCalculator.DayName(d),
            Tag = d,
            Margin = new Thickness(0, 2, 14, 2)
        };

        private static void Save() => AppState.Save();

        private void Guide_Click(object sender, RoutedEventArgs e) => App.OpenGuide();

        private static bool Confirm(string message) =>
            MessageBox.Show(message, "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private static void RemoveCompletions(Func<string, bool> match)
        {
            foreach (var key in AppState.Data.Completions.Keys.Where(match).ToList())
                AppState.Data.Completions.Remove(key);
        }

        private static void Swap<T>(List<T> list, int i, int j)
        {
            (list[i], list[j]) = (list[j], list[i]);
        }

        // =====================================================================
        // 게임
        // =====================================================================

        private static string GameLabel(Game g) => g.Type switch
        {
            GameType.Rpg => $"{g.Name}   (RPG · {g.Characters.Count}캐릭)",
            GameType.PlayOnly => $"{g.Name}   (플레이 시간만)",
            _ => g.Name
        };

        private static int TypeIndex(GameType t) => t switch { GameType.Rpg => 1, GameType.PlayOnly => 2, _ => 0 };
        private static GameType TypeFromIndex(int i) => i switch { 1 => GameType.Rpg, 2 => GameType.PlayOnly, _ => GameType.Mobile };

        /// <summary>분류에 따라 캐릭터/숙제 칸 보이기</summary>
        private void UpdateGameTypeVisibility()
        {
            var type = _game?.Type ?? GameType.Mobile;
            GrpChars.Visibility = type == GameType.Rpg ? Visibility.Visible : Visibility.Collapsed;
            var tasks = type == GameType.PlayOnly ? Visibility.Collapsed : Visibility.Visible;
            GrpTasks.Visibility = tasks;
            GrpTask.Visibility = tasks;
        }

        private void RefreshGameList(Game? select)
        {
            _loading = true;
            LstGames.Items.Clear();
            foreach (var g in Games)
                LstGames.Items.Add(new ListBoxItem { Content = GameLabel(g), Tag = g });
            _loading = false;

            int idx = select == null ? -1 : Games.IndexOf(select);
            if (idx >= 0)
            {
                LstGames.SelectedIndex = idx;
                LstGames.ScrollIntoView(LstGames.SelectedItem);
                if (!ReferenceEquals(_game, select)) LoadGame(select);
            }
            else
            {
                LoadGame(null);
            }
        }

        private void UpdateGameLabel()
        {
            if (_game == null) return;
            foreach (ListBoxItem item in LstGames.Items)
                if (ReferenceEquals(item.Tag, _game)) item.Content = GameLabel(_game);
        }

        private void LstGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            LoadGame((LstGames.SelectedItem as ListBoxItem)?.Tag as Game);
        }

        private void LoadGame(Game? game)
        {
            _game = game;
            GamePanel.IsEnabled = game != null;

            _loading = true;
            TxtGameName.Text = game?.Name ?? "";
            CmbGameType.SelectedIndex = TypeIndex(game?.Type ?? GameType.Mobile);
            TxtResetTime.Text = game?.ResetTime ?? "";
            TxtResetTime.BorderBrush = _normalBorder;
            CmbWeeklyDay.SelectedIndex = game == null ? 0 : Array.IndexOf(ResetCalculator.WeekOrder, game.WeeklyResetDay);
            _loading = false;

            UpdateGameTypeVisibility();
            RefreshExeList(null);
            RefreshCharList(null);
            RefreshTaskList(game?.Tasks.FirstOrDefault());
        }

        private void AddGame_Click(object sender, RoutedEventArgs e)
        {
            var g = new Game { Name = "새 게임", Type = GameType.Mobile, ResetTime = "04:00", WeeklyResetDay = DayOfWeek.Monday };
            g.Tasks.Add(new HomeworkTask { Name = "일퀘", Kind = ResetKind.Daily, Scope = TaskScope.Common });
            g.Tasks.Add(new HomeworkTask { Name = "주간퀘", Kind = ResetKind.Weekly, Scope = TaskScope.Common });
            Games.Add(g);
            Save();
            RefreshGameList(g);
            RefreshEventGames();
            TxtGameName.Focus();
            TxtGameName.SelectAll();
        }

        private void DeleteGame_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            if (!Confirm($"'{_game.Name}'을(를) 삭제할까요?\n숙제, 체크 기록, 이벤트, 픽업도 함께 삭제됩니다.")) return;
            var g = _game;
            int idx = Games.IndexOf(g);
            var taskIds = g.Tasks.Select(t => t.Id.ToString()).ToList();
            RemoveCompletions(k => taskIds.Any(id => k.StartsWith(id, StringComparison.Ordinal)));
            AppState.Data.Events.RemoveAll(ev => ev.GameId == g.Id);
            AppState.Data.Pickups.RemoveAll(p => p.GameId == g.Id);
            Games.Remove(g);
            Save();
            RefreshGameList(Games.Count == 0 ? null : Games[Math.Min(idx, Games.Count - 1)]);
            RefreshEventGames();
            RefreshEventList(null);
            RefreshPickupList(null);
            RefreshCycleList(null);
        }

        private void MoveGameUp_Click(object sender, RoutedEventArgs e) => MoveGame(-1);
        private void MoveGameDown_Click(object sender, RoutedEventArgs e) => MoveGame(1);

        private void MoveGame(int dir)
        {
            if (_game == null) return;
            int i = Games.IndexOf(_game), j = i + dir;
            if (i < 0 || j < 0 || j >= Games.Count) return;
            Swap(Games, i, j);
            Save();
            RefreshGameList(_game);
            RefreshEventGames();
        }

        private void TxtGameName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _game == null) return;
            _game.Name = TxtGameName.Text;
            Save();
            UpdateGameLabel();
        }

        private void TxtGameName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            if (string.IsNullOrWhiteSpace(_game.Name))
            {
                _game.Name = "이름 없는 게임";
                _loading = true;
                TxtGameName.Text = _game.Name;
                _loading = false;
                Save();
                UpdateGameLabel();
            }
            RefreshEventGames();
        }

        private void CmbGameType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _game == null) return;
            var type = TypeFromIndex(CmbGameType.SelectedIndex);
            if (_game.Type == type) return;
            _game.Type = type;
            Save();
            UpdateGameLabel();
            UpdateGameTypeVisibility();
            RefreshTaskList(_task);
            RefreshEventGames();
        }

        private void TxtResetTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _game == null) return;
            if (TimeParser.TryParse(TxtResetTime.Text, out var t))
            {
                TxtResetTime.BorderBrush = _normalBorder;
                _game.ResetTime = t.ToString(@"hh\:mm");
                Save();
                RefreshTaskLabels();
            }
            else
            {
                TxtResetTime.BorderBrush = ErrorBorder;
            }
        }

        private void CmbWeeklyDay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _game == null) return;
            if (CmbWeeklyDay.SelectedItem is ComboBoxItem { Tag: DayOfWeek d })
            {
                _game.WeeklyResetDay = d;
                Save();
                RefreshTaskLabels();
            }
        }

        // =====================================================================
        // 실행 파일 (플레이 시간 측정)
        // =====================================================================

        private void RefreshExeList(string? select)
        {
            LstExe.Items.Clear();
            if (_game == null) return;
            foreach (var exe in _game.ExeNames)
            {
                LstExe.Items.Add(exe);
                if (select != null && string.Equals(exe, select, StringComparison.OrdinalIgnoreCase)) LstExe.SelectedItem = exe;
            }
        }

        private void AddExeName(string raw)
        {
            if (_game == null) return;
            var name = raw.Trim().Trim('"');
            name = Path.GetFileName(name); // 전체 경로를 붙여 넣어도 파일 이름만
            if (name.Length == 0) return;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";
            var norm = PlayTracker.NormalizeExe(name);
            var owner = Games.FirstOrDefault(g => g != _game && g.ExeNames.Any(x => PlayTracker.NormalizeExe(x) == norm));
            if (owner != null)
            {
                MessageBox.Show($"'{name}'은(는) 이미 '{owner.Name}'에 등록되어 있습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!_game.ExeNames.Any(x => PlayTracker.NormalizeExe(x) == norm))
            {
                _game.ExeNames.Add(name);
                Save();
            }
            RefreshExeList(name);
        }

        private void PickExe_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            var dlg = new ProcessPickerWindow { Owner = this };
            if (dlg.ShowDialog() == true && dlg.SelectedExe != null) AddExeName(dlg.SelectedExe);
        }

        private void AddExe_Click(object sender, RoutedEventArgs e)
        {
            AddExeName(TxtExe.Text);
            TxtExe.Clear();
        }

        private void TxtExe_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            AddExe_Click(sender, e);
            e.Handled = true;
        }

        private void DeleteExe_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null || LstExe.SelectedItem is not string exe) return;
            _game.ExeNames.Remove(exe);
            Save();
            RefreshExeList(null);
        }

        // =====================================================================
        // 캐릭터
        // =====================================================================

        private Character? SelectedChar => (LstChars.SelectedItem as ListBoxItem)?.Tag as Character;

        private void RefreshCharList(Character? select)
        {
            _loading = true;
            LstChars.Items.Clear();
            if (_game != null)
                foreach (var c in _game.Characters)
                    LstChars.Items.Add(new ListBoxItem { Content = c.Name, Tag = c });
            if (select != null && _game != null)
                LstChars.SelectedIndex = _game.Characters.IndexOf(select);
            _loading = false;
        }

        private void LstChars_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (SelectedChar is Character c) TxtCharName.Text = c.Name;
        }

        private void AddChar_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            var name = TxtCharName.Text.Trim();
            if (name.Length == 0) { TxtCharName.Focus(); return; }
            var c = new Character { Name = name };
            _game.Characters.Add(c);
            Save();
            TxtCharName.Clear();
            RefreshCharList(c);
            UpdateGameLabel();
            LoadTask(_task);
        }

        private void RenameChar_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedChar is not Character c) return;
            var name = TxtCharName.Text.Trim();
            if (name.Length == 0) return;
            c.Name = name;
            Save();
            RefreshCharList(c);
            LoadTask(_task);
        }

        private void DeleteChar_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null || SelectedChar is not Character c) return;
            if (!Confirm($"캐릭터 '{c.Name}'을(를) 삭제할까요?\n이 캐릭터의 체크 기록도 삭제됩니다.")) return;
            _game.Characters.Remove(c);
            string suffix = ":" + c.Id;
            RemoveCompletions(k => k.EndsWith(suffix, StringComparison.Ordinal));
            foreach (var t in _game.Tasks)
            {
                t.CharacterIds.Remove(c.Id);
                t.CharacterLabels.Remove(c.Id);
            }
            Save();
            TxtCharName.Clear();
            RefreshCharList(null);
            UpdateGameLabel();
            LoadTask(_task);
        }

        private void MoveCharUp_Click(object sender, RoutedEventArgs e) => MoveChar(-1);
        private void MoveCharDown_Click(object sender, RoutedEventArgs e) => MoveChar(1);

        private void MoveChar(int dir)
        {
            if (_game == null || SelectedChar is not Character c) return;
            var list = _game.Characters;
            int i = list.IndexOf(c), j = i + dir;
            if (i < 0 || j < 0 || j >= list.Count) return;
            Swap(list, i, j);
            Save();
            RefreshCharList(c);
            LoadTask(_task);
        }

        // =====================================================================
        // 숙제
        // =====================================================================

        private string TaskLabel(HomeworkTask t)
        {
            if (_game == null) return t.Name;
            var parts = new List<string> { ResetCalculator.DescribeRule(t, _game) };
            if (_game.Type == GameType.Rpg)
            {
                if (t.Scope == TaskScope.Common) parts.Add("공통");
                else if (t.CharacterIds.Count > 0)
                    parts.Add(string.Join(",", ResetCalculator.ApplicableCharacters(t, _game).Select(c => c.Name)) + "만");
                else parts.Add("캐릭터별");
            }
            if (_game.Type == GameType.Rpg && t.Scope == TaskScope.PerCharacter && t.CharacterLabels.Count > 0)
            {
                var named = _game.Characters
                    .Where(c => t.CharacterLabels.TryGetValue(c.Id, out var v) && !string.IsNullOrWhiteSpace(v))
                    .Select(c => $"{c.Name}={t.CharacterLabels[c.Id]}");
                parts.Add(string.Join(", ", named));
            }
            if (t.HiddenDays.Count > 0)
                parts.Add(string.Join("·", ResetCalculator.OrderDays(t.HiddenDays).Select(ResetCalculator.DayName)) + " 숨김");
            return $"{t.Name}      —  {string.Join("  ·  ", parts)}";
        }

        /// <summary>게임/숙제 탭에 보이는 숙제 (주기 컨텐츠는 따로 탭에서 관리)</summary>
        private List<HomeworkTask> ListedTasks => _game == null
            ? new List<HomeworkTask>()
            : _game.Tasks.Where(t => t.Kind != ResetKind.Cycle).ToList();

        private void RefreshTaskList(HomeworkTask? select)
        {
            var tasks = ListedTasks;
            _loading = true;
            LstTasks.Items.Clear();
            foreach (var t in tasks)
                LstTasks.Items.Add(new ListBoxItem { Content = TaskLabel(t), Tag = t });
            _loading = false;

            int idx = select == null ? -1 : tasks.IndexOf(select);
            if (idx < 0 && tasks.Count > 0) idx = 0;
            if (idx >= 0)
            {
                _loading = true;
                LstTasks.SelectedIndex = idx;
                _loading = false;
                LstTasks.ScrollIntoView(LstTasks.SelectedItem);
                LoadTask(tasks[idx]);
            }
            else
            {
                LoadTask(null);
            }
        }

        private void RefreshTaskLabels()
        {
            foreach (ListBoxItem item in LstTasks.Items)
                if (item.Tag is HomeworkTask t) item.Content = TaskLabel(t);
        }

        private void UpdateTaskLabel()
        {
            if (_task == null) return;
            foreach (ListBoxItem item in LstTasks.Items)
                if (ReferenceEquals(item.Tag, _task)) item.Content = TaskLabel(_task);
        }

        private void LstTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            LoadTask((LstTasks.SelectedItem as ListBoxItem)?.Tag as HomeworkTask);
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            var t = new HomeworkTask
            {
                Name = "새 숙제",
                Kind = ResetKind.Daily,
                Scope = _game.Type == GameType.Rpg ? TaskScope.PerCharacter : TaskScope.Common
            };
            int insertAt = _task != null ? _game.Tasks.IndexOf(_task) + 1 : _game.Tasks.Count;
            _game.Tasks.Insert(Math.Clamp(insertAt, 0, _game.Tasks.Count), t);
            Save();
            RefreshTaskList(t);
            TxtTaskName.Focus();
            TxtTaskName.SelectAll();
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null || _task == null) return;
            if (!Confirm($"숙제 '{_task.Name}'을(를) 삭제할까요?")) return;
            var t = _task;
            int idx = ListedTasks.IndexOf(t);
            string id = t.Id.ToString();
            RemoveCompletions(k => k.StartsWith(id, StringComparison.Ordinal));
            _game.Tasks.Remove(t);
            Save();
            var left = ListedTasks;
            RefreshTaskList(left.Count == 0 ? null : left[Math.Min(idx, left.Count - 1)]);
        }

        private void MoveTaskUp_Click(object sender, RoutedEventArgs e) => MoveTask(-1);
        private void MoveTaskDown_Click(object sender, RoutedEventArgs e) => MoveTask(1);

        private void MoveTask(int dir)
        {
            if (_game == null || _task == null) return;
            // 보이는 숙제끼리 자리 바꾸기 (주기 컨텐츠는 건너뜀)
            var shown = ListedTasks;
            int a = shown.IndexOf(_task), b = a + dir;
            if (a < 0 || b < 0 || b >= shown.Count) return;
            var list = _game.Tasks;
            Swap(list, list.IndexOf(shown[a]), list.IndexOf(shown[b]));
            Save();
            RefreshTaskList(_task);
        }

        private static int KindIndex(ResetKind k) => k switch
        {
            ResetKind.Daily => 0,
            ResetKind.Weekly => 1,
            ResetKind.Weekdays => 2,
            _ => 3
        };

        private static ResetKind KindFromIndex(int i) => i switch
        {
            1 => ResetKind.Weekly,
            2 => ResetKind.Weekdays,
            3 => ResetKind.Monthly,
            _ => ResetKind.Daily
        };

        private void LoadTask(HomeworkTask? task)
        {
            _task = task;
            GrpTask.IsEnabled = task != null;

            _loading = true;
            TxtTaskName.Text = task?.Name ?? "";
            CmbTaskKind.SelectedIndex = task == null ? 0 : KindIndex(task.Kind);
            foreach (var cb in _resetDayChecks)
                cb.IsChecked = task != null && task.ResetDays.Contains((DayOfWeek)cb.Tag);
            foreach (var cb in _hiddenDayChecks)
                cb.IsChecked = task != null && task.HiddenDays.Contains((DayOfWeek)cb.Tag);
            TxtMonthDay.Text = (task?.MonthDay ?? 1).ToString();
            TxtMonthDay.BorderBrush = _normalBorder;
            CmbScope.SelectedIndex = task?.Scope == TaskScope.Common ? 0 : 1;
            _loading = false;

            UpdateTaskFieldVisibility();
            BuildTaskCharControls();
        }

        private void UpdateTaskFieldVisibility()
        {
            var kind = _task?.Kind ?? ResetKind.Daily;
            bool rpg = _game?.Type == GameType.Rpg;
            bool perChar = rpg && _task?.Scope == TaskScope.PerCharacter && (_game?.Characters.Count ?? 0) > 0;

            SetVisible(kind == ResetKind.Weekdays, LblResetDays, PnlResetDays);
            SetVisible(kind == ResetKind.Monthly, LblMonthDay, PnlMonthDay);
            SetVisible(rpg, LblScope, CmbScope);
            SetVisible(perChar, LblTaskChars, PnlTaskCharsBox, LblCharLabels, PnlCharLabelsBox);
        }

        private static void SetVisible(bool visible, params UIElement[] elements)
        {
            foreach (var el in elements) el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BuildTaskCharControls()
        {
            PnlTaskChars.Children.Clear();
            GridCharLabels.Children.Clear();
            GridCharLabels.RowDefinitions.Clear();
            GridCharLabels.ColumnDefinitions.Clear();
            if (_game == null || _task == null || _game.Type != GameType.Rpg || _task.Scope != TaskScope.PerCharacter)
                return;

            var task = _task;
            var game = _game;

            foreach (var ch in game.Characters)
            {
                var cb = new CheckBox
                {
                    Content = ch.Name,
                    Tag = ch,
                    Margin = new Thickness(0, 2, 14, 2),
                    IsChecked = task.CharacterIds.Count == 0 || task.CharacterIds.Contains(ch.Id)
                };
                cb.Click += (s, e) =>
                {
                    var checkedIds = PnlTaskChars.Children.OfType<CheckBox>()
                        .Where(x => x.IsChecked == true)
                        .Select(x => ((Character)x.Tag).Id)
                        .ToList();
                    if (checkedIds.Count == 0)
                    {
                        MessageBox.Show("최소 한 캐릭터는 선택해야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        cb.IsChecked = true;
                        return;
                    }
                    task.CharacterIds = checkedIds.Count == game.Characters.Count ? new List<Guid>() : checkedIds;
                    Save();
                    UpdateTaskLabel();
                };
                PnlTaskChars.Children.Add(cb);
            }

            GridCharLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            GridCharLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            for (int i = 0; i < game.Characters.Count; i++)
            {
                var ch = game.Characters[i];
                GridCharLabels.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var lbl = new TextBlock { Text = ch.Name, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(lbl, i);
                GridCharLabels.Children.Add(lbl);

                var tb = new TextBox
                {
                    Text = task.CharacterLabels.TryGetValue(ch.Id, out var l) ? l : "",
                    Padding = new Thickness(3),
                    Margin = new Thickness(0, 2, 0, 2)
                };
                tb.TextChanged += (s, e) =>
                {
                    var v = tb.Text.Trim();
                    if (v.Length == 0) task.CharacterLabels.Remove(ch.Id);
                    else task.CharacterLabels[ch.Id] = v;
                    Save();
                };
                Grid.SetRow(tb, i);
                Grid.SetColumn(tb, 1);
                GridCharLabels.Children.Add(tb);
            }
        }

        private void TxtTaskName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _task == null) return;
            _task.Name = TxtTaskName.Text;
            Save();
            UpdateTaskLabel();
        }

        private void TxtTaskName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_task == null || !string.IsNullOrWhiteSpace(_task.Name)) return;
            _task.Name = "이름 없는 숙제";
            _loading = true;
            TxtTaskName.Text = _task.Name;
            _loading = false;
            Save();
            UpdateTaskLabel();
        }

        private void CmbTaskKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _task == null || _game == null) return;
            _task.Kind = KindFromIndex(CmbTaskKind.SelectedIndex);
            if (_task.Kind == ResetKind.Weekdays && _task.ResetDays.Count == 0)
            {
                _task.ResetDays.Add(_game.WeeklyResetDay);
                _loading = true;
                foreach (var cb in _resetDayChecks) cb.IsChecked = _task.ResetDays.Contains((DayOfWeek)cb.Tag);
                _loading = false;
            }
            Save();
            UpdateTaskFieldVisibility();
            UpdateTaskLabel();
        }

        private void ResetDay_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _task == null) return;
            var days = _resetDayChecks.Where(c => c.IsChecked == true).Select(c => (DayOfWeek)c.Tag).ToList();
            if (days.Count == 0)
            {
                MessageBox.Show("최소 한 요일은 선택해야 합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is CheckBox cb) cb.IsChecked = true;
                return;
            }
            _task.ResetDays = days;
            Save();
            UpdateTaskLabel();
        }

        private void HiddenDay_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _task == null) return;
            var days = _hiddenDayChecks.Where(c => c.IsChecked == true).Select(c => (DayOfWeek)c.Tag).ToList();
            if (days.Count == 7)
            {
                MessageBox.Show("모든 요일을 숨길 수는 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is CheckBox cb) cb.IsChecked = false;
                return;
            }
            _task.HiddenDays = days;
            Save();
            UpdateTaskLabel();
        }

        private void TxtMonthDay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _task == null) return;
            if (int.TryParse(TxtMonthDay.Text.Trim(), out int d) && d >= 1 && d <= 31)
            {
                TxtMonthDay.BorderBrush = _normalBorder;
                _task.MonthDay = d;
                Save();
                UpdateTaskLabel();
            }
            else
            {
                TxtMonthDay.BorderBrush = ErrorBorder;
            }
        }

        private void CmbScope_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _task == null) return;
            _task.Scope = CmbScope.SelectedIndex == 0 ? TaskScope.Common : TaskScope.PerCharacter;
            Save();
            UpdateTaskFieldVisibility();
            BuildTaskCharControls();
            UpdateTaskLabel();
        }

        // =====================================================================
        // 이벤트
        // =====================================================================

        private bool IsGoalMode => RdoNoDeadline.IsChecked == true;

        private void InitEventsTab()
        {
            RefreshEventGames();
            DpEventEnd.SelectedDate = DateTime.Today.AddDays(7);
            UpdateEventTypeUi();
            RefreshEventList(null);
        }

        private void EventType_Click(object sender, RoutedEventArgs e)
        {
            UpdateEventTypeUi();
            RefreshEventGames();
        }

        private void UpdateEventTypeUi()
        {
            bool goal = IsGoalMode;
            DpEventEnd.IsEnabled = !goal;
            PnlQuickDates.IsEnabled = !goal;
            TxtEventHint.Text = goal
                ? "무기한 목표는 기한 없이 계속 표시되고, 완료 체크하면 그 게임의 다음 초기화 시각에 자동으로 삭제됩니다. 모든 게임에 등록할 수 있어요."
                : "마감일 당일(게임 초기화 시각 기준)까지 표시, 7일 이하로 남으면 빨간 테두리. 끝나면 완료한 건 자동 삭제, 못 한 건 [놓침]으로 남아서 확인 후 삭제합니다. (모바일 게임만)";
        }

        /// <summary>기한 이벤트 → 모바일 게임만, 무기한 목표 → 모든 게임</summary>
        private void RefreshEventGames()
        {
            var selectedId = (CmbEventGame.SelectedItem as ComboBoxItem)?.Tag is Game sg ? sg.Id : (Guid?)null;
            CmbEventGame.Items.Clear();
            bool goal = IsGoalMode;
            foreach (var game in Games.Where(x => goal ? x.Type != GameType.PlayOnly : x.Type == GameType.Mobile))
                CmbEventGame.Items.Add(new ComboBoxItem { Content = game.Name, Tag = game });
            int idx = -1;
            for (int i = 0; i < CmbEventGame.Items.Count; i++)
                if (((ComboBoxItem)CmbEventGame.Items[i]).Tag is Game item && item.Id == selectedId) idx = i;
            CmbEventGame.SelectedIndex = idx >= 0 ? idx : (CmbEventGame.Items.Count > 0 ? 0 : -1);
            RefreshPickupGames(); // 게임 추가/삭제/분류 변경 시 픽업 탭도
        }

        private EventQuest? SelectedEvent() => (LstEvents.SelectedItem as ListBoxItem)?.Tag as EventQuest;

        private void RefreshEventList(Guid? selectId)
        {
            var now = DateTime.Now;
            LstEvents.Items.Clear();
            var ordered = AppState.Data.Events
                .OrderBy(e => e.Done)
                .ThenBy(e => e.NoDeadline)
                .ThenBy(e => e.EndDate)
                .ThenBy(e => e.CreatedAt);
            foreach (var ev in ordered)
            {
                var game = Games.FirstOrDefault(g => g.Id == ev.GameId);
                string name = string.IsNullOrWhiteSpace(ev.Name) ? (ev.NoDeadline ? "목표" : "이벤트") : ev.Name.Trim();
                string text;
                bool ended = false, urgent = false;
                if (ev.NoDeadline)
                {
                    string status = ev.Done ? $"완료 {ev.DoneAt:M'/'d}" : "진행 중";
                    text = $"[목표]   {game?.Name ?? "?"}   ·   {name}   ·   무기한   ({status})";
                }
                else
                {
                    int left = game != null ? ResetCalculator.EventDaysLeft(ev, game, now) : (ev.EndDate.Date - now.Date).Days;
                    ended = left < 0;
                    urgent = !ev.Done && !ended && left <= 7;
                    string status = ev.Done ? "완료" : ended ? "놓침 — 확인 후 삭제하세요" : ProgressBuilder.FormatDDay(left);
                    text = $"[이벤트]   {game?.Name ?? "?"}   ·   {name}   ·   ~{ev.EndDate:yyyy-MM-dd}   ({status})";
                }
                var item = new ListBoxItem { Content = text, Tag = ev, Padding = new Thickness(4, 3, 4, 3) };
                if (ended && !ev.Done) item.Foreground = MissedText;
                else if (ev.Done) item.Foreground = GrayText;
                else if (urgent) { item.Foreground = WarnText; item.FontWeight = FontWeights.SemiBold; }
                LstEvents.Items.Add(item);
                if (ev.Id == selectId) LstEvents.SelectedItem = item;
            }
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e) => AddEvent();

        private void AddEvent()
        {
            bool goal = IsGoalMode;
            if ((CmbEventGame.SelectedItem as ComboBoxItem)?.Tag is not Game game)
            {
                MessageBox.Show(goal ? "게임을 선택해 주세요." : "게임을 선택해 주세요. (기한 있는 이벤트는 모바일 게임에만 등록할 수 있습니다)",
                    "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var name = TxtEventName.Text.Trim();
            EventQuest ev;
            if (goal)
            {
                if (name.Length == 0)
                {
                    MessageBox.Show("목표 이름을 입력해 주세요. (예: 메인스토리 깨기)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    TxtEventName.Focus();
                    return;
                }
                ev = new EventQuest { GameId = game.Id, Name = name, NoDeadline = true };
            }
            else
            {
                if (DpEventEnd.SelectedDate is not DateTime end)
                {
                    MessageBox.Show("마감일을 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                ev = new EventQuest { GameId = game.Id, Name = name, EndDate = end.Date };
            }
            AppState.Data.Events.Add(ev);
            Save();
            TxtEventName.Clear();
            TxtEventName.Focus();
            RefreshEventList(ev.Id);
        }

        private void TxtEventName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AddEvent(); e.Handled = true; }
        }

        private void QuickDate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string s } && int.TryParse(s, out int days))
                DpEventEnd.SelectedDate = DateTime.Today.AddDays(days);
        }

        private void ToggleEventDone_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent() is not EventQuest ev) return;
            AppState.SetEventDone(ev, !ev.Done);
            RefreshEventList(ev.Id);
        }

        private void ChangeEventDate_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent() is not EventQuest ev)
            {
                MessageBox.Show("목록에서 항목을 선택한 뒤, 위의 마감일 칸에 새 날짜를 넣고 누르세요.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (DpEventEnd.SelectedDate is not DateTime end) return;
            var game = Games.FirstOrDefault(g => g.Id == ev.GameId);
            if (ev.NoDeadline && game?.Type != GameType.Mobile)
            {
                MessageBox.Show("기한 있는 이벤트는 모바일 게임에만 둘 수 있어서, 이 목표에는 마감일을 넣을 수 없습니다.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (ev.NoDeadline && !Confirm("이 무기한 목표를 기한 있는 이벤트로 바꿀까요?")) return;
            ev.NoDeadline = false;
            ev.EndDate = end.Date;
            Save();
            RefreshEventList(ev.Id);
        }

        private void RenameEvent_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent() is not EventQuest ev)
            {
                MessageBox.Show("목록에서 항목을 선택한 뒤, 위의 이름 칸에 새 이름을 넣고 누르세요.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ev.Name = TxtEventName.Text.Trim();
            Save();
            TxtEventName.Clear();
            RefreshEventList(ev.Id);
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEvent() is not EventQuest ev) return;
            AppState.DeleteEvent(ev);
            RefreshEventList(null);
        }

        // =====================================================================
        // 주기 컨텐츠
        // =====================================================================

        private sealed record CycleEntry(Game Game, HomeworkTask Task);

        private CycleEntry? SelectedCycle() => (LstCycles.SelectedItem as ListBoxItem)?.Tag as CycleEntry;

        private void RefreshCycleList(Guid? selectId)
        {
            var now = DateTime.Now;
            LstCycles.Items.Clear();
            var entries = Games.Where(g => g.Type != GameType.PlayOnly)
                .SelectMany(g => g.Tasks.Where(t => t.Kind == ResetKind.Cycle).Select(t => new CycleEntry(g, t)))
                .OrderBy(e => ResetCalculator.CycleCurrentEnd(e.Task, e.Game, now))
                .ToList();
            foreach (var e in entries)
            {
                var p = ProgressBuilder.Build(e.Game, AppState.Data, now, WidgetKind.Cycle);
                var cells = p.Cells.Where(c => c.Task.Id == e.Task.Id).ToList();
                int done = cells.Count(c => c.Done);
                var end = ResetCalculator.CycleCurrentEnd(e.Task, e.Game, now);
                int left = ResetCalculator.CycleDaysLeft(e.Task, e.Game, now);
                string status = cells.Count == 0 ? "" : cells.Count == 1 ? (done == 1 ? "완료" : "남음") : $"{done}/{cells.Count} 완료";
                var item = new ListBoxItem
                {
                    Content = $"{e.Game.Name}   ·   {e.Task.Name}   ·   {ResetCalculator.CycleWeeksOf(e.Task)}주마다 {ResetCalculator.DayName(e.Task.CycleEndDay)}요일까지   ·   " +
                              $"이번 회차 {end:M'/'d}({ResetCalculator.DayName(end.DayOfWeek)})까지 ({ProgressBuilder.FormatDDay(left)})   ·   {status}",
                    Tag = e,
                    Padding = new Thickness(4, 3, 4, 3)
                };
                bool allDone = cells.Count > 0 && done == cells.Count;
                if (allDone) item.Foreground = GrayText;
                else if (left <= ResetCalculator.CycleWarnDays) { item.Foreground = WarnText; item.FontWeight = FontWeights.SemiBold; }
                LstCycles.Items.Add(item);
                if (e.Task.Id == selectId) LstCycles.SelectedItem = item;
            }
        }

        /// <summary>주기 컨텐츠 창 (등록/수정)</summary>
        private void OpenCycleWindow(Game? game, HomeworkTask? task)
        {
            var w = new CycleWindow(game, task) { Owner = this };
            if (w.ShowDialog() != true) return;
            RefreshCycleList(w.Result?.Id);
        }

        private void AddCycle_Click(object sender, RoutedEventArgs e) =>
            OpenCycleWindow(_game?.Type == GameType.PlayOnly ? null : _game, null);

        private void EditCycle_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCycle() is not CycleEntry c)
            {
                MessageBox.Show("목록에서 고칠 항목을 선택하세요. (두 번 클릭해도 돼요)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenCycleWindow(c.Game, c.Task);
        }

        private void LstCycles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedCycle() is CycleEntry c) OpenCycleWindow(c.Game, c.Task);
        }

        private void DeleteCycle_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCycle() is not CycleEntry c) return;
            if (!Confirm($"주기 컨텐츠 '{c.Task.Name}'({c.Game.Name})을(를) 삭제할까요?")) return;
            string id = c.Task.Id.ToString();
            RemoveCompletions(k => k.StartsWith(id, StringComparison.Ordinal));
            c.Game.Tasks.Remove(c.Task);
            Save();
            RefreshCycleList(null);
        }

        // =====================================================================
        // 픽업
        // =====================================================================

        private bool _pickupLoading;

        private void InitPickupTab()
        {
            RefreshPickupGames();
            DpPickupStart.SelectedDate = DateTime.Today;
            DpPickupEnd.SelectedDate = DateTime.Today.AddDays(20);
            RefreshPickupList(null);
        }

        /// <summary>숙제 없는 게임을 뺀 모든 게임 (모바일 게임 먼저)</summary>
        private void RefreshPickupGames()
        {
            var selectedId = (CmbPickupGame.SelectedItem as ComboBoxItem)?.Tag is Game sg ? sg.Id : (Guid?)null;
            CmbPickupGame.Items.Clear();
            foreach (var game in Games.Where(x => x.Type != GameType.PlayOnly).OrderBy(x => x.Type == GameType.Mobile ? 0 : 1))
                CmbPickupGame.Items.Add(new ComboBoxItem { Content = game.Name, Tag = game });
            SelectPickupGame(selectedId);
        }

        private void SelectPickupGame(Guid? id)
        {
            int idx = -1;
            for (int i = 0; i < CmbPickupGame.Items.Count; i++)
                if (((ComboBoxItem)CmbPickupGame.Items[i]).Tag is Game g && g.Id == id) idx = i;
            CmbPickupGame.SelectedIndex = idx >= 0 ? idx : (CmbPickupGame.Items.Count > 0 ? 0 : -1);
        }

        private PickupBanner? SelectedPickup() => (LstPickups.SelectedItem as ListBoxItem)?.Tag as PickupBanner;

        private void RefreshPickupList(Guid? selectId)
        {
            var now = DateTime.Now;
            _pickupLoading = true;
            LstPickups.Items.Clear();
            var order = Games.Select((g, i) => (g.Id, i)).ToDictionary(x => x.Id, x => x.i);
            foreach (var p in AppState.Data.Pickups
                         .OrderBy(p => order.TryGetValue(p.GameId, out var i) ? i : 999)
                         .ThenBy(p => p.StartDate).ThenBy(p => p.EndDate))
            {
                var game = Games.FirstOrDefault(g => g.Id == p.GameId);
                string status = game == null ? "" : PickupCalc.Status(p, game, now) switch
                {
                    PickupStatus.Upcoming => "예정 · " + PickupCalc.PeriodText(p, game, now),
                    PickupStatus.Active => "진행 중 · " + PickupCalc.PeriodText(p, game, now),
                    _ => "끝남"
                };
                string name = string.IsNullOrWhiteSpace(p.Name) ? "픽업" : p.Name.Trim();
                var item = new ListBoxItem
                {
                    Content = $"{game?.Name ?? "?"}   ·   {name}   ·   {p.StartDate:yyyy-MM-dd} ~ {p.EndDate:yyyy-MM-dd}   ({status})",
                    Tag = p,
                    Padding = new Thickness(4, 3, 4, 3)
                };
                if (game != null && PickupCalc.Status(p, game, now) == PickupStatus.Upcoming) item.Foreground = GrayText;
                LstPickups.Items.Add(item);
                if (p.Id == selectId) LstPickups.SelectedItem = item;
            }
            _pickupLoading = false;
        }

        /// <summary>목록에서 고르면 위 칸에 불러온다 (수정하기 쉽게)</summary>
        private void LstPickups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_pickupLoading || SelectedPickup() is not PickupBanner p) return;
            SelectPickupGame(p.GameId);
            TxtPickupName.Text = p.Name;
            DpPickupStart.SelectedDate = p.StartDate;
            DpPickupEnd.SelectedDate = p.EndDate;
        }

        /// <summary>위 칸 값 읽기 (잘못됐으면 안내하고 false)</summary>
        private bool ReadPickupFields(out Game game, out string name, out DateTime start, out DateTime end)
        {
            game = null!; name = TxtPickupName.Text.Trim(); start = default; end = default;
            if ((CmbPickupGame.SelectedItem as ComboBoxItem)?.Tag is not Game g)
            {
                MessageBox.Show("게임을 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            game = g;
            if (name.Length == 0)
            {
                MessageBox.Show("어떤 픽업인지 입력해 주세요. (예: 캐릭터 이름)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtPickupName.Focus();
                return false;
            }
            if (DpPickupStart.SelectedDate is not DateTime s || DpPickupEnd.SelectedDate is not DateTime en)
            {
                MessageBox.Show("시작일과 마지막 날을 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (en.Date < s.Date)
            {
                MessageBox.Show("마지막 날이 시작일보다 빨라요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (en.Date < ResetCalculator.GameDate(DateTime.Now, g.GetResetTimeOfDay()))
            {
                MessageBox.Show("이미 끝난 기간이에요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            start = s.Date;
            end = en.Date;
            return true;
        }

        private void AddPickup_Click(object sender, RoutedEventArgs e) => AddPickup();

        private void AddPickup()
        {
            if (!ReadPickupFields(out var game, out var name, out var start, out var end)) return;
            var p = new PickupBanner { GameId = game.Id, Name = name, StartDate = start, EndDate = end };
            AppState.Data.Pickups.Add(p);
            Save();
            TxtPickupName.Clear();
            TxtPickupName.Focus();
            RefreshPickupList(null);
        }

        private void TxtPickupName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { AddPickup(); e.Handled = true; }
        }

        private void QuickPickup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string s } || !int.TryParse(s, out int days)) return;
            var start = DpPickupStart.SelectedDate ?? DateTime.Today;
            DpPickupEnd.SelectedDate = start.Date.AddDays(days - 1); // 시작일 포함 N일
        }

        private void UpdatePickup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickup() is not PickupBanner p)
            {
                MessageBox.Show("목록에서 고친 뒤 누르세요. (고르면 위 칸에 불러와요)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!ReadPickupFields(out var game, out var name, out var start, out var end)) return;
            p.GameId = game.Id;
            p.Name = name;
            p.StartDate = start;
            p.EndDate = end;
            Save();
            RefreshPickupList(p.Id);
        }

        private void DeletePickup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPickup() is not PickupBanner p) return;
            if (!Confirm($"픽업 '{p.Name}'을(를) 삭제할까요?")) return;
            AppState.Data.Pickups.Remove(p);
            Save();
            RefreshPickupList(null);
        }

        // =====================================================================
        // 공유 (탭별 내보내기 / 추가로 가져오기)
        // =====================================================================

        private static string GameNameOf(Guid id) => Games.FirstOrDefault(g => g.Id == id)?.Name ?? "?";

        /// <summary>가져온 뒤 모든 목록 새로 고침</summary>
        private void AfterImport()
        {
            RefreshEventList(null);
            RefreshCycleList(null);
            RefreshPickupList(null);
        }

        private void ExportEvents_Click(object sender, RoutedEventArgs e)
        {
            var items = AppState.Data.Events
                .OrderBy(x => x.NoDeadline).ThenBy(x => x.EndDate)
                .Select(x => ((x.NoDeadline ? "[목표] " : "[이벤트] ") + $"{GameNameOf(x.GameId)}  ·  " +
                              (string.IsNullOrWhiteSpace(x.Name) ? "(이름 없음)" : x.Name) +
                              (x.NoDeadline ? "  ·  무기한" : $"  ·  ~{x.EndDate:yyyy'-'MM'-'dd}"), (object)x))
                .ToList();
            ShareUi.Export(this, ShareType.Events, items, SelectedEvent(),
                sel => ShareCodec.ExportEvents(sel.Cast<EventQuest>(), AppState.Data));
        }

        private void ImportEvents_Click(object sender, RoutedEventArgs e)
        {
            if (ShareUi.Import(this, ShareType.Events)) AfterImport();
        }

        private void ExportCycles_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            var items = Games.Where(g => g.Type != GameType.PlayOnly)
                .SelectMany(g => g.Tasks.Where(t => t.Kind == ResetKind.Cycle).Select(t => new CycleEntry(g, t)))
                .Select(c => ($"{c.Game.Name}  ·  {c.Task.Name}  ·  {ResetCalculator.CycleWeeksOf(c.Task)}주마다 {ResetCalculator.DayName(c.Task.CycleEndDay)}요일까지" +
                              $"  ·  이번 회차 ~{ResetCalculator.CycleCurrentEnd(c.Task, c.Game, now):M'/'d}", (object)c))
                .ToList();
            ShareUi.Export(this, ShareType.Cycles, items, SelectedCycle(),
                sel => ShareCodec.ExportCycles(sel.Cast<CycleEntry>().Select(c => (c.Game, c.Task)), DateTime.Now));
        }

        private void ImportCycles_Click(object sender, RoutedEventArgs e)
        {
            if (ShareUi.Import(this, ShareType.Cycles)) AfterImport();
        }

        private void ExportPickups_Click(object sender, RoutedEventArgs e)
        {
            var items = AppState.Data.Pickups
                .OrderBy(p => p.StartDate)
                .Select(p => ($"{GameNameOf(p.GameId)}  ·  {p.Name}  ·  {p.StartDate:yyyy'-'MM'-'dd} ~ {p.EndDate:yyyy'-'MM'-'dd}", (object)p))
                .ToList();
            ShareUi.Export(this, ShareType.Pickups, items, SelectedPickup(),
                sel => ShareCodec.ExportPickups(sel.Cast<PickupBanner>(), AppState.Data));
        }

        private void ImportPickups_Click(object sender, RoutedEventArgs e)
        {
            if (ShareUi.Import(this, ShareType.Pickups)) AfterImport();
        }

        // =====================================================================
        // 통계
        // =====================================================================

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 안쪽 ComboBox/ListBox의 SelectionChanged도 여기로 올라오므로 탭 자체만 처리
            if (!ReferenceEquals(e.OriginalSource, Tabs) || TabStats == null) return;
            if (ReferenceEquals(Tabs.SelectedItem, TabStats))
            {
                RefreshStatsGames();
                RefreshStats();
            }
        }

        private void RefreshStatsGames()
        {
            var selected = (CmbStatsGame.SelectedItem as ComboBoxItem)?.Tag as Guid?;
            _loading = true;
            CmbStatsGame.Items.Clear();
            CmbStatsGame.Items.Add(new ComboBoxItem { Content = "전체 게임", Tag = null });
            int idx = 0;
            foreach (var g in Games)
            {
                CmbStatsGame.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g.Id });
                if (g.Id == selected) idx = CmbStatsGame.Items.Count - 1;
            }
            CmbStatsGame.SelectedIndex = idx;
            _loading = false;
        }

        private void RefreshStats()
        {
            if (_loading || StatsPanel == null) return;
            var now = DateTime.Now;
            int days = RdoStats7.IsChecked == true ? 7 : RdoStatsAll.IsChecked == true ? 0 : 28;
            var opt = new StatsOptions
            {
                From = days == 0 ? null : StatsEngine.StatDay(now).AddDays(-(days - 1)) + StatsEngine.DayCutoff,
                GameId = (CmbStatsGame.SelectedItem as ComboBoxItem)?.Tag as Guid?,
                IncludePlayOnly = ChkStatsPlayOnly.IsChecked == true
            };
            try
            {
                var records = AppState.Log.ReadAll(opt.From?.AddDays(-1));
                var result = StatsEngine.Compute(records, AppState.Data, opt);
                StatsView.Render(StatsPanel, result, days, App.BuildToday());
            }
            catch (Exception ex)
            {
                App.LogError("통계 계산 실패", ex);
                StatsPanel.Children.Clear();
                StatsPanel.Children.Add(new TextBlock { Text = "통계를 불러오지 못했습니다: " + ex.Message, Foreground = WarnText, TextWrapping = TextWrapping.Wrap });
            }
        }

        private void StatsOption_Changed(object sender, RoutedEventArgs e) => RefreshStats();

        private void CmbStatsGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            RefreshStats();
        }

        private void ExportStatsCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"게임숙제_기록_{DateTime.Now:yyyyMMdd}.csv",
                Filter = "CSV 파일 (*.csv)|*.csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                // BOM 포함 UTF-8 → 엑셀에서 한글이 깨지지 않음
                File.WriteAllText(dlg.FileName, StatsLog.ToCsv(AppState.Log.ReadAll()), new System.Text.UTF8Encoding(true));
                MessageBox.Show("기록을 CSV로 저장했습니다.", "내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppState.Log.FolderPath);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppState.Log.FolderPath + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // =====================================================================
        // 위젯
        // =====================================================================

        private void InitWidgetTab()
        {
            _loading = true;
            var w = AppState.Data.Widget;
            SldOpacity.Value = Math.Clamp(w.Opacity, 0.3, 1.0);
            TxtOpacity.Text = $"{Math.Round(SldOpacity.Value * 100)}%";
            ChkLock.IsChecked = w.LockPosition;
            ChkSortDone.IsChecked = w.SortDoneToBottom;
            ChkAutoStart.IsChecked = AutoStart.IsEnabled;
            TxtDataPath.Text = $"버전 v{App.Version} · 저장 위치: " + AppState.Store.FilePath;
            if (CmbTheme.Items.Count == 0)
                foreach (var t in WidgetTheme.All)
                    CmbTheme.Items.Add(new ComboBoxItem { Content = $"{t.Name} — {t.Description}", Tag = t.Id });
            _loading = false;
            RefreshWidgetChecks();
            LoadThemeControls();
        }

        // ---------- 테마 · 배경 이미지 ----------

        /// <summary>테마를 꾸밀 위젯 칸</summary>
        private int ThemeSlot => (CmbThemeWidget.SelectedItem as ComboBoxItem)?.Tag is int i ? i : AppState.Data.Widget.ActiveSlots.FirstOrDefault();

        private WidgetPlacement ThemePlacement => AppState.Data.Widget.GetSlot(ThemeSlot);

        /// <summary>테마 대상 목록: 창이 있는 칸만 ("위젯 1 — 일일 · 주간")</summary>
        private void RefreshThemeWidgetList()
        {
            int keep = ThemeSlot;
            var ws = AppState.Data.Widget;
            bool was = _loading;
            _loading = true;
            CmbThemeWidget.Items.Clear();
            foreach (var i in ws.ActiveSlots)
            {
                var item = new ComboBoxItem { Content = ws.SlotLabel(i), Tag = i };
                CmbThemeWidget.Items.Add(item);
                if (i == keep) CmbThemeWidget.SelectedItem = item;
            }
            if (CmbThemeWidget.SelectedIndex < 0 && CmbThemeWidget.Items.Count > 0) CmbThemeWidget.SelectedIndex = 0;
            _loading = was;
        }

        private void LoadThemeControls()
        {
            if (CmbTheme == null || CmbTheme.Items.Count == 0) return;
            var pl = ThemePlacement;
            _loading = true;
            var theme = WidgetTheme.Get(pl.Theme);
            for (int i = 0; i < CmbTheme.Items.Count; i++)
                if ((CmbTheme.Items[i] as ComboBoxItem)?.Tag as string == theme.Id) CmbTheme.SelectedIndex = i;
            TxtBgImage.Text = string.IsNullOrEmpty(pl.BackgroundImage) ? "없음" : "설정됨 (" + Path.GetFileName(pl.BackgroundImage) + ")";
            for (int i = 0; i < CmbImageAlign.Items.Count; i++)
                if ((CmbImageAlign.Items[i] as ComboBoxItem)?.Tag as string == pl.ImageAlign) CmbImageAlign.SelectedIndex = i;
            if (CmbImageAlign.SelectedIndex < 0) CmbImageAlign.SelectedIndex = 1;
            ChkTopmost.IsChecked = pl.AlwaysOnTop;
            SldOverlay.Value = pl.EffectiveOverlay;
            TxtOverlay.Text = $"{Math.Round(pl.EffectiveOverlay * 100)}%";
            _loading = false;
        }

        private void CmbThemeWidget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            LoadThemeControls();
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || (CmbTheme.SelectedItem as ComboBoxItem)?.Tag is not string id) return;
            var pl = ThemePlacement;
            pl.Theme = id;
            pl.OverlayOpacity = null; // 테마를 바꾸면 그 테마에 맞는 기본 가림 정도로
            Save();
            LoadThemeControls();
        }

        private void PickBg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = BackgroundImages.FileFilter, Title = "배경 이미지 고르기" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                ThemePlacement.BackgroundImage = BackgroundImages.Import(dlg.FileName);
                Save();
                BackgroundImages.CleanupUnused();
                LoadThemeControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show("이미지를 가져오지 못했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearBg_Click(object sender, RoutedEventArgs e)
        {
            ThemePlacement.BackgroundImage = null;
            Save();
            BackgroundImages.CleanupUnused();
            LoadThemeControls();
        }

        private void CmbImageAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || (CmbImageAlign.SelectedItem as ComboBoxItem)?.Tag is not string align) return;
            ThemePlacement.ImageAlign = align;
            Save();
        }

        private void SldOverlay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtOverlay == null) return;
            ThemePlacement.OverlayOpacity = Math.Round(SldOverlay.Value, 2);
            TxtOverlay.Text = $"{Math.Round(SldOverlay.Value * 100)}%";
            Save();
        }

        private void ChkTopmost_Click(object sender, RoutedEventArgs e)
        {
            ThemePlacement.AlwaysOnTop = ChkTopmost.IsChecked == true;
            Save();
        }

        private void ResetOverlay_Click(object sender, RoutedEventArgs e)
        {
            ThemePlacement.OverlayOpacity = null;
            Save();
            LoadThemeControls();
        }

        private void ApplyThemeAll_Click(object sender, RoutedEventArgs e)
        {
            var src = ThemePlacement;
            for (int slot = 0; slot < WidgetSettings.SlotCount; slot++)
            {
                var pl = AppState.Data.Widget.GetSlot(slot);
                if (ReferenceEquals(pl, src)) continue;
                pl.Theme = src.Theme;
                pl.BackgroundImage = src.BackgroundImage;
                pl.ImageAlign = src.ImageAlign;
                pl.OverlayOpacity = src.OverlayOpacity;
            }
            Save();
            MessageBox.Show("모든 위젯에 같은 테마와 배경을 적용했습니다. (항상 위 설정은 위젯마다 그대로)", "테마", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>위젯 탭 다시 그리기 (트레이에서 켜고 끈 경우 등)</summary>
        private void RefreshWidgetChecks()
        {
            BuildSlotEditor();
            RefreshThemeWidgetList();
        }

        // ---------- 위젯 배치 (칸 5개, 끌어서 합치기) ----------

        private const string DragFormat = "GameHomework.WidgetKind";
        private Point _dragStart;
        private WidgetKind? _dragKind;
        private static readonly Brush SlotBorder = new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD4));
        private static readonly Brush SlotHover = new SolidColorBrush(Color.FromRgb(0x4C, 0x6E, 0xF5));
        private static readonly Brush SlotBg = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9));
        private static readonly Brush SlotEmptyBg = new SolidColorBrush(Color.FromRgb(0xFB, 0xFB, 0xFC));
        private static readonly Brush ChipBg = new SolidColorBrush(Color.FromRgb(0xE4, 0xEA, 0xFF));
        private static readonly Brush ChipBorder = new SolidColorBrush(Color.FromRgb(0x9F, 0xB2, 0xF5));

        private static string KindLabel(WidgetKind k) => k switch
        {
            WidgetKind.Daily => "일일 숙제",
            WidgetKind.Weekly => "주간 숙제",
            WidgetKind.Cycle => "주기 컨텐츠",
            WidgetKind.Events => "이벤트 · 목표",
            _ => "픽업"
        };

        private void BuildSlotEditor()
        {
            PnlSlots.Children.Clear();
            var ws = AppState.Data.Widget;
            for (int slot = 0; slot < WidgetSettings.SlotCount; slot++)
            {
                int s = slot;
                var kinds = ws.KindsIn(s);
                var box = new Border
                {
                    BorderBrush = SlotBorder,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(8),
                    Background = kinds.Count == 0 ? SlotEmptyBg : SlotBg,
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, slot == WidgetSettings.SlotCount - 1 ? 0 : 8, 0),
                    MinHeight = 130,
                    AllowDrop = true
                };
                var stack = new StackPanel();
                box.Child = stack;

                // 머리: "위젯 1"  [✓ 보이기]
                var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
                if (kinds.Count > 0)
                {
                    var show = new CheckBox
                    {
                        Content = "보이기",
                        FontSize = 11.5,
                        IsChecked = IsSlotShown(s),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    show.Click += (o, e) => App.SetWidgetVisible(s, show.IsChecked == true);
                    DockPanel.SetDock(show, Dock.Right);
                    head.Children.Add(show);
                }
                head.Children.Add(new TextBlock { Text = $"위젯 {s + 1}", FontWeight = FontWeights.Bold });
                stack.Children.Add(head);

                var chips = new WrapPanel();
                foreach (var k in kinds) chips.Children.Add(MakeChip(k));
                stack.Children.Add(chips);

                if (kinds.Count == 0)
                    stack.Children.Add(new TextBlock
                    {
                        Text = "비어 있음 — 창 없음\n여기로 끌어 놓으면\n따로 떨어진 위젯이 돼요",
                        Foreground = Brushes.Gray,
                        FontSize = 11.5,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 6, 0, 0)
                    });
                else if (kinds.Count > 1)
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"{kinds.Count}칸을 나란히",
                        Foreground = Brushes.Gray,
                        FontSize = 11,
                        Margin = new Thickness(0, 6, 0, 0)
                    });

                box.DragEnter += (o, e) => { if (e.Data.GetDataPresent(DragFormat)) box.BorderBrush = SlotHover; };
                box.DragLeave += (o, e) => box.BorderBrush = SlotBorder;
                box.DragOver += (o, e) =>
                {
                    e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
                    e.Handled = true;
                };
                box.Drop += (o, e) =>
                {
                    box.BorderBrush = SlotBorder;
                    if (e.Data.GetData(DragFormat) is not WidgetKind kind) return;
                    // 놓은 위치: 어느 이름표 앞에 끼울지 (없으면 맨 뒤)
                    int index = -1;
                    for (int c = 0; c < chips.Children.Count; c++)
                    {
                        if (chips.Children[c] is not FrameworkElement fe) continue;
                        var p = e.GetPosition(fe);
                        if (p.Y < 0 || (p.Y <= fe.ActualHeight && p.X < fe.ActualWidth / 2)) { index = c; break; }
                    }
                    e.Handled = true;
                    // 끌기 이벤트가 끝난 뒤에 화면을 다시 만든다
                    Dispatcher.BeginInvoke(new Action(() => MoveKind(kind, s, index)));
                };
                PnlSlots.Children.Add(box);
            }
        }

        private Border MakeChip(WidgetKind kind)
        {
            var chip = new Border
            {
                Background = ChipBg,
                BorderBrush = ChipBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.SizeAll,
                ToolTip = "끌어서 다른 칸으로 옮기기 (오른쪽 클릭: 메뉴)",
                Child = new TextBlock { Text = "⠿ " + KindLabel(kind) }
            };
            chip.PreviewMouseLeftButtonDown += (o, e) => { _dragStart = e.GetPosition(null); _dragKind = kind; };
            chip.PreviewMouseLeftButtonUp += (o, e) => _dragKind = null;
            chip.PreviewMouseMove += (o, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragKind != kind) return;
                var d = e.GetPosition(null) - _dragStart;
                if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                _dragKind = null;
                chip.Opacity = 0.5;
                DragDrop.DoDragDrop(chip, new DataObject(DragFormat, kind), DragDropEffects.Move);
                chip.Opacity = 1;
            };

            // 끌기가 어려울 때: 오른쪽 클릭 → "위젯 N으로 옮기기"
            var menu = new ContextMenu();
            var ws = AppState.Data.Widget;
            int cur = ws.SlotOf(kind);
            for (int slot = 0; slot < WidgetSettings.SlotCount; slot++)
            {
                if (slot == cur) continue;
                int s = slot;
                var item = new MenuItem
                {
                    Header = ws.KindsIn(s).Count == 0 ? $"위젯 {s + 1}(빈 칸)으로 — 따로 떼기" : $"{ws.SlotLabel(s)} 에 합치기"
                };
                item.Click += (o, e) => MoveKind(kind, s, -1);
                menu.Items.Add(item);
            }
            chip.ContextMenu = menu;
            return chip;
        }

        private void MoveKind(WidgetKind kind, int slot, int index)
        {
            App.MoveWidgetKind(kind, slot, index);
            RefreshWidgetChecks();
            LoadThemeControls();
        }

        private static bool IsSlotShown(int slot) =>
            App.Widgets.TryGetValue(slot, out var w) ? w.IsVisible : AppState.Data.Widget.GetSlot(slot).Visible;

        private void ResetSlots_Click(object sender, RoutedEventArgs e)
        {
            if (!Confirm("위젯을 처음 상태(칸마다 하나씩, 5개 따로)로 돌릴까요?")) return;
            App.ResetWidgetSlots();
            RefreshWidgetChecks();
            LoadThemeControls();
        }

        private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || TxtOpacity == null) return;
            AppState.Data.Widget.Opacity = Math.Round(SldOpacity.Value, 2);
            TxtOpacity.Text = $"{Math.Round(SldOpacity.Value * 100)}%";
            Save();
        }

        private void ChkLock_Click(object sender, RoutedEventArgs e)
        {
            AppState.Data.Widget.LockPosition = ChkLock.IsChecked == true;
            Save();
        }

        private void ChkSortDone_Click(object sender, RoutedEventArgs e)
        {
            AppState.Data.Widget.SortDoneToBottom = ChkSortDone.IsChecked == true;
            Save();
        }

        private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
        {
            AutoStart.Set(ChkAutoStart.IsChecked == true);
            ChkAutoStart.IsChecked = AutoStart.IsEnabled;
        }

        private void ResetPosition_Click(object sender, RoutedEventArgs e)
        {
            App.ResetWidgetPositions();
            RefreshWidgetChecks();
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(AppState.Store.FolderPath);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppState.Store.FolderPath + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"게임숙제_백업_{DateTime.Now:yyyyMMdd}.json",
                Filter = "JSON 파일 (*.json)|*.json"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllText(dlg.FileName, DataStore.Serialize(AppState.Data));
                MessageBox.Show("백업을 저장했습니다.", "백업", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON 파일 (*.json)|*.json" };
            if (dlg.ShowDialog(this) != true) return;
            if (!Confirm("지금 데이터를 백업 파일 내용으로 바꿀까요?\n(현재 데이터는 data.json.bak으로 한 번 보관됩니다)")) return;
            try
            {
                AppState.ReplaceFromJson(File.ReadAllText(dlg.FileName));
                RefreshGameList(Games.FirstOrDefault());
                RefreshEventGames();
                RefreshEventList(null);
                RefreshPickupList(null);
                RefreshCycleList(null);
                InitWidgetTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show("불러오지 못했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

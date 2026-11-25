using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameHomework.Core;
using Microsoft.Win32;

namespace GameHomework
{
    /// <summary>탭별 공유: 골라서 내보내기 / 받은 파일에서 추가로 가져오기</summary>
    public static class ShareUi
    {
        /// <summary>내보내기 창 → 파일 저장. items: (목록에 보일 글, 원본 객체)</summary>
        public static void Export(Window owner, ShareType type, IList<(string Label, object Tag)> items,
                                  object? preselected, Func<List<object>, SharePackage> build)
        {
            if (items.Count == 0)
            {
                MessageBox.Show(owner, $"내보낼 {ShareCodec.TypeName(type)}이(가) 없어요.", "내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var w = new ShareExportWindow(type, items, preselected) { Owner = owner };
            if (w.ShowDialog() != true || w.Selected.Count == 0) return;

            var dlg = new SaveFileDialog
            {
                Filter = "게임 숙제 공유 파일 (*.json)|*.json",
                FileName = $"게임숙제_{ShareCodec.TypeName(type).Replace("·", "")}_{DateTime.Now:yyyyMMdd}.json"
            };
            if (dlg.ShowDialog(owner) != true) return;
            try
            {
                var pkg = build(w.Selected);
                pkg.AppVersion = App.Version;
                File.WriteAllText(dlg.FileName, ShareCodec.Serialize(pkg));
                MessageBox.Show(owner, $"{ShareCodec.TypeName(type)} {pkg.Count}개를 내보냈어요.\n받는 사람은 같은 탭의 [가져오기]로 추가하면 돼요. (완료 체크는 같이 가지 않아요)",
                    "내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "저장하지 못했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>파일 고르기 → 미리보기 창 → 추가. 추가한 게 있으면 true</summary>
        public static bool Import(Window owner, ShareType expected)
        {
            var dlg = new OpenFileDialog { Filter = "게임 숙제 공유 파일 (*.json)|*.json|모든 파일|*.*" };
            if (dlg.ShowDialog(owner) != true) return false;
            SharePackage? pkg;
            try { pkg = ShareCodec.Parse(File.ReadAllText(dlg.FileName)); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "파일을 읽지 못했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (pkg == null)
            {
                MessageBox.Show(owner, "공유 파일이 아니에요.\n(전체 백업 파일은 설정 → 위젯 탭의 '백업 불러오기'로 여세요)",
                    "가져오기", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (pkg.Count == 0)
            {
                MessageBox.Show(owner, "파일에 항목이 없어요.", "가져오기", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (pkg.Type != expected &&
                MessageBox.Show(owner, $"이 파일은 {ShareCodec.TypeName(pkg.Type)} 파일이에요. 그대로 {ShareCodec.TypeName(pkg.Type)}(으)로 가져올까요?",
                    "가져오기", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return false;

            var w = new ShareImportWindow(pkg) { Owner = owner };
            return w.ShowDialog() == true;
        }

        internal static Window Setup(Window w, string title, double width, double height)
        {
            w.Title = title;
            w.Width = width;
            w.Height = height;
            w.MinWidth = 420;
            w.MinHeight = 300;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            w.FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
            w.FontSize = 12.5;
            w.ShowInTaskbar = false;
            return w;
        }

        internal static Button Btn(string text, Action click, bool isDefault = false, bool isCancel = false)
        {
            var b = new Button { Content = text, Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 6, 0), IsDefault = isDefault, IsCancel = isCancel };
            b.Click += (s, e) => click();
            return b;
        }
    }

    /// <summary>내보낼 항목 고르기 (체크박스)</summary>
    public sealed class ShareExportWindow : Window
    {
        private readonly List<CheckBox> _checks = new();
        private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray };
        public List<object> Selected { get; private set; } = new();

        public ShareExportWindow(ShareType type, IList<(string Label, object Tag)> items, object? preselected)
        {
            ShareUi.Setup(this, $"{ShareCodec.TypeName(type)} 내보내기", 620, 520);
            var root = new DockPanel { Margin = new Thickness(12) };

            var hint = new TextBlock
            {
                Text = "보낼 항목을 고르세요. 받는 사람은 같은 탭의 [가져오기]로 이 항목들만 추가해요. 완료 체크는 같이 가지 않아요.",
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            top.Children.Add(ShareUi.Btn("전체 선택", () => SetAll(true)));
            top.Children.Add(ShareUi.Btn("전체 해제", () => SetAll(false)));
            top.Children.Add(_count);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            bottom.Children.Add(ShareUi.Btn("파일로 내보내기…", Accept, isDefault: true));
            bottom.Children.Add(ShareUi.Btn("취소", () => { }, isCancel: true));
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

            var list = new StackPanel();
            bool anyPre = preselected != null && items.Any(i => ReferenceEquals(i.Tag, preselected));
            foreach (var (label, tag) in items)
            {
                var cb = new CheckBox
                {
                    Content = label,
                    Tag = tag,
                    Margin = new Thickness(2, 3, 2, 3),
                    // 탭에서 하나를 골라 두고 눌렀으면 그것만, 아니면 전부 체크
                    IsChecked = !anyPre || ReferenceEquals(tag, preselected)
                };
                cb.Click += (s, e) => UpdateCount();
                _checks.Add(cb);
                list.Children.Add(cb);
            }
            root.Children.Add(new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(6) }
            });
            Content = root;
            UpdateCount();
        }

        private void SetAll(bool on)
        {
            foreach (var cb in _checks) cb.IsChecked = on;
            UpdateCount();
        }

        private void UpdateCount() => _count.Text = $"   {_checks.Count(c => c.IsChecked == true)} / {_checks.Count}개 선택";

        private void Accept()
        {
            Selected = _checks.Where(c => c.IsChecked == true).Select(c => c.Tag).ToList();
            if (Selected.Count == 0)
            {
                MessageBox.Show(this, "하나 이상 골라 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }
    }

    /// <summary>가져오기 미리보기: 게임 연결 + 추가할 항목 고르기</summary>
    public sealed class ShareImportWindow : Window
    {
        private readonly SharePackage _pkg;
        private readonly List<ImportItem> _items;
        private readonly Dictionary<string, ComboBox> _gameCombos = new();
        private readonly List<(ImportItem Item, CheckBox Check, TextBlock State)> _rows = new();
        /// <summary>사용자가 직접 체크를 푼 항목 (게임 연결을 바꿔도 다시 체크하지 않음)</summary>
        private readonly HashSet<ImportItem> _userUnchecked = new();
        private readonly Button _ok;
        private bool _updating;

        private const string SkipLabel = "(건너뛰기)";

        public ShareImportWindow(SharePackage pkg)
        {
            _pkg = pkg;
            _items = ShareCodec.Items(pkg);
            ShareUi.Setup(this, $"{ShareCodec.TypeName(pkg.Type)} 가져오기", 720, 600);
            var data = AppState.Data;
            var root = new DockPanel { Margin = new Thickness(12) };

            var head = new TextBlock
            {
                Text = $"{ShareCodec.TypeName(pkg.Type)} {pkg.Count}개 · {pkg.ExportedAt:yyyy'-'MM'-'dd}에 내보낸 파일. 지금 데이터는 그대로 두고, 체크한 항목만 추가해요.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(head, Dock.Top);
            root.Children.Add(head);

            // ---- 게임 연결 ----
            var games = data.Games.Where(g => g.Type != GameType.PlayOnly).ToList();
            var mapGrid = new Grid();
            mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int r = 0;
            foreach (var name in _items.Select(i => i.GameName).Distinct())
            {
                mapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var src = new TextBlock { Text = string.IsNullOrWhiteSpace(name) ? "(게임 없음)" : name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                var arrow = new TextBlock { Text = "→", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var cmb = new ComboBox { Width = 220, Margin = new Thickness(0, 3, 10, 3) };
                cmb.Items.Add(new ComboBoxItem { Content = SkipLabel, Tag = null });
                foreach (var g in games) cmb.Items.Add(new ComboBoxItem { Content = g.Name, Tag = g });
                var match = ShareCodec.MatchGame(name, data);
                cmb.SelectedIndex = match == null ? 0 : games.IndexOf(match) + 1;
                var note = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Firebrick, FontSize = 11.5 };
                void UpdateNote() => note.Text = (cmb.SelectedItem as ComboBoxItem)?.Tag == null ? "← 같은 이름의 게임이 없어요. 넣을 게임을 골라 주세요" : "";
                UpdateNote();
                cmb.SelectionChanged += (s, e) => { UpdateNote(); Refresh(); };
                _gameCombos[name] = cmb;
                foreach (var (el, col) in new (UIElement, int)[] { (src, 0), (arrow, 1), (cmb, 2), (note, 3) })
                {
                    Grid.SetRow(el, r);
                    Grid.SetColumn(el, col);
                    mapGrid.Children.Add(el);
                }
                r++;
            }
            var mapBox = new GroupBox { Header = "게임 연결 (파일의 게임 → 내 게임)", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 10), Content = mapGrid };
            DockPanel.SetDock(mapBox, Dock.Top);
            root.Children.Add(mapBox);

            // ---- 버튼 ----
            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            _ok = ShareUi.Btn("추가", Accept, isDefault: true);
            bottom.Children.Add(_ok);
            bottom.Children.Add(ShareUi.Btn("취소", () => { }, isCancel: true));
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

            // ---- 항목 ----
            var list = new StackPanel();
            foreach (var item in _items)
            {
                var row = new DockPanel { Margin = new Thickness(2, 3, 2, 3) };
                var state = new TextBlock { FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                DockPanel.SetDock(state, Dock.Right);
                row.Children.Add(state);
                var cb = new CheckBox
                {
                    Content = $"{(string.IsNullOrWhiteSpace(item.GameName) ? "?" : item.GameName)}  ·  {item.Name}  ·  {item.Detail}",
                    VerticalAlignment = VerticalAlignment.Center
                };
                cb.Click += (s, e) =>
                {
                    if (cb.IsChecked == true) _userUnchecked.Remove(item); else _userUnchecked.Add(item);
                    UpdateOk();
                };
                row.Children.Add(cb);
                list.Children.Add(row);
                _rows.Add((item, cb, state));
            }
            root.Children.Add(new GroupBox
            {
                Header = "추가할 항목",
                Padding = new Thickness(4),
                Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });

            Content = root;
            Refresh();
        }

        private Dictionary<string, Game?> Mapping() =>
            _gameCombos.ToDictionary(p => p.Key, p => (p.Value.SelectedItem as ComboBoxItem)?.Tag as Game);

        /// <summary>게임 연결이 바뀌면 각 항목 상태(추가 / 이미 있음 / 끝남 …)를 다시 계산</summary>
        private void Refresh()
        {
            if (_updating) return;
            _updating = true;
            var map = Mapping();
            var now = DateTime.Now;
            foreach (var (item, cb, stateText) in _rows)
            {
                map.TryGetValue(item.GameName, out var game);
                var st = ShareCodec.State(item, game, AppState.Data, now);
                bool ok = st == ImportState.Ok;
                cb.IsEnabled = ok;
                cb.IsChecked = ok && !_userUnchecked.Contains(item);
                cb.Opacity = ok ? 1 : 0.55;
                stateText.Text = ok ? "→ " + game!.Name : ShareCodec.StateText(st);
                stateText.Foreground = ok ? Brushes.SeaGreen : Brushes.Gray;
            }
            _updating = false;
            UpdateOk();
        }

        private void UpdateOk()
        {
            int n = _rows.Count(x => x.Check.IsEnabled && x.Check.IsChecked == true);
            _ok.Content = n == 0 ? "추가" : $"{n}개 추가";
            _ok.IsEnabled = n > 0;
        }

        private void Accept()
        {
            var selected = _rows.Where(x => x.Check.IsEnabled && x.Check.IsChecked == true).Select(x => x.Item).ToList();
            if (selected.Count == 0) return;
            var res = ShareCodec.Apply(selected, Mapping(), AppState.Data, DateTime.Now);
            AppState.Save();
            MessageBox.Show(this, $"{ShareCodec.TypeName(_pkg.Type)} {res.Added}개를 추가했어요." + (res.Skipped > 0 ? $" ({res.Skipped}개는 건너뜀)" : ""),
                "가져오기", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
    }
}

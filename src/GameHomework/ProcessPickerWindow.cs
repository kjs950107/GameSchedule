using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameHomework
{
    /// <summary>지금 실행 중인 프로그램 목록에서 게임 실행 파일을 고르는 창 (창이 있는 프로그램을 위로)</summary>
    public sealed class ProcessPickerWindow : Window
    {
        private readonly ListBox _list = new();
        private readonly CheckBox _showAll = new() { Content = "창 없는 프로그램도 보기", Margin = new Thickness(0, 0, 0, 6) };

        public string? SelectedExe { get; private set; }

        private sealed class Item
        {
            public string Exe { get; init; } = "";
            public string Title { get; init; } = "";
            public bool HasWindow { get; init; }
            public override string ToString() => Title.Length > 0 ? $"{Exe}    —    {Title}" : Exe;
        }

        public ProcessPickerWindow()
        {
            Title = "실행 중인 프로그램에서 고르기";
            Width = 560;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Malgun Gothic, Segoe UI");
            FontSize = 12.5;
            ShowInTaskbar = false;

            var root = new DockPanel { Margin = new Thickness(12) };

            var hint = new TextBlock
            {
                Text = "게임을 켜 둔 상태에서 게임 실행 파일을 고르세요. (런처 말고 실제 게임)",
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            _showAll.Click += (s, e) => Reload();
            DockPanel.SetDock(_showAll, Dock.Top);
            root.Children.Add(_showAll);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var refresh = new Button { Content = "새로고침", Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 6, 0) };
            refresh.Click += (s, e) => Reload();
            var ok = new Button { Content = "등록", Padding = new Thickness(16, 3, 16, 3), Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            ok.Click += (s, e) => Accept();
            var cancel = new Button { Content = "취소", Padding = new Thickness(16, 3, 16, 3), IsCancel = true };
            buttons.Children.Add(refresh);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            _list.MouseDoubleClick += (s, e) => Accept();
            root.Children.Add(_list);

            Content = root;
            Loaded += (s, e) => Reload();
        }

        private void Reload()
        {
            var items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        string exe = p.ProcessName + ".exe";
                        string title = "";
                        bool hasWindow = false;
                        try
                        {
                            hasWindow = p.MainWindowHandle != IntPtr.Zero;
                            title = p.MainWindowTitle ?? "";
                        }
                        catch { }
                        if (items.TryGetValue(exe, out var existing) && (existing.HasWindow || !hasWindow)) continue;
                        items[exe] = new Item { Exe = exe, Title = title, HasWindow = hasWindow };
                    }
                    catch { }
                }
            }
            bool all = _showAll.IsChecked == true;
            var list = items.Values
                .Where(i => all || i.HasWindow)
                .Where(i => !string.Equals(i.Exe, "GameHomework.exe", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.HasWindow)
                .ThenBy(i => i.Exe, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _list.ItemsSource = list;
        }

        private void Accept()
        {
            if (_list.SelectedItem is not Item item)
            {
                MessageBox.Show(this, "목록에서 프로그램을 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedExe = item.Exe;
            DialogResult = true;
        }
    }
}

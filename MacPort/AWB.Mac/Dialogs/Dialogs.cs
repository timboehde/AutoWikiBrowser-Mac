// Small dialogs the engine asks for through UiHost, built in code.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using WikiFunctions.Controls;
using WikiFunctions.Controls.Lists;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace AutoWikiBrowser.Mac.Dialogs
{
    internal static class DialogHelper
    {
        public static Window Make(string title, Control content) => new Window
        {
            Title = title,
            Content = new Border { Padding = new Thickness(18), Child = content },
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            MinWidth = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        /// <summary>Modal over the owner when it's on screen; otherwise a plain window we wait on.</summary>
        public static Task ShowAsync(Window dialog, Window owner)
        {
            if (owner != null && owner.IsVisible) return dialog.ShowDialog(owner);
            var tcs = new TaskCompletionSource<bool>();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Closed += (s, e) => tcs.TrySetResult(true);
            dialog.Show();
            return tcs.Task;
        }

        public static StackPanel ButtonRow(IEnumerable<Button> buttons)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            foreach (var b in buttons) row.Children.Add(b);
            return row;
        }

        public static TextBlock Wrapped(string text, double maxWidth = 460) => new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = maxWidth
        };
    }

    internal static class MessageDialog
    {
        public static async Task<DialogResult> ShowAsync(Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            // (label, result, isDefault, isCancel) — macOS order: cancel-type buttons left of the default
            var spec = new List<(string, DialogResult, bool, bool)>();
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel:
                    spec.Add(("Cancel", DialogResult.Cancel, false, true)); spec.Add(("OK", DialogResult.OK, true, false)); break;
                case MessageBoxButtons.YesNo:
                    spec.Add(("No", DialogResult.No, false, true)); spec.Add(("Yes", DialogResult.Yes, true, false)); break;
                case MessageBoxButtons.YesNoCancel:
                    spec.Add(("Cancel", DialogResult.Cancel, false, true)); spec.Add(("No", DialogResult.No, false, false));
                    spec.Add(("Yes", DialogResult.Yes, true, false)); break;
                case MessageBoxButtons.RetryCancel:
                    spec.Add(("Cancel", DialogResult.Cancel, false, true)); spec.Add(("Retry", DialogResult.Retry, true, false)); break;
                case MessageBoxButtons.AbortRetryIgnore:
                    spec.Add(("Abort", DialogResult.Abort, false, true)); spec.Add(("Ignore", DialogResult.Ignore, false, false));
                    spec.Add(("Retry", DialogResult.Retry, true, false)); break;
                default:
                    spec.Add(("OK", DialogResult.OK, true, true)); break;
            }

            DialogResult result = spec.Find(s => s.Item4).Item2;
            string glyph = icon switch
            {
                MessageBoxIcon.Error => "⛔️",
                MessageBoxIcon.Warning => "⚠️",
                MessageBoxIcon.Question => "❓",
                MessageBoxIcon.Information => "ℹ️",
                _ => ""
            };

            var panel = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            if (glyph.Length > 0) top.Children.Add(new TextBlock { Text = glyph, FontSize = 28, VerticalAlignment = VerticalAlignment.Top });
            top.Children.Add(new SelectableTextBlock { Text = (text ?? "").Replace("\r\n", "\n"), TextWrapping = TextWrapping.Wrap, MaxWidth = 460 });
            panel.Children.Add(top);

            var window = DialogHelper.Make(string.IsNullOrEmpty(caption) ? "AutoWikiBrowser" : caption, panel);
            var buttonList = new List<Button>();
            foreach (var (label, res, isDefault, isCancel) in spec)
            {
                var b = new Button { Content = label, IsDefault = isDefault, IsCancel = isCancel, MinWidth = 80 };
                if (isDefault) b.Classes.Add("accent");
                var r = res;
                b.Click += (s, e) => { result = r; window.Close(); };
                buttonList.Add(b);
            }
            panel.Children.Add(DialogHelper.ButtonRow(buttonList));

            await DialogHelper.ShowAsync(window, owner);
            return result;
        }
    }

    internal static class PromptDialog
    {
        public static async Task<InputBoxResult> ShowAsync(Window owner, string prompt, string title, string defaultText,
            InputBoxValidatingHandler validator)
        {
            var result = new InputBoxResult { OK = false, Text = defaultText };
            var box = new TextBox { Text = defaultText ?? "", MinWidth = 320 };
            var error = new TextBlock { Foreground = Brushes.Red, IsVisible = false, Margin = new Thickness(0, 6, 0, 0) };
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(DialogHelper.Wrapped(prompt));
            panel.Children.Add(box);
            panel.Children.Add(error);

            var window = DialogHelper.Make(string.IsNullOrEmpty(title) ? "AutoWikiBrowser" : title, panel);
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
            ok.Classes.Add("accent");
            cancel.Click += (s, e) => window.Close();
            ok.Click += (s, e) =>
            {
                var args = new InputBoxValidatingArgs { Text = box.Text ?? "" };
                validator?.Invoke(window, args);
                if (args.Cancel)
                {
                    error.Text = args.Message ?? "Invalid value";
                    error.IsVisible = true;
                    return;
                }
                result = new InputBoxResult { OK = true, Text = box.Text ?? "" };
                window.Close();
            };
            panel.Children.Add(DialogHelper.ButtonRow(new[] { cancel, ok }));
            window.Opened += (s, e) => box.Focus();

            await DialogHelper.ShowAsync(window, owner);
            return result;
        }
    }

    internal static class ProtectionLevelDialog
    {
        public static async Task<bool> ShowAsync(Window owner, ProtectionLevel picker)
        {
            bool ok = false;
            var type = new ListBox { ItemsSource = new[] { "Edit protection", "Move protection", "Edit & Move protection" }, SelectedIndex = 0 };
            var level = new ListBox { ItemsSource = new[] { "Autoconfirmed", "Sysop", "Autoconfirmed & Sysop" }, SelectedIndex = 0 };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            grid.Children.Add(type);
            Grid.SetColumn(level, 2);
            grid.Children.Add(level);

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "List protected pages with:" });
            panel.Children.Add(grid);
            var window = DialogHelper.Make("Protection level", panel);
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
            var okButton = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
            okButton.Classes.Add("accent");
            cancel.Click += (s, e) => window.Close();
            okButton.Click += (s, e) =>
            {
                picker.Type = new[] { "edit", "move", "edit|move" }[Math.Max(0, type.SelectedIndex)];
                picker.Level = new[] { "autoconfirmed", "sysop", "autoconfirmed|sysop" }[Math.Max(0, level.SelectedIndex)];
                ok = true;
                window.Close();
            };
            panel.Children.Add(DialogHelper.ButtonRow(new[] { cancel, okButton }));
            await DialogHelper.ShowAsync(window, owner);
            return ok;
        }
    }

    internal static class ErrorDialog
    {
        public static async Task ShowAsync(Window owner, string subject, string message, string bugReport)
        {
            var panel = new StackPanel { Spacing = 10, Width = 600 };
            panel.Children.Add(new TextBlock { Text = "AutoWikiBrowser hit an unexpected error", FontSize = 16, FontWeight = FontWeight.SemiBold });
            panel.Children.Add(new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(DialogHelper.Wrapped(
                "If this keeps happening, please report it. The details below are already formatted for Phabricator. " +
                "Mention that you're using the Mac port.", 600));
            panel.Children.Add(new TextBox
            {
                Text = subject + "\n\n" + (bugReport ?? "").Replace("\r\n", "\n"),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 240,
                FontFamily = new FontFamily("Menlo, Monaco, monospace"),
                FontSize = 11
            });

            var window = DialogHelper.Make("Error", panel);
            var copy = new Button { Content = "Copy details" };
            var report = new Button { Content = "Report on Phabricator…" };
            var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, MinWidth = 80 };
            close.Classes.Add("accent");
            copy.Click += async (s, e) => { if (window.Clipboard != null) await window.Clipboard.SetTextAsync(subject + "\n\n" + bugReport); };
            report.Click += (s, e) => MacSystem.OpenUrl(
                "https://phabricator.wikimedia.org/maniphest/task/create/?projects=AutoWikiBrowser&title=" +
                Uri.EscapeDataString(subject ?? "") + "&description=" + Uri.EscapeDataString(bugReport ?? ""));
            close.Click += (s, e) => window.Close();
            panel.Children.Add(DialogHelper.ButtonRow(new[] { copy, report, close }));
            await DialogHelper.ShowAsync(window, owner);
        }
    }
}

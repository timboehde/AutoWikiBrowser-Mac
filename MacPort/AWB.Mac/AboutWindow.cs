// About AutoWikiBrowser, as in AWB/AWB/AboutBox.cs. The warning and GPL notice (Assets/About.txt)
// are taken from AWB's AssemblyInfo description and WikiFunctions Resources.resx.
using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AutoWikiBrowser.Mac.Dialogs;
using WikiFunctions;

namespace AutoWikiBrowser.Mac
{
    internal static class AboutWindow
    {
        private static Window open;

        public static void Show(Window owner)
        {
            if (open != null) { open.Activate(); return; }

            string notice;
            try
            {
                using (var stream = AssetLoader.Open(new Uri("avares://AutoWikiBrowser/Assets/About.txt")))
                using (var reader = new StreamReader(stream))
                    notice = reader.ReadToEnd().Trim();
            }
            catch { notice = "AutoWikiBrowser is free software under the GNU General Public License."; }

            Bitmap icon = null;
            try { icon = new Bitmap(AssetLoader.Open(new Uri("avares://AutoWikiBrowser/Assets/AutoWikiBrowser.png"))); } catch { }

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            if (icon != null) header.Children.Add(new Image { Source = icon, Width = 96, Height = 96 });
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            titles.Children.Add(new TextBlock { Text = "AutoWikiBrowser", FontSize = 24, FontWeight = FontWeight.SemiBold });
            titles.Children.Add(new TextBlock { Text = "The semi-automated wiki editor", Opacity = 0.8 });
            titles.Children.Add(new TextBlock { Text = "Version " + Tools.VersionString + " for Mac", Margin = new Thickness(0, 6, 0, 0) });
            header.Children.Add(titles);

            var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            links.Children.Add(Link("AutoWikiBrowser on Wikipedia", "https://en.wikipedia.org/wiki/Wikipedia:AutoWikiBrowser"));
            links.Children.Add(Link("Report a bug", "https://phabricator.wikimedia.org/maniphest/task/create/?projects=AutoWikiBrowser"));

            var versions = new SelectableTextBlock
            {
                Text = ".NET version: " + Environment.Version + "\n" +
                       "macOS version: " + MacOSVersion() + " (" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant() + ")\n" +
                       "Avalonia version: " + typeof(Application).Assembly.GetName().Version + "\n" +
                       "WikiFunctions version: " + typeof(Variables).Assembly.GetName().Version,
                FontSize = 12,
                Opacity = 0.85
            };

            var noticeBox = new TextBox
            {
                Text = notice,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = 200,
                FontSize = 12
            };

            var panel = new StackPanel { Spacing = 12, Width = 520 };
            panel.Children.Add(header);
            panel.Children.Add(new TextBlock
            {
                Text = "Made by the AWB team. Mac version built on the AWB source with Avalonia.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(links);
            panel.Children.Add(noticeBox);
            panel.Children.Add(versions);

            var window = DialogHelper.Make("About AutoWikiBrowser", panel);
            window.ShowInTaskbar = true;
            var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 80 };
            ok.Classes.Add("accent");
            ok.Click += (s, e) => window.Close();
            panel.Children.Add(DialogHelper.ButtonRow(new[] { ok }));

            window.Closed += (s, e) => open = null;
            open = window;
            if (owner != null && owner.IsVisible) window.Show(owner);
            else { window.WindowStartupLocation = WindowStartupLocation.CenterScreen; window.Show(); }
        }

        private static Button Link(string text, string url)
        {
            var b = new Button { Content = text, Padding = new Thickness(10, 3) };
            b.Click += (s, e) => MacSystem.OpenUrl(url);
            return b;
        }

        private static string MacOSVersion()
        {
            // Environment.OSVersion reports the Darwin kernel version on .NET 8; read the real one
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("/usr/bin/sw_vers", "-productVersion")
                { RedirectStandardOutput = true, UseShellExecute = false };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string v = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    if (v.Length > 0) return v;
                }
            }
            catch { }
            return Environment.OSVersion.VersionString;
        }
    }
}

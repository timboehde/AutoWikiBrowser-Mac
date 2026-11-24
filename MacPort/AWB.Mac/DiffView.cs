// Native rendering of WikiDiff's output. WikiDiff produces a MediaWiki-style HTML table
// (rows of marker/content cells, with <span class='diffchange'> around changed words). That format is
// regular, so it's parsed here and drawn with Avalonia text, no web view needed.
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AutoWikiBrowser.Mac
{
    internal sealed class DiffView : UserControl
    {
        private static readonly Regex RowRegex = new Regex(@"<tr\b([^>]*)>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex CellRegex = new Regex(@"<td\b([^>]*)>(.*?)</td>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ClassRegex = new Regex(@"class='([^']*)'", RegexOptions.Compiled);
        private static readonly Regex ColspanRegex = new Regex(@"colspan='(\d+)'", RegexOptions.Compiled);
        private static readonly Regex GoToRegex = new Regex(@"GoTo\((\d+)\)", RegexOptions.Compiled);
        private static readonly Regex SpanRegex = new Regex(@"(<span class='diffchange'>|</span>)", RegexOptions.Compiled);

        private static readonly IBrush AddedBorder = Brush.Parse("#a3d3ff");
        private static readonly IBrush DeletedBorder = Brush.Parse("#ffe49c");
        private static readonly IBrush ContextBg = Brush.Parse("#f3f3f3");
        private static readonly IBrush ContextBorder = Brush.Parse("#e6e6e6");
        private static readonly IBrush AddedChange = Brush.Parse("#d8ecff");
        private static readonly IBrush DeletedChange = Brush.Parse("#feeec8");
        private static readonly IBrush TextBrush = Brush.Parse("#202122");
        private static readonly FontFamily Mono = new FontFamily("Menlo, Monaco, monospace");

        private readonly StackPanel rows = new StackPanel { Spacing = 2 };

        /// <summary>Raised when a row is clicked, with the 0-based line in the edited text</summary>
        public event Action<int> LineClicked;

        public DiffView()
        {
            Content = new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(6), Background = Brushes.White, Child = rows },
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            };
        }

        public void ShowMessage(string heading, string text)
        {
            rows.Children.Clear();
            rows.Children.Add(new TextBlock { Text = heading, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = TextBrush, Margin = new Thickness(4, 8) });
            if (!string.IsNullOrEmpty(text))
                rows.Children.Add(new TextBlock { Text = text, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0) });
        }

        public void ShowHtml(string tableRowsHtml)
        {
            rows.Children.Clear();
            int count = 0;
            foreach (Match row in RowRegex.Matches(tableRowsHtml ?? ""))
            {
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*,18,*") };
                int col = 0;
                int? line = null;
                var m = GoToRegex.Match(row.Groups[1].Value);
                if (m.Success) line = int.Parse(m.Groups[1].Value);

                foreach (Match cell in CellRegex.Matches(row.Groups[2].Value))
                {
                    string attrs = cell.Groups[1].Value;
                    string cls = ClassRegex.Match(attrs) is { Success: true } c ? c.Groups[1].Value : "";
                    int span = ColspanRegex.Match(attrs) is { Success: true } s ? int.Parse(s.Groups[1].Value) : 1;
                    if (line == null && GoToRegex.Match(attrs) is { Success: true } g) line = int.Parse(g.Groups[1].Value);

                    var element = MakeCell(cls, cell.Groups[2].Value);
                    Grid.SetColumn(element, Math.Min(col, 3));
                    Grid.SetColumnSpan(element, Math.Max(1, Math.Min(span, 4 - col)));
                    grid.Children.Add(element);
                    col += span;
                }

                if (line is int target)
                {
                    grid.Cursor = new Cursor(StandardCursorType.Hand);
                    grid.Background = Brushes.Transparent; // hit-testable
                    grid.PointerPressed += (sender, e) => LineClicked?.Invoke(target);
                }
                rows.Children.Add(grid);
                count++;
            }
            if (count == 0) ShowMessage("No changes", "");
        }

        private static Control MakeCell(string cls, string html)
        {
            string content = html.TrimEnd(' ', '\r', '\n', '\t').TrimStart('\r', '\n');
            switch (cls)
            {
                case "diff-lineno":
                    return new TextBlock
                    {
                        Text = WebUtility.HtmlDecode(content), FontWeight = FontWeight.Bold, Foreground = TextBrush,
                        Margin = new Thickness(4, 8, 4, 2)
                    };
                case "diff-addedline":
                case "diff-deletedline":
                case "diff-context":
                {
                    bool added = cls == "diff-addedline", context = cls == "diff-context";
                    var text = new SelectableTextBlock
                    {
                        TextWrapping = TextWrapping.Wrap, FontFamily = Mono, FontSize = 12, Foreground = TextBrush
                    };
                    text.Inlines = BuildInlines(content, added ? AddedChange : DeletedChange);
                    return new Border
                    {
                        Child = text,
                        Padding = new Thickness(6, 3),
                        Margin = new Thickness(0, 0, 4, 0),
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(4, 1, 1, 1),
                        BorderBrush = context ? ContextBorder : added ? AddedBorder : DeletedBorder,
                        Background = context ? ContextBg : Brushes.White
                    };
                }
                default: // marker cells: "-", "+", or blank
                    return new TextBlock
                    {
                        Text = WebUtility.HtmlDecode(content).Trim(), FontWeight = FontWeight.Bold, FontSize = 15,
                        Foreground = TextBrush, HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top
                    };
            }
        }

        private static InlineCollection BuildInlines(string html, IBrush highlight)
        {
            var inlines = new InlineCollection();
            bool changed = false;
            foreach (string part in SpanRegex.Split(html))
            {
                if (part == "<span class='diffchange'>") { changed = true; continue; }
                if (part == "</span>") { changed = false; continue; }
                if (part.Length == 0) continue;
                var run = new Run(WebUtility.HtmlDecode(part));
                if (changed) { run.Background = highlight; run.FontWeight = FontWeight.Bold; }
                inlines.Add(run);
            }
            return inlines;
        }
    }
}

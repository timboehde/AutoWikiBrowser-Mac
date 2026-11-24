using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WikiFunctions.Parse;

namespace AutoWikiBrowser.Mac
{
    /// <summary>One row of the rules grid, in the form the user types it</summary>
    public sealed class RuleRow
    {
        public string Find { get; set; } = "";
        public string Replace { get; set; } = "";
        public bool CaseSensitive { get; set; }
        public bool IsRegex { get; set; }
        public bool Multiline { get; set; }
        public bool Singleline { get; set; }
        public bool Minor { get; set; }
        public bool AfterFixes { get; set; }
        public bool Enabled { get; set; } = true;
        public string Comment { get; set; } = "";
    }

    public partial class FindReplaceWindow : Window
    {
        private readonly FindandReplace far;
        private readonly ObservableCollection<RuleRow> rows = new ObservableCollection<RuleRow>();

        public FindReplaceWindow() : this(new FindandReplace()) { }

        public FindReplaceWindow(FindandReplace far)
        {
            this.far = far;
            AvaloniaXamlLoader.Load(this);
            var grid = this.FindControl<DataGrid>("Grid");
            grid.ItemsSource = rows;

            foreach (Replacement r in far.GetList())
            {
                var opts = r.RegularExpressionOptions;
                rows.Add(new RuleRow
                {
                    Find = FindandReplace.DisplayFind(r),
                    Replace = FindandReplace.DisplayReplace(r),
                    CaseSensitive = (opts & System.Text.RegularExpressions.RegexOptions.IgnoreCase) == 0,
                    IsRegex = r.IsRegex,
                    Multiline = (opts & System.Text.RegularExpressions.RegexOptions.Multiline) != 0,
                    Singleline = (opts & System.Text.RegularExpressions.RegexOptions.Singleline) != 0,
                    Minor = r.Minor,
                    AfterFixes = r.BeforeOrAfter,
                    Enabled = r.Enabled,
                    Comment = r.Comment ?? ""
                });
            }
            if (rows.Count == 0) rows.Add(new RuleRow());

            this.FindControl<CheckBox>("IgnoreLinksCheck").IsChecked = far.IgnoreLinks;
            this.FindControl<CheckBox>("IgnoreMoreCheck").IsChecked = far.IgnoreMore;
            this.FindControl<CheckBox>("AddToSummaryCheck").IsChecked = far.AppendToSummary;

            this.FindControl<Button>("AddButton").Click += (s, e) => { rows.Add(new RuleRow()); grid.SelectedIndex = rows.Count - 1; };
            this.FindControl<Button>("DeleteButton").Click += (s, e) =>
            {
                if (grid.SelectedItem is RuleRow row) rows.Remove(row);
            };
            this.FindControl<Button>("UpButton").Click += (s, e) => Move(grid, -1);
            this.FindControl<Button>("DownButton").Click += (s, e) => Move(grid, 1);
            this.FindControl<Button>("ClearButton").Click += (s, e) => rows.Clear();
            this.FindControl<Button>("CancelButton").Click += (s, e) => Close(false);
            this.FindControl<Button>("OkButton").Click += OnOk;
        }

        private void Move(DataGrid grid, int delta)
        {
            int i = grid.SelectedIndex, j = i + delta;
            if (i < 0 || j < 0 || j >= rows.Count) return;
            rows.Move(i, j);
            grid.SelectedIndex = j;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            var grid = this.FindControl<DataGrid>("Grid");
            grid.CommitEdit();
            far.Clear();
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.Find)) continue;
                far.AddNew(FindandReplace.FromEditor(r.Find, r.Replace, r.CaseSensitive, r.IsRegex, r.Multiline,
                    r.Singleline, r.Minor, r.AfterFixes, r.Enabled, r.Comment));
            }
            far.IgnoreLinks = this.FindControl<CheckBox>("IgnoreLinksCheck").IsChecked == true;
            far.IgnoreMore = this.FindControl<CheckBox>("IgnoreMoreCheck").IsChecked == true;
            far.AppendToSummary = this.FindControl<CheckBox>("AddToSummaryCheck").IsChecked == true;
            Close(true);
        }
    }
}

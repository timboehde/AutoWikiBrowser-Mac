// Engine-side stand-ins for AWB dialogs that the engine opens itself.
// Same names and members as the WinForms originals; the actual UI comes from UiHost.
using System;
using System.Threading;
using System.Windows.Forms;
using WikiFunctions.Mac;

namespace WikiFunctions.Controls
{
    public enum ArticleAction { Move, Delete, Protect }

    public class ArticleActionDialog : Form
    {
        public ArticleActionDialog(ArticleAction moveDeleteProtect) { Action = moveDeleteProtect; }
        public ArticleAction Action { get; }
        public bool AutoProtectAll { get; set; }
        public string NewTitle { get; set; } = "";
        public string Summary { get; set; } = "";
        public string EditProtectionLevel { get; set; } = "";
        public string MoveProtectionLevel { get; set; } = "";
        public string ProtectExpiry { get; set; } = "";
        public bool CascadingProtection { get; set; }
        public bool NoRedirect { get; set; }
        public bool Watch { get; set; }
        public bool DealWithAssocTalkPage { get; set; }
        public override DialogResult ShowDialog() =>
            DialogResult = UiHost.Current.ShowArticleAction(this) ? DialogResult.OK : DialogResult.Cancel;
    }

    public class LevelNumber : Form
    {
        private readonly bool edits;
        private readonly int max;
        public LevelNumber(bool edits, int max) { this.edits = edits; this.max = max; }
        public int Levels { get; set; }
        public override DialogResult ShowDialog()
        {
            Levels = UiHost.Current.GetNumber(edits, max);
            return DialogResult = Levels < 0 ? DialogResult.Cancel : DialogResult.OK;
        }
    }

    public class InputBoxResult
    {
        public bool OK;
        public string Text;
    }

    public class InputBoxValidatingArgs : EventArgs
    {
        public string Text;
        public string Message;
        public bool Cancel;
    }

    public delegate void InputBoxValidatingHandler(object sender, InputBoxValidatingArgs e);

    public static class InputBox
    {
        public static InputBoxResult Show(string prompt, string title, string defaultText, InputBoxValidatingHandler validator)
            => UiHost.Current.PromptText(prompt, title, defaultText, validator);
        public static InputBoxResult Show(string prompt, string title, string defaultResponse,
            InputBoxValidatingHandler validator, int xpos, int ypos)
            => Show(prompt, title, defaultResponse, validator);
        public static InputBoxResult Show(string prompt, string title, string defaultText)
            => Show(prompt, title, defaultText, null);
        public static InputBoxResult Show(string prompt, string title)
            => Show(prompt, title, "", null);
    }

    /// <summary>Row in the typo statistics list (UI lives in the app)</summary>
    public class TypoStatsListViewItem : ListViewItem
    {
        public void Refresh() { }
    }
}

namespace WikiFunctions.Controls.Lists
{
    public class ProtectionLevel : Form
    {
        public string Type { get; set; } = "edit";
        public string Level { get; set; } = "";
        public override DialogResult ShowDialog() =>
            DialogResult = UiHost.Current.ShowProtectionLevel(this) ? DialogResult.OK : DialogResult.Cancel;
    }

    /// <summary>Engine-side placeholder for the list maker control. The static helper is
    /// copied from AWB/WikiFunctions/Controls/Lists/ListMaker.cs.</summary>
    public class ListMaker
    {
        // Used by ListPrefs (settings files). The Mac app sets these before saving.
        public string SourceText { get; set; } = "";
        public string SelectedProvider { get; set; } = "CategoryListProvider";
        public System.Collections.Generic.List<Article> Articles { get; set; } = new System.Collections.Generic.List<Article>();
        public System.Collections.Generic.List<Article> GetArticleList() => Articles;

        private const string DiffEditURL = @"/w(?:(?:iki)?/index\.php5?\?|/\?)title=(.*?)(?:&(?:action|diff|oldid|pe|offset|curid|redirect|type)=.*|$)";

        public static string NormalizeTitleCore(string s)
        {
            string originals = s;
            string escaped = System.Text.RegularExpressions.Regex.Escape(Variables.URL);

            var historyDiff = new System.Text.RegularExpressions.Regex(
                System.Text.RegularExpressions.Regex.Replace(escaped, @"https?://", @"(?:https?://|//)?") + DiffEditURL);
            s = historyDiff.Replace(s, m => m.Groups[1].Value.Replace('+', '_'));

            // Assumption flaw: that all wikis use /wiki/ as the default path
            string url = Variables.URL + "/wiki/";
            if (Variables.URL.Contains("https:"))
                s = s.Replace("http://", "https://");

            s = s.Replace(url, "").Trim();

            if (s.IndexOf("#", StringComparison.Ordinal) > 0)
                s = s.Substring(0, s.IndexOf("#", StringComparison.Ordinal));

            if (!originals.Equals(s))
                s = Tools.WikiDecode(s);

            // Remove left-to-right marks from title
            s = s.Replace("‎", "").Trim();
            return s;
        }
    }
}

namespace WikiFunctions.Background
{
    public class PleaseWait : Form
    {
        private IProgressDisplay display;
        private string status = "";
        public Thread Worker;
        public string Status
        {
            get => status;
            set { status = value; if (display != null) display.Status = value; }
        }
        public void SetProgress(int completed, int total) => display?.SetProgress(completed, total);
        public override void Show() { display = UiHost.Current.ShowProgress("Please wait"); display.Status = status; }
        public override void Close() { display?.Close(); display = null; }
    }
}

namespace WikiFunctions.Disambiguation
{
    public class DabForm : Form
    {
        private readonly Session session;
        public DabForm(Session session) { this.session = session; }
        public bool Abort;
        public string Disambiguate(string articleText, string articleTitle, string dabLink,
            string[] dabVariants, int contextChars, bool botMode, out bool skip)
        {
            string result = UiHost.Current.Disambiguate(session, articleText, articleTitle, dabLink,
                dabVariants, contextChars, botMode, out skip, out Abort);
            return result;
        }
    }
}

namespace WikiFunctions.DBScanner
{
    public class DatabaseScanner : Form
    {
        private readonly Controls.Lists.ListMaker listMaker;
        public DatabaseScanner() { }
        public DatabaseScanner(Controls.Lists.ListMaker lm) { listMaker = lm; }
        public override void Show() => UiHost.Current.ShowDatabaseScanner(listMaker);
    }
}

// What the engine sees as "the main form" (Variables.MainForm). The list makers and the
// project loader reach the session through it. Plugin-facing commands are wired up in stage 4.
using System;
using System.Windows.Forms;
using WikiFunctions;
using WikiFunctions.Logging;
using WikiFunctions.Plugin;

namespace AutoWikiBrowser.Mac
{
    internal sealed class AwbHost : IAutoWikiBrowser
    {
        private readonly MainWindow main;

        public AwbHost(MainWindow main, Session session)
        {
            this.main = main;
            TheSession = session;
        }

        public Session TheSession { get; }
        public bool PreParseMode => false;
        public string StatusLabelText
        {
            get => main.StatusText;
            set => Ui.OnUi(() => main.StatusText = value);
        }
        public void NotifyBalloon(string message, ToolTipIcon icon) => StatusLabelText = message;

        // Info
        public Version AWBVersion => typeof(Variables).Assembly.GetName().Version;
        public Version WikiFunctionsVersion => typeof(Variables).Assembly.GetName().Version;
        public string AWBVersionString => AWBVersion.ToString();
        public string WikiFunctionsVersionString => WikiFunctionsVersion.ToString();
        public string WikiDiffVersionString => WikiFunctionsVersionString;
        public int NumberOfEdits => main.NumberOfEdits;
        public int NumberOfNewPages => 0;
        public int NumberOfIgnoredEdits => 0;
        public int NumberOfEditsPerMinute => 0;
        public int NumberOfPagesPerMinute => 0;
        public int Nudges => 0;
        public ProjectEnum Project => Variables.Project;
        public string LangCode => Variables.LangCode;
        public bool CheckStatus(bool login) => TheSession.Update() == WikiStatusResult.Registered;
        public bool Privacy => false;
        public bool Shutdown => false;

        // Commands (used by plugins; stage 4)
        public void ShowHelp(string url) => MacSystem.OpenUrl(url);
        public void ShowHelpEnWiki(string article) => MacSystem.OpenUrl("https://en.wikipedia.org/wiki/" + article);
        public void Start(IAWBPlugin sender) => Ui.OnUi(main.StartProcessing);
        public void Start(string sender) => Ui.OnUi(main.StartProcessing);
        public void Stop(IAWBPlugin sender) => Ui.OnUi(main.StopProcessing);
        public void Stop(string sender) => Ui.OnUi(main.StopProcessing);
        public void Save(IAWBPlugin sender) => Ui.OnUi(main.SaveCurrent);
        public void Save(string sender) => Ui.OnUi(main.SaveCurrent);
        public void AddLogItem(bool skipped, AWBLogListener logListener) { }
        public void SkipPage(IAWBPlugin sender, string reason) => Ui.OnUi(() => main.SkipCurrent(reason));
        public void SkipPage(string sender, string reason) => Ui.OnUi(() => main.SkipCurrent(reason));
        public void GetDiff(IAWBPlugin sender) => Ui.OnUi(main.ShowDiff);
        public void GetDiff(string sender) => Ui.OnUi(main.ShowDiff);
        public void GetPreview(IAWBPlugin sender) { }
        public void GetPreview(string sender) { }
        public void AddMainFormClosingEventHandler(FormClosingEventHandler handler) { }
        public void StartProgressBar() => Ui.OnUi(() => main.IsBusy = true);
        public void StopProgressBar() => Ui.OnUi(() => main.IsBusy = false);
        public void AddArticleRedirectedEventHandler(ArticleRedirected handler) { }
        public void LoadTypos(bool reload) => main.TyposNeedReload = true;

        public TraceManager TraceManager => null;
        public bool SkipNoChanges { get; set; } = true;
        public string CustomModule => "";
    }

    /// <summary>The skip options the general fixes consult (AWB's "More skip options" dialog)</summary>
    internal sealed class MacSkipOptions : ISkipOptions
    {
        public bool SkipNoUnicode { get; set; }
        public bool SkipNoTag { get; set; }
        public bool SkipNoHeaderError { get; set; }
        public bool SkipNoBoldTitle { get; set; }
        public bool SkipNoBulletedLink { get; set; }
        public bool SkipNoBadLink { get; set; }
        public bool SkipNoDefaultSortAdded { get; set; }
        public bool SkipNoUserTalkTemplatesSubstd { get; set; }
        public bool SkipNoCiteTemplateDatesFixed { get; set; }
        public bool SkipNoPeopleCategoriesFixed { get; set; }
    }

    internal static class MacSystem
    {
        public static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start("/usr/bin/open", "\"" + url + "\""); }
            catch { }
        }
    }
}

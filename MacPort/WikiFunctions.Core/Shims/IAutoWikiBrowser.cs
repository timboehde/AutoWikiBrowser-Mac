// Mac version of AWB/WikiFunctions/Plugin/IAutoWikiBrowser.cs.
// The original exposes WinForms controls (buttons, tabs, menus) directly; those members are removed here.
// The engine itself only uses TheSession and LoadTypos. UI-level members will come back as
// toolkit-neutral equivalents when the plugin layer is ported (stage 4).
using System;
using System.Windows.Forms;
using WikiFunctions.Logging;

namespace WikiFunctions.Plugin
{
    public interface IAutoWikiBrowserForm
    {
        Session TheSession { get; }
        bool PreParseMode { get; }
        string StatusLabelText { get; set; }
        void NotifyBalloon(string message, ToolTipIcon icon);
    }

    public interface IAutoWikiBrowserInfo
    {
        Version AWBVersion { get; }
        Version WikiFunctionsVersion { get; }
        string AWBVersionString { get; }
        string WikiFunctionsVersionString { get; }
        string WikiDiffVersionString { get; }
        int NumberOfEdits { get; }
        int NumberOfNewPages { get; }
        int NumberOfIgnoredEdits { get; }
        int NumberOfEditsPerMinute { get; }
        int NumberOfPagesPerMinute { get; }
        int Nudges { get; }
        ProjectEnum Project { get; }
        string LangCode { get; }
        bool CheckStatus(bool login);
        bool Privacy { get; }
        bool Shutdown { get; }
    }

    public interface IAutoWikiBrowserCommands
    {
        void ShowHelp(string url);
        void ShowHelpEnWiki(string article);
        void Start(IAWBPlugin sender);
        void Start(string sender);
        void Stop(IAWBPlugin sender);
        void Stop(string sender);
        void Save(IAWBPlugin sender);
        void Save(string sender);
        void AddLogItem(bool skipped, AWBLogListener logListener);
        void SkipPage(IAWBPlugin sender, string reason);
        void SkipPage(string sender, string reason);
        void GetDiff(IAWBPlugin sender);
        void GetDiff(string sender);
        void GetPreview(IAWBPlugin sender);
        void GetPreview(string sender);
        void AddMainFormClosingEventHandler(FormClosingEventHandler handler);
        void StartProgressBar();
        void StopProgressBar();
        void AddArticleRedirectedEventHandler(ArticleRedirected handler);
        void LoadTypos(bool reload);
    }

    [CLSCompliant(true)]
    public interface IAutoWikiBrowser : IAutoWikiBrowserForm, IAutoWikiBrowserCommands, IAutoWikiBrowserInfo
    {
        TraceManager TraceManager { get; }
        bool SkipNoChanges { get; set; }
        string CustomModule { get; }
    }
}

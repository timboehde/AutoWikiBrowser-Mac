// The bridge between the engine and whatever UI is running it.
// The Avalonia app sets UiHost.Current at startup; until then a headless default is used
// (messages go to stderr, questions answer No/Cancel), which is also what unit tests get.
using System;
using System.Threading;
using System.Windows.Forms;
using WikiFunctions.Controls;
using WikiFunctions.Controls.Lists;

namespace WikiFunctions.Mac
{
    public interface IProgressDisplay
    {
        string Status { get; set; }
        void SetProgress(int completed, int total);
        void Close();
    }

    public interface IUiHost
    {
        DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon);
        void SetClipboardText(string text);
        string GetClipboardText();
        /// <summary>Called from engine loops that used to spin Application.DoEvents()</summary>
        void DoEvents();
        InputBoxResult PromptText(string prompt, string title, string defaultText, InputBoxValidatingHandler validator);
        /// <summary>Returns -1 if cancelled</summary>
        int GetNumber(bool edits, int max);
        /// <summary>Let the user fill in a move/delete/protect dialog. Return true for OK.</summary>
        bool ShowArticleAction(ArticleActionDialog dialog);
        /// <summary>Let the user pick protection type/level for the protected-pages list. Return true for OK.</summary>
        bool ShowProtectionLevel(ProtectionLevel picker);
        /// <summary>Interactive disambiguation. Return the new text; set abort to stop processing.</summary>
        string Disambiguate(Session session, string articleText, string articleTitle, string dabLink,
                            string[] dabVariants, int contextChars, bool botMode, out bool noChange, out bool abort);
        IProgressDisplay ShowProgress(string title);
        /// <summary>Unexpected exception: show the message and offer the bug report text (Phabricator format)</summary>
        void ShowError(string subject, string message, string bugReport);
        void ShowDatabaseScanner(ListMaker listMaker);
    }

    public static class UiHost
    {
        public static IUiHost Current { get; set; } = new HeadlessUiHost();
    }

    public class HeadlessUiHost : IUiHost
    {
        private string clipboard = "";

        public virtual DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Console.Error.WriteLine($"[{caption}] {text}");
            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                case MessageBoxButtons.YesNoCancel: return DialogResult.No;
                case MessageBoxButtons.OKCancel:
                case MessageBoxButtons.RetryCancel: return DialogResult.Cancel;
                case MessageBoxButtons.AbortRetryIgnore: return DialogResult.Abort;
                default: return DialogResult.OK;
            }
        }
        public virtual void SetClipboardText(string text) => clipboard = text ?? "";
        public virtual string GetClipboardText() => clipboard;
        public virtual void DoEvents() => Thread.Sleep(10);
        public virtual InputBoxResult PromptText(string prompt, string title, string defaultText, InputBoxValidatingHandler validator)
            => new InputBoxResult { OK = false, Text = defaultText };
        public virtual int GetNumber(bool edits, int max) => -1;
        public virtual bool ShowArticleAction(ArticleActionDialog dialog) => false;
        public virtual bool ShowProtectionLevel(ProtectionLevel picker) => false;
        public virtual string Disambiguate(Session session, string articleText, string articleTitle, string dabLink,
            string[] dabVariants, int contextChars, bool botMode, out bool noChange, out bool abort)
        { noChange = true; abort = false; return articleText; }
        public virtual IProgressDisplay ShowProgress(string title) => new NullProgress();
        public virtual void ShowError(string subject, string message, string bugReport) =>
            Console.Error.WriteLine($"[Error] {subject}: {message}\n{bugReport}");
        public virtual void ShowDatabaseScanner(ListMaker listMaker) =>
            ShowMessage("The database scanner isn't available yet on the Mac.", "AutoWikiBrowser", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private class NullProgress : IProgressDisplay
        {
            public string Status { get; set; } = "";
            public void SetProgress(int completed, int total) { }
            public void Close() { }
        }
    }
}

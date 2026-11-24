// The Mac UI's implementation of the engine's UiHost: every dialog the engine asks for.
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AutoWikiBrowser.Mac.Dialogs;
using WikiFunctions;
using WikiFunctions.Controls;
using WikiFunctions.Controls.Lists;
using WikiFunctions.Mac;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace AutoWikiBrowser.Mac
{
    internal sealed class AvaloniaUiHost : HeadlessUiHost
    {
        private readonly MainWindow main;

        public AvaloniaUiHost(MainWindow main) { this.main = main; }

        public override DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => Ui.RunSync(() => MessageDialog.ShowAsync(main, text, caption, buttons, icon));

        public override void SetClipboardText(string text)
            => Ui.RunSync(async () => { if (main.Clipboard != null) await main.Clipboard.SetTextAsync(text ?? ""); });

        public override string GetClipboardText()
            => Ui.RunSync(async () => main.Clipboard == null ? "" : (await main.Clipboard.GetTextAsync() ?? ""));

        public override void DoEvents()
        {
            if (Dispatcher.UIThread.CheckAccess()) Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        public override InputBoxResult PromptText(string prompt, string title, string defaultText, InputBoxValidatingHandler validator)
            => Ui.RunSync(() => PromptDialog.ShowAsync(main, prompt, title, defaultText, validator));

        public override int GetNumber(bool edits, int max)
        {
            var r = Ui.RunSync(() => PromptDialog.ShowAsync(main,
                (edits ? "Number of edits" : "Number of levels") + " (1–" + max + "):", "AutoWikiBrowser", "1",
                (s, e) =>
                {
                    if (!int.TryParse(e.Text, out int n) || n < 1 || n > max)
                    { e.Cancel = true; e.Message = "Enter a number from 1 to " + max; }
                }));
            return r.OK && int.TryParse(r.Text, out int v) ? v : -1;
        }

        public override bool ShowArticleAction(ArticleActionDialog dialog)
        {
            ShowMessage("Moving, deleting and protecting pages aren't available on the Mac yet.",
                "Not available yet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        public override bool ShowProtectionLevel(ProtectionLevel picker)
            => Ui.RunSync(() => ProtectionLevelDialog.ShowAsync(main, picker));

        public override IProgressDisplay ShowProgress(string title) => new StatusBarProgress(main);

        public override void ShowError(string subject, string message, string bugReport)
        {
            base.ShowError(subject, message, bugReport);
            Ui.RunSync(() => ErrorDialog.ShowAsync(main, subject, message, bugReport));
        }

        /// <summary>"Please wait" progress shown in the main window's status bar</summary>
        private sealed class StatusBarProgress : IProgressDisplay
        {
            private readonly MainWindow main;
            private string status = "";

            public StatusBarProgress(MainWindow main)
            {
                this.main = main;
                Ui.OnUi(() => main.IsBusy = true);
            }

            public string Status
            {
                get => status;
                set { status = value; Ui.OnUi(() => main.StatusText = value); }
            }

            public void SetProgress(int completed, int total)
                => Ui.OnUi(() => main.StatusText = status + " (" + completed + "/" + total + ")");

            public void Close() => Ui.OnUi(() => main.IsBusy = false);
        }
    }
}

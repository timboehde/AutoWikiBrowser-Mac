using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace AutoWikiBrowser.Mac
{
    /// <summary>
    /// The engine expects modal dialogs to block (WinForms style). Avalonia dialogs are async, so these
    /// helpers wait for them: on the UI thread by running a nested dispatcher loop, on other threads by
    /// posting to the UI thread and blocking until the result arrives.
    /// </summary>
    internal static class Ui
    {
        public static T RunSync<T>(Func<Task<T>> show)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Task<T> task = show();
                if (!task.IsCompleted)
                {
                    using (var cts = new CancellationTokenSource())
                    {
                        task.ContinueWith(_ => Dispatcher.UIThread.Post(() => cts.Cancel()), TaskScheduler.Default);
                        Dispatcher.UIThread.MainLoop(cts.Token);
                    }
                }
                return task.GetAwaiter().GetResult();
            }

            var tcs = new TaskCompletionSource<T>();
            Dispatcher.UIThread.Post(async () =>
            {
                try { tcs.SetResult(await show()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task.GetAwaiter().GetResult();
        }

        public static void RunSync(Func<Task> show) => RunSync(async () => { await show(); return true; });

        /// <summary>Run an action on the UI thread (now if already there)</summary>
        public static void OnUi(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        }

        /// <summary>CRLF (engine) → LF (Mac text box)</summary>
        public static string ToLf(string text) => (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
    }
}

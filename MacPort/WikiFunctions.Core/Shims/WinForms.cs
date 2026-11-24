// Minimal stand-ins for the System.Windows.Forms types the WikiFunctions engine touches,
// so the original AWB source files compile unchanged on macOS.
// Anything user-facing is routed through WikiFunctions.Mac.UiHost, which the Avalonia app replaces.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using WikiFunctions.Mac;

namespace System.Windows.Forms
{
    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }
    public enum MessageBoxButtons { OK, OKCancel, AbortRetryIgnore, YesNoCancel, YesNo, RetryCancel }
    public enum MessageBoxIcon { None = 0, Hand = 16, Stop = 16, Error = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }
    public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }
    public enum ToolTipIcon { None, Info, Warning, Error }

    public interface IWin32Window { IntPtr Handle { get; } }

    public static class MessageBox
    {
        public static DialogResult Show(string text) => Show(text, "AutoWikiBrowser");
        public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            => UiHost.Current.ShowMessage(text, caption, buttons, icon);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
            => Show(text, caption, buttons, icon);
        public static DialogResult Show(IWin32Window owner, string text) => Show(text);
        public static DialogResult Show(IWin32Window owner, string text, string caption) => Show(text, caption);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) => Show(text, caption, buttons, icon);
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) => Show(text, caption, buttons, icon);
    }

    public static class Clipboard
    {
        public static void Clear() => UiHost.Current.SetClipboardText("");
        public static void SetText(string text) => UiHost.Current.SetClipboardText(text);
        public static void SetDataObject(object data) => UiHost.Current.SetClipboardText(data?.ToString() ?? "");
        public static void SetDataObject(object data, bool copy) => SetDataObject(data);
        public static string GetText() => UiHost.Current.GetClipboardText();
        public static bool ContainsText() => !string.IsNullOrEmpty(GetText());
    }

    public static class Application
    {
        public static string StartupPath => AppContext.BaseDirectory.TrimEnd('/', '\\');
        public static string ExecutablePath => Environment.ProcessPath ?? StartupPath;
        public static void DoEvents() => UiHost.Current.DoEvents();
        public static string ProductName { get; set; } = "AutoWikiBrowser";
        public static string ProductVersion { get; set; } =
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    /// <summary>Thread-marshalling stand-in for a WinForms control: Invoke runs on the
    /// SynchronizationContext that was current when the object was created (the UI thread).</summary>
    public class Control : IWin32Window, IDisposable
    {
        private readonly SynchronizationContext context = SynchronizationContext.Current;

        public IntPtr Handle => IntPtr.Zero;
        public string Text { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public bool Visible { get; set; }
        public object Tag { get; set; }
        public bool IsDisposed { get; private set; }
        public bool InvokeRequired => context != null && SynchronizationContext.Current != context;
        public System.Drawing.Point Location { get; set; }
        public Control Parent { get; set; }
        public ControlCollection Controls { get; }
        public Control() { Controls = new ControlCollection(this); }
        public void Hide() => Visible = false;
        public void Show() => Visible = true;

        public class ControlCollection : List<Control>
        {
            private readonly Control owner;
            public ControlCollection(Control owner) { this.owner = owner; }
            public new void Add(Control c) { c.Parent = owner; base.Add(c); }
            public new bool Remove(Control c) { if (c.Parent == owner) c.Parent = null; return base.Remove(c); }
        }

        public object Invoke(Delegate method) => Invoke(method, null);
        public object Invoke(Delegate method, params object[] args)
        {
            if (!InvokeRequired) return method.DynamicInvoke(args);
            object result = null;
            Exception error = null;
            context.Send(_ => { try { result = method.DynamicInvoke(args); } catch (Exception ex) { error = ex; } }, null);
            if (error != null) throw error;
            return result;
        }
        public void BeginInvoke(Delegate method, params object[] args)
        {
            if (context == null) ThreadPool.QueueUserWorkItem(_ => method.DynamicInvoke(args));
            else context.Post(_ => method.DynamicInvoke(args), null);
        }
        public virtual void Dispose() { IsDisposed = true; }
    }

    public class FormClosingEventArgs : CancelEventArgs { }
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);

    /// <summary>Base for engine-side dialog stubs. Subclasses override ShowDialog to ask the UiHost.</summary>
    public class Form : Control
    {
        public DialogResult DialogResult { get; set; } = DialogResult.None;
        public virtual DialogResult ShowDialog() => DialogResult;
        public DialogResult ShowDialog(IWin32Window owner) => ShowDialog();
        public new virtual void Show() { Visible = true; }
        public void Show(IWin32Window owner) => Show();
        public virtual void Close() { Visible = false; }
    }

    public class TreeNode
    {
        public TreeNode() { Nodes = new TreeNodeCollection(this); }
        public TreeNode(string text) : this() { Text = text; }
        public string Text { get; set; } = "";
        public string Name { get; set; } = "";
        public object Tag { get; set; }
        public bool Checked { get; set; }
        public TreeNode Parent { get; internal set; }
        public TreeNodeCollection Nodes { get; }

        /// <summary>Copies this node and its subtree (Tag is shared, as in WinForms)</summary>
        public object Clone()
        {
            var copy = new TreeNode(Text) { Name = Name, Tag = Tag, Checked = Checked };
            foreach (TreeNode child in Nodes) copy.Nodes.Add((TreeNode)child.Clone());
            return copy;
        }
    }

    public class ComboBox : Control
    {
        public ObjectCollection Items { get; } = new ObjectCollection();
        public object SelectedItem { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public class ObjectCollection : List<object> { }
    }

    public class TreeNodeCollection : Collection<TreeNode>
    {
        private readonly TreeNode owner;
        internal TreeNodeCollection(TreeNode owner) { this.owner = owner; }
        public TreeNode Add(string text) { var n = new TreeNode(text); Add(n); return n; }
        protected override void InsertItem(int index, TreeNode item) { item.Parent = owner; base.InsertItem(index, item); }
    }

    public class ListBox : Control
    {
        public List<object> Items { get; } = new List<object>();
        public List<object> SelectedItems { get; } = new List<object>();
    }

    public class ListView : Control
    {
        public List<ListViewItem> Items { get; } = new List<ListViewItem>();
        public List<ListViewItem> SelectedItems { get; } = new List<ListViewItem>();
        public void BeginUpdate() { }
        public void EndUpdate() { }
    }

    public class ListViewItem
    {
        public ListViewItem() { SubItems = new ListViewSubItemCollection(this); }
        public ListViewItem(string text) : this() { Text = text; }

        public string Text
        {
            get => SubItems.Count > 0 ? SubItems[0].Text : "";
            set { if (SubItems.Count == 0) SubItems.Add(value); else SubItems[0].Text = value; }
        }
        public string ToolTipText { get; set; } = "";
        public object Tag { get; set; }
        public bool Selected { get; set; }
        public bool Checked { get; set; }
        public ListView ListView { get; internal set; }
        public ListViewSubItemCollection SubItems { get; }

        public class ListViewSubItem
        {
            public ListViewSubItem() { }
            public ListViewSubItem(ListViewItem owner, string text) { Text = text; }
            public string Text { get; set; } = "";
        }

        public class ListViewSubItemCollection : List<ListViewSubItem>
        {
            internal ListViewSubItemCollection(ListViewItem owner) { }
            public ListViewSubItem Add(string text) { var s = new ListViewSubItem { Text = text }; Add(s); return s; }
        }
    }

    /// <summary>The Mac app picks files itself and passes the paths in; this never shows UI.</summary>
    public class OpenFileDialog : IDisposable
    {
        public string Filter { get; set; } = "";
        public int FilterIndex { get; set; } = 1;
        public bool Multiselect { get; set; }
        public string Title { get; set; } = "";
        public string InitialDirectory { get; set; } = "";
        public string FileName { get; set; } = "";
        public string[] FileNames { get; set; } = new string[0];
        public DialogResult ShowDialog() => DialogResult.Cancel;
        public DialogResult ShowDialog(IWin32Window owner) => DialogResult.Cancel;
        public void Dispose() { }
    }
}

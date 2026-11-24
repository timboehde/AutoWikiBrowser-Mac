// Editor for Replace Special ("advanced find and replace") rules. Reads and writes the rule objects
// the same way as the Windows rule controls (RuleControl / InTemplateRuleControl /
// TemplateParamRuleControl SaveToRule and RestoreFromRule). Works on a copy; Done commits it.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WikiFunctions.ReplaceSpecial;

namespace AutoWikiBrowser.Mac
{
    public sealed class RuleNode : INotifyPropertyChanged
    {
        public RuleNode(IRule rule) { Rule = rule; }
        public IRule Rule { get; }
        public RuleNode Parent { get; set; }
        public ObservableCollection<RuleNode> Children { get; } = new ObservableCollection<RuleNode>();

        public string Display
        {
            get
            {
                string kind = Rule is InTemplateRule ? "  [in template]" : Rule is TemplateParamRule ? "  [parameter]" : "";
                string name = string.IsNullOrWhiteSpace(Rule.Name) ? "(unnamed)" : Rule.Name;
                return (Rule.enabled_ ? "" : "⊘ ") + name + kind;
            }
        }

        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class ReplaceSpecialWindow : Window
    {
        private readonly WikiFunctions.ReplaceSpecial.ReplaceSpecial target;
        private readonly ObservableCollection<RuleNode> roots = new ObservableCollection<RuleNode>();
        private RuleNode selected;
        private bool loading;

        private readonly TreeView tree;
        private readonly Control nothingSelected, commonPanel, rulePanel, inTemplatePanel, paramPanel;
        private readonly TextBox nameBox, replaceBox, withBox, ifContainsBox, ifNotContainsBox, templateNamesBox,
            renameTemplateBox, paramNameBox, newParamNameBox;
        private readonly CheckBox enabledCheck, regexCheck, caseCheck, multilineCheck, singlelineCheck, ifRegexCheck,
            ifCaseCheck, ifMultilineCheck, ifSinglelineCheck, renameTemplateCheck, asSubruleCheck;
        private readonly ComboBox ruleTypeBox;
        private readonly NumericUpDown timesBox;

        public ReplaceSpecialWindow() : this(new WikiFunctions.ReplaceSpecial.ReplaceSpecial()) { }

        public ReplaceSpecialWindow(WikiFunctions.ReplaceSpecial.ReplaceSpecial target)
        {
            this.target = target;
            AvaloniaXamlLoader.Load(this);
            T C<T>(string n) where T : Control => this.FindControl<T>(n);

            tree = C<TreeView>("RuleTree");
            nothingSelected = C<Control>("NothingSelected"); commonPanel = C<Control>("CommonPanel");
            rulePanel = C<Control>("RulePanel"); inTemplatePanel = C<Control>("InTemplatePanel"); paramPanel = C<Control>("ParamPanel");
            nameBox = C<TextBox>("NameBox"); replaceBox = C<TextBox>("ReplaceBox"); withBox = C<TextBox>("WithBox");
            ifContainsBox = C<TextBox>("IfContainsBox"); ifNotContainsBox = C<TextBox>("IfNotContainsBox");
            templateNamesBox = C<TextBox>("TemplateNamesBox"); renameTemplateBox = C<TextBox>("RenameTemplateBox");
            paramNameBox = C<TextBox>("ParamNameBox"); newParamNameBox = C<TextBox>("NewParamNameBox");
            enabledCheck = C<CheckBox>("EnabledCheck"); regexCheck = C<CheckBox>("RegexCheck"); caseCheck = C<CheckBox>("CaseCheck");
            multilineCheck = C<CheckBox>("MultilineCheck"); singlelineCheck = C<CheckBox>("SinglelineCheck");
            ifRegexCheck = C<CheckBox>("IfRegexCheck"); ifCaseCheck = C<CheckBox>("IfCaseCheck");
            ifMultilineCheck = C<CheckBox>("IfMultilineCheck"); ifSinglelineCheck = C<CheckBox>("IfSinglelineCheck");
            renameTemplateCheck = C<CheckBox>("RenameTemplateCheck"); asSubruleCheck = C<CheckBox>("AsSubruleCheck");
            ruleTypeBox = C<ComboBox>("RuleTypeBox"); timesBox = C<NumericUpDown>("TimesBox");

            ruleTypeBox.ItemsSource = new[] { "on the whole page", "inside templates" };
            ruleTypeBox.SelectedIndex = 0;

            foreach (IRule r in target.GetRules()) roots.Add(ToNode(DeepClone(r), null));
            tree.ItemsSource = roots;
            tree.SelectionChanged += (s, e) => Select(tree.SelectedItem as RuleNode);

            nameBox.TextChanged += (s, e) => { if (!loading && selected != null) { selected.Rule.Name = (nameBox.Text ?? "").Trim(); selected.Refresh(); } };
            enabledCheck.IsCheckedChanged += (s, e) => { if (!loading && selected != null) { selected.Rule.enabled_ = enabledCheck.IsChecked == true; selected.Refresh(); } };
            regexCheck.IsCheckedChanged += (s, e) => UpdateRegexOptions();
            ifRegexCheck.IsCheckedChanged += (s, e) => UpdateRegexOptions();
            renameTemplateCheck.IsCheckedChanged += (s, e) => renameTemplateBox.IsEnabled = renameTemplateCheck.IsChecked == true;

            C<Button>("NewRuleButton").Click += (s, e) => Add(new Rule());
            C<Button>("NewInTemplateButton").Click += (s, e) => Add(new InTemplateRule());
            C<Button>("NewParamButton").Click += (s, e) => Add(new TemplateParamRule());
            C<Button>("DeleteButton").Click += (s, e) => Delete();
            C<Button>("UpButton").Click += (s, e) => Move(-1);
            C<Button>("DownButton").Click += (s, e) => Move(1);
            C<Button>("CancelButton").Click += (s, e) => Close(false);
            C<Button>("OkButton").Click += (s, e) => Commit();

            Select(null);
        }

        #region Tree
        /// <summary>IRule.Clone is a shallow MemberwiseClone; copy subrules and template name lists too</summary>
        private static IRule DeepClone(IRule r)
        {
            var copy = (IRule)r.Clone();
            if (copy is InTemplateRule it && r is InTemplateRule original)
                it.TemplateNames_ = new List<string>(original.TemplateNames_ ?? new List<string>());
            copy.Children = r.Children?.Select(DeepClone).ToList();
            return copy;
        }

        private static RuleNode ToNode(IRule r, RuleNode parent)
        {
            var node = new RuleNode(r) { Parent = parent };
            if (r.Children != null)
                foreach (IRule c in r.Children) node.Children.Add(ToNode(c, node));
            return node;
        }

        private static IRule FromNode(RuleNode n)
        {
            n.Rule.Children = n.Children.Count == 0 ? null : n.Children.Select(FromNode).ToList();
            return n.Rule;
        }

        private ObservableCollection<RuleNode> SiblingsOf(RuleNode n) => n.Parent == null ? roots : n.Parent.Children;

        private void Add(IRule rule)
        {
            Save();
            if (rule is Rule) rule.Name = "Rule";
            else if (rule is InTemplateRule) rule.Name = "In template";
            else rule.Name = "Template parameter";

            RuleNode node;
            if (asSubruleCheck.IsChecked == true && selected != null)
            {
                node = new RuleNode(rule) { Parent = selected };
                selected.Children.Add(node);
            }
            else if (selected != null)
            {
                var siblings = SiblingsOf(selected);
                node = new RuleNode(rule) { Parent = selected.Parent };
                siblings.Insert(siblings.IndexOf(selected) + 1, node);
            }
            else
            {
                node = new RuleNode(rule);
                roots.Add(node);
            }
            tree.SelectedItem = node;
            Select(node);
            nameBox.Focus();
            nameBox.SelectAll();
        }

        private void Delete()
        {
            if (selected == null) return;
            SiblingsOf(selected).Remove(selected);
            selected = null;
            tree.SelectedItem = null;
            Select(null);
        }

        private void Move(int delta)
        {
            if (selected == null) return;
            var siblings = SiblingsOf(selected);
            int i = siblings.IndexOf(selected), j = i + delta;
            if (j < 0 || j >= siblings.Count) return;
            var node = selected;
            Save();
            siblings.Move(i, j);
            tree.SelectedItem = node;
        }
        #endregion

        #region Editing (as SaveToRule / RestoreFromRule in the Windows controls)
        private void Select(RuleNode node)
        {
            Save();
            selected = node;
            loading = true;

            bool any = node != null;
            nothingSelected.IsVisible = !any;
            commonPanel.IsVisible = any;
            rulePanel.IsVisible = node?.Rule is Rule;
            inTemplatePanel.IsVisible = node?.Rule is InTemplateRule;
            paramPanel.IsVisible = node?.Rule is TemplateParamRule;

            if (any)
            {
                nameBox.Text = node.Rule.Name;
                enabledCheck.IsChecked = node.Rule.enabled_;
            }
            switch (node?.Rule)
            {
                case Rule r:
                    ruleTypeBox.SelectedIndex = (int)r.ruletype_;
                    timesBox.Value = r.numoftimes_;
                    replaceBox.Text = r.replace_;
                    withBox.Text = r.with_;
                    regexCheck.IsChecked = r.regex_;
                    caseCheck.IsChecked = (r.regexOptions_ & RegexOptions.IgnoreCase) == 0;
                    multilineCheck.IsChecked = (r.regexOptions_ & RegexOptions.Multiline) != 0;
                    singlelineCheck.IsChecked = (r.regexOptions_ & RegexOptions.Singleline) != 0;
                    ifContainsBox.Text = r.ifContains_;
                    ifNotContainsBox.Text = r.ifNotContains_;
                    ifRegexCheck.IsChecked = r.ifIsRegex_;
                    ifCaseCheck.IsChecked = (r.ifRegexOptions_ & RegexOptions.IgnoreCase) == 0;
                    ifMultilineCheck.IsChecked = (r.ifRegexOptions_ & RegexOptions.Multiline) != 0;
                    ifSinglelineCheck.IsChecked = (r.ifRegexOptions_ & RegexOptions.Singleline) != 0;
                    break;
                case InTemplateRule t:
                    templateNamesBox.Text = string.Join("\n", t.TemplateNames_ ?? new List<string>());
                    renameTemplateCheck.IsChecked = t.DoReplace_;
                    renameTemplateBox.Text = t.ReplaceWith_;
                    renameTemplateBox.IsEnabled = t.DoReplace_;
                    break;
                case TemplateParamRule p:
                    paramNameBox.Text = p.ParamName;
                    newParamNameBox.Text = p.NewParamName;
                    break;
            }
            UpdateRegexOptions();
            loading = false;
        }

        private void Save()
        {
            if (selected == null || loading) return;
            IRule rule = selected.Rule;
            rule.Name = (nameBox.Text ?? "").Trim();
            rule.enabled_ = enabledCheck.IsChecked == true;
            switch (rule)
            {
                case Rule r:
                    r.ruletype_ = (Rule.T)System.Math.Max(0, ruleTypeBox.SelectedIndex);
                    r.numoftimes_ = (int)(timesBox.Value ?? 1);
                    r.replace_ = (replaceBox.Text ?? "").Replace("\r\n", "\n");
                    r.with_ = (withBox.Text ?? "").Replace("\r\n", "\n");
                    r.regex_ = regexCheck.IsChecked == true;
                    r.regexOptions_ = RegexOptions.None;
                    if (caseCheck.IsChecked != true) r.regexOptions_ |= RegexOptions.IgnoreCase;
                    if (singlelineCheck.IsChecked == true) r.regexOptions_ |= RegexOptions.Singleline;
                    if (multilineCheck.IsChecked == true) r.regexOptions_ |= RegexOptions.Multiline;
                    r.ifContains_ = ifContainsBox.Text ?? "";
                    r.ifNotContains_ = ifNotContainsBox.Text ?? "";
                    r.ifIsRegex_ = ifRegexCheck.IsChecked == true;
                    r.ifRegexOptions_ = RegexOptions.None;
                    if (ifCaseCheck.IsChecked != true) r.ifRegexOptions_ |= RegexOptions.IgnoreCase;
                    if (ifSinglelineCheck.IsChecked == true) r.ifRegexOptions_ |= RegexOptions.Singleline;
                    if (ifMultilineCheck.IsChecked == true) r.ifRegexOptions_ |= RegexOptions.Multiline;
                    break;
                case InTemplateRule t:
                    t.DoReplace_ = renameTemplateCheck.IsChecked == true;
                    t.ReplaceWith_ = (renameTemplateBox.Text ?? "").Trim();
                    t.TemplateNames_ = (templateNamesBox.Text ?? "").Replace("\r\n", "\n").Split('\n')
                        .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    break;
                case TemplateParamRule p:
                    p.ParamName = (paramNameBox.Text ?? "").Trim();
                    p.NewParamName = (newParamNameBox.Text ?? "").Trim();
                    break;
            }
            selected.Refresh();
        }

        private void UpdateRegexOptions()
        {
            multilineCheck.IsEnabled = singlelineCheck.IsEnabled = regexCheck.IsChecked == true;
            ifMultilineCheck.IsEnabled = ifSinglelineCheck.IsEnabled = ifRegexCheck.IsChecked == true;
        }

        private void Commit()
        {
            Save();
            target.AddNewRule(roots.Select(FromNode).ToList());
            Close(true);
        }
        #endregion
    }
}

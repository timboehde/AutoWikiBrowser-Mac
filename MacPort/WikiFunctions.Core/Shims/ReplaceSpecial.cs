// Mac version of AWB/WikiFunctions/ReplaceSpecial/ReplaceSpecial.cs ("Advanced find and replace").
// The rule classes (Rule, InTemplateRule, TemplateParamRule) are compiled unchanged from the original.
// In the original, the rule tree lives in a WinForms TreeView whose node Tags are the rules; each rule's
// subrules are also kept in IRule.Children, which is what settings files store. Here the rules are a
// plain list, and the TreeNode tree that Apply() walks is built from Children when rules are applied.
using System.Collections.Generic;
using System.Windows.Forms;

namespace WikiFunctions.ReplaceSpecial
{
    public class ReplaceSpecial
    {
        private List<IRule> rules = new List<IRule>();

        public int NoOfRules => rules.Count;
        public bool HasRules => NoOfRules != 0;

        public void Clear() => rules.Clear();

        /// <summary>Top-level rules, with subrules in Children (same shape as the original's GetRules)</summary>
        public List<IRule> GetRules() => rules;

        /// <summary>Replaces the rules (original: AddNewRule(List&lt;IRule&gt;) clears the tree first)</summary>
        public void AddNewRule(List<IRule> newRules) => rules = newRules == null ? new List<IRule>() : new List<IRule>(newRules);

        public string ApplyRules(string text, string title)
        {
            foreach (IRule r in rules)
                text = r.Apply(ToTreeNode(r), text, title);
            return text;
        }

        public static TreeNode ToTreeNode(IRule rule)
        {
            var node = new TreeNode(rule.Name) { Tag = rule };
            if (rule.Children != null)
                foreach (IRule child in rule.Children)
                    node.Nodes.Add(ToTreeNode(child));
            return node;
        }
    }
}

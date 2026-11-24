// Stand-ins for the Replace Special rule editor controls. The rule classes reference these, but on the
// Mac the editor is part of the Avalonia app, so they are never shown.
using System.Windows.Forms;

namespace WikiFunctions.ReplaceSpecial
{
    public class RuleControl : Control
    {
        public RuleControl(IRuleControlOwner owner) { }
        public void SelectName() { }
        public void SaveToRule(Rule rule) { }
        public void RestoreFromRule(Rule rule) { }
    }

    public class InTemplateRuleControl : Control
    {
        public InTemplateRuleControl(IRuleControlOwner owner) { }
        public void SelectName() { }
        public void SaveToRule(InTemplateRule rule) { }
        public void RestoreFromRule(InTemplateRule rule) { }
    }

    public class TemplateParamRuleControl : Control
    {
        public TemplateParamRuleControl(IRuleControlOwner owner) { }
        public void SelectName() { }
        public void SaveToRule(TemplateParamRule rule) { }
        public void RestoreFromRule(TemplateParamRule rule) { }
    }
}

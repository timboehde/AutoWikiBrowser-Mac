// Mac-only: settings files must stay compatible with Windows AWB (same XmlSerializer format).
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using WikiFunctions;
using WikiFunctions.AWBSettings;
using WikiFunctions.Parse;
using WikiFunctions.ReplaceSpecial;

namespace UnitTests
{
    [TestFixture]
    public class MacSettingsTests : RequiresInitialization
    {
        [Test]
        public void SettingsRoundTrip()
        {
            var far = new FindandReplace();
            far.AddNew(new Replacement("foo", "bar", false, true, false, false, RegexOptions.IgnoreCase, "a comment"));
            far.IgnoreMore = true;

            var rule = new Rule { Name = "Parent", replace_ = "a", with_ = "b" };
            rule.Children = new List<IRule> { new Rule { Name = "Child", replace_ = "b", with_ = "c" } };
            var advanced = new WikiFunctions.ReplaceSpecial.ReplaceSpecial();
            advanced.AddNewRule(new List<IRule> { rule, new InTemplateRule { Name = "Templates" } });

            var prefs = new UserPrefs();
            prefs.FindAndReplace = new FaRPrefs(far, advanced, new WikiFunctions.SubstTemplates()) { Enabled = true };
            prefs.List.ArticleList = new List<Article> { new Article("Foo"), new Article("Bar baz") };
            prefs.General.Summaries.Add("test summary");
            prefs.SkipOptions.SkipWhenNoChanges = true;

            string file = Path.Combine(Path.GetTempPath(), "awb-settings-test-" + System.Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                UserPrefs.SavePrefs(prefs, file);
                Assert.That(File.ReadAllText(file), Does.Contain("<AutoWikiBrowserPreferences"));

                UserPrefs loaded = UserPrefs.LoadPrefs(file);
                Assert.That(loaded.FindAndReplace.Enabled, Is.True);
                Assert.That(loaded.FindAndReplace.IgnoreMoreText, Is.True);
                Assert.That(loaded.FindAndReplace.Replacements.Count, Is.EqualTo(1));
                Assert.That(loaded.FindAndReplace.Replacements[0].Find, Is.EqualTo("foo"));
                Assert.That(loaded.FindAndReplace.Replacements[0].Comment, Is.EqualTo("a comment"));
                Assert.That(loaded.FindAndReplace.Replacements[0].RegularExpressionOptions, Is.EqualTo(RegexOptions.IgnoreCase));
                Assert.That(loaded.FindAndReplace.AdvancedReps.Count, Is.EqualTo(2));
                Assert.That(loaded.FindAndReplace.AdvancedReps[0], Is.InstanceOf<Rule>());
                Assert.That(loaded.FindAndReplace.AdvancedReps[1], Is.InstanceOf<InTemplateRule>());
                Assert.That(loaded.FindAndReplace.AdvancedReps[0].Children.Count, Is.EqualTo(1));
                Assert.That(loaded.List.ArticleList.ConvertAll(a => a.Name), Is.EqualTo(new[] { "Foo", "Bar baz" }));
                Assert.That(loaded.General.Summaries, Does.Contain("test summary"));
                Assert.That(loaded.SkipOptions.SkipWhenNoChanges, Is.True);

                // the loaded rule tree still works: "a" -> "b" by the parent, then "b" -> "c" by its subrule
                var reloaded = new WikiFunctions.ReplaceSpecial.ReplaceSpecial();
                reloaded.AddNewRule(new List<IRule> { loaded.FindAndReplace.AdvancedReps[0] });
                Assert.That(reloaded.ApplyRules("a", "Test"), Is.EqualTo("c"));
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}

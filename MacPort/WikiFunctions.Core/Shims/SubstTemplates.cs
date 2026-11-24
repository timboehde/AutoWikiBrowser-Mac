// Mac version of AWB/WikiFunctions/SubstTemplates.cs: the template list and options without the WinForms dialog.
// RefreshRegexes and SubstituteTemplates are copied unchanged from the original.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WikiFunctions
{
    public class SubstTemplates
    {
        private string[] LocTemplateList = new string[0];
        private readonly Dictionary<Regex, string> Regexes = new Dictionary<Regex, string>();
        private readonly Parse.HideText RemoveUnformatted = new Parse.HideText(true, false, true);

        public string[] TemplateList
        {
            get { return LocTemplateList; }
            set { LocTemplateList = value ?? new string[0]; RefreshRegexes(); }
        }

        public bool ExpandRecursively { get; set; } = true;
        public bool IgnoreUnformatted { get; set; }
        public bool IncludeComments { get; set; }

        public void Clear()
        {
            LocTemplateList = new string[0];
            Regexes.Clear();
        }

        /// <summary>
        /// Generates regexes to match the templates from the template list.
        /// Supports templates with Template: or Msg: at the start
        /// Does not process nested templates
        /// </summary>
        private void RefreshRegexes()
        {
            Regexes.Clear();
            
            // derive optional template namespace prefixes to allow
            string templ = Variables.NamespacesCaseInsensitive[Namespace.Template];
            if (templ[0] == '(')
                templ = "(?:" + templ.Insert(templ.IndexOf(')'), "|[Mm]sg") + @")?\s*";
            else
                templ = @"(?:" + templ + @"|[Mm]sg:|)\s*";

            foreach (string s in TemplateList)
            {
                if (string.IsNullOrEmpty(s.Trim())) 
                    continue;
                
                Regexes.Add(new Regex(@"\{\{\s*" + templ + Tools.FirstLetterCaseInsensitive(Regex.Escape(s)) + @"\s*(\|[^\}]*|)}}",
                    RegexOptions.Singleline), @"{{subst:" + s + "$1}}");
            }
        }

        /// <summary>
        /// Returns the number of templates in the substitution list
        /// </summary>
        public int NoOfRegexes { get { return Regexes.Count; } }

        /// <summary>
        /// Returns whether there are any templates in the substitution list
        /// </summary>
        public bool HasSubstitutions { get { return NoOfRegexes != 0; } }

        /// <summary>
        /// Substitutes templates in the given article text
        /// </summary>
        /// <param name="articleText">The wiki text of the article.</param>
        /// <param name="articleTitle">Title of the article</param>
        /// <returns></returns>
        public string SubstituteTemplates(string articleText, string articleTitle)
        {
            if (!HasSubstitutions) 
                return articleText; // nothing to substitute

            if (IgnoreUnformatted)
                articleText = RemoveUnformatted.HideUnformatted(articleText);

            if (!ExpandRecursively)
            {
                foreach (KeyValuePair<Regex, string> p in Regexes)
                {
                    articleText = p.Key.Replace(articleText, p.Value);
                }
            }
            else
                articleText = Tools.ExpandTemplate(articleText, articleTitle, Regexes, IncludeComments);

            if (IgnoreUnformatted)
                articleText = RemoveUnformatted.AddBackUnformatted(articleText);

            return articleText;
        }
    }
}

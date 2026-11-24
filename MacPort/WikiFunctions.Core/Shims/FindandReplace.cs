// Mac version of AWB/WikiFunctions/Parse/FindandReplace.cs.
// In the original, the rule list lives in a WinForms data grid; here it is a plain list, and the
// editor UI (Avalonia) reads and writes it through GetList/AddNew/Clear and the display helpers below.
// The replacement logic, summaries and the Replacement class are copied unchanged from the original.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WikiFunctions.Parse
{
    public class FindandReplace
    {
        private readonly HideText _remove = new HideText(true, false, true);

        private List<Replacement> _replacementList = new List<Replacement>();
        private List<Replacement> replacementBackup;

        public bool IgnoreLinks { get; set; }
        public bool IgnoreMore { get; set; }
        public bool AppendToSummary { get; set; } = true;

        /// <summary>
        /// Returns proper direction arrow depending on locale
        /// Currently returns only LTR arrow due to direction conflict
        /// demonstrated by https://ar.wikipedia.org/w/index.php?diff=1192871
        /// July 2012: from discusison at https://en.wikipedia.org/wiki/Wikipedia_talk:AutoWikiBrowser/Bugs/Archive_20#The_arrow_in_.22replaced.22_edit_summaries_points_the_wrong_way_in_right-to-left_languages
        /// Arrow derivation reinstated
        /// </summary>
        public static string Arrow
        {
            get
            {
                return Variables.RTL ? " ← " : " → ";
            }
        }

        public void Clear()
        {
            _replacementList.Clear();
        }

        public void AddNew(Replacement r) => _replacementList.Add(r);
        public void AddNew(Replacement r, bool decodeRequired) => _replacementList.Add(r);
        public void AddNew(List<Replacement> rList) => _replacementList.AddRange(rList);
        public List<Replacement> GetList() => _replacementList;

        /// <summary>Backs up the rules before an edit session, so Cancel can restore them</summary>
        public void BeginEdit() => replacementBackup = new List<Replacement>(_replacementList.ConvertAll(r => new Replacement(r)));
        public void CancelEdit() { if (replacementBackup != null) _replacementList = replacementBackup; replacementBackup = null; }

        #region Editor display helpers (from RowToReplacement / AddNew in the original)
        public static string Encode(string text) => text.Replace("\\r\\n", "\r\n");
        public static string Decode(string text) => text.Replace("\n", "\\r\\n");

        /// <summary>The text a user sees in the Find column for this rule</summary>
        public static string DisplayFind(Replacement r) => r.IsRegex ? Decode(r.Find) : Regex.Unescape(Decode(r.Find));
        public static string DisplayReplace(Replacement r) => Decode(r.Replace);

        /// <summary>Builds a rule from what the user typed in the editor (same as RowToReplacement)</summary>
        public static Replacement FromEditor(string find, string replace, bool caseSensitive, bool isRegex, bool multiline,
            bool singleline, bool minor, bool afterProcessing, bool enabled, string comment)
        {
            var rep = new Replacement { Enabled = enabled, Minor = minor, IsRegex = isRegex, BeforeOrAfter = afterProcessing };
            string f = Encode(find ?? ""), r = Encode(replace ?? "");
            if (!rep.IsRegex)
            {
                bool newlines = f.Contains("\\n");
                f = Regex.Escape(f);
                if (newlines) f = f.Replace(@"\\n", "\n");
            }
            rep.Find = f;
            rep.Replace = r;
            if (!caseSensitive) rep.RegularExpressionOptions |= RegexOptions.IgnoreCase;
            if (multiline) rep.RegularExpressionOptions |= RegexOptions.Multiline;
            if (singleline) rep.RegularExpressionOptions |= RegexOptions.Singleline;
            rep.Comment = comment ?? "";
            return rep;
        }
        #endregion

        private static readonly Regex NewlineRegex = new Regex(@"(?<!\\)\\n", RegexOptions.Compiled),
            TabulationRegex = new Regex(@"(?<!\\)\\t", RegexOptions.Compiled);

        private static string PrepareReplacePart(string replace)
        {
            replace = NewlineRegex.Replace(replace, "\n");
            return TabulationRegex.Replace(replace, "\t");
        }

        /// <summary>
        /// Returns the number of replacements (enabled and disabled)
        /// </summary>
        public int NoOfReplacements { get { return _replacementList.Count; } }

        /// <summary>
        /// Returns whether there are any replacements (enabled and disabled)
        /// </summary>
        public bool HasReplacements { get { return NoOfReplacements != 0; } }

        /// <summary>
        /// Returns whether any of the enabled find & replace entries are specified to be run 'after processing'
        /// </summary>
        public bool HasAfterProcessingReplacements
        {
            get
            {
                return HasProcessingReplacements(true);
            }
        }
        
        /// <summary>
        /// Returns whether any of the enabled find & replace entries are specified to be run at before/after as input
        /// </summary>
        public bool HasProcessingReplacements(bool after)
        {
            foreach (Replacement rep in _replacementList)
            {
                if (rep.Enabled && ((after && rep.BeforeOrAfter) || (!after && !rep.BeforeOrAfter)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Applies a series of defined find and replacements to the supplied article text.
        /// </summary>
        /// <param name="articleText">The wiki text of the article.</param>
        /// <param name="editSummary"></param>
        /// <param name="strTitle"></param>
        /// <param name="beforeOrAfter">False if "before", true if "after"</param>
        /// <param name="majorChangesMade"></param>
        /// <returns>The modified article text.</returns>
        public string MultipleFindAndReplace(string articleText, string strTitle, bool beforeOrAfter, ref string editSummary, out bool majorChangesMade)
        {
            majorChangesMade = false;

            if (!HasReplacements)
                return articleText;

            ReplacedSummary = "";
            RemovedSummary = "";

            if (IgnoreMore)
            {
                articleText = _remove.HideMore(articleText);
            }
            else if (IgnoreLinks)
            {
                articleText = _remove.Hide(articleText);
            }

            foreach (Replacement rep in _replacementList)
            {
                if (!rep.Enabled || rep.BeforeOrAfter != beforeOrAfter)
                    continue;

                bool changeMade;
                articleText = PerformFindAndReplace(rep, articleText, strTitle, out changeMade);

                if (changeMade && !rep.Minor)
                {
                    majorChangesMade = true;
                }
            }

            if (IgnoreMore)
            {
                // https://en.wikipedia.org/wiki/Wikipedia_talk:AutoWikiBrowser/Bugs/Archive_24#FormatException_in_HideText.AddBackMore
                // FIXME: Usages of IgnoreMore with number (or M) replacement done in the FindAndReplace can cause corruption
                // e.g. Replacing 2 with "" ⌊⌊⌊⌊M2⌋⌋⌋⌋ becomes ⌊⌊⌊⌊M⌋⌋⌋⌋
                // This cannot then be added back
                articleText = _remove.AddBackMore(articleText);
            }
            else if (IgnoreLinks)
            {
                articleText = _remove.AddBack(articleText);
            }

            if (AppendToSummary)
            {
                if (!string.IsNullOrEmpty(ReplacedSummary))
                    if (Variables.LangCode.Equals("ar"))
                        editSummary = "استبدل: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("arz"))
                        editSummary = "غير: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("be"))
                        editSummary = "перанесена: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("el"))
                        editSummary = "αντικατέστησε: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("eo"))
                        editSummary = "anstataŭigis: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("fa"))
                                                editSummary = "جایگزین شد: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("fr"))
                        editSummary = "remplacement: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("hy"))
                        editSummary = "փոխարինվեց: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("sq"))
                        editSummary = "zëvendësova: " + ReplacedSummary.Trim();
                    else if (Variables.LangCode.Equals("tr"))
                        editSummary = "değiştirildi: " + ReplacedSummary.Trim();
                    else
                        editSummary += "replaced: " + ReplacedSummary.Trim();

                if (!string.IsNullOrEmpty(RemovedSummary))
                {
                    if (!string.IsNullOrEmpty(editSummary))
                    {
                        if (Variables.LangCode.Equals("ar") || Variables.LangCode.Equals("arz") || Variables.LangCode.Equals("fa"))
                        editSummary += "، ";
                        else
                        editSummary += ", ";
                    }

                    if (Variables.LangCode.Equals("ar"))
                        editSummary += "أزال: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("arz"))
                        editSummary += "شال: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("be"))
                        editSummary += "выдалена: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("el"))
                        editSummary += "αφαίρεσε: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("eo"))
                        editSummary += "forigis: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("fa"))
                                                editSummary += "حذف شده: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("fr"))
                        editSummary += "retrait: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("hy"))
                        editSummary += "ջնջվեց: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("sq"))
                        editSummary += "hoqa: " + RemovedSummary.Trim();
                    else if (Variables.LangCode.Equals("tr"))
                        editSummary += "çıkartıldı:" + RemovedSummary.Trim();
                    else
                        editSummary += "removed: " + RemovedSummary.Trim();
                }
            }

            return articleText;
        }

        public string RemovedSummary { get; private set; }
        public string ReplacedSummary { get; private set; }

        /// <summary>
        /// Executes a single find & replace rule against the article text. First applies keywords to the find and replace portions of the rule, then executes the rule.
        /// Edit summary is generated from the first match of the rule that changes the article text on replacement. Count of changes is replacements affecting article, not total matches
        /// i.e. no-change replacements are not counted in the edit summary
        /// </summary>
        /// <param name="rep">F&amp;R rule to execute</param>
        /// <param name="articleText">The article text</param>
        /// <param name="articleTitle">The article title</param>
        /// <param name="changeMade">Whether the F&amp;R rule caused changes to the article text</param>
        /// <returns>The updated article text</returns>
        public string PerformFindAndReplace(Replacement rep, string articleText, string articleTitle, out bool changeMade)
        {
            if (rep == null) throw new ArgumentNullException("rep");

            string findThis = Tools.ApplyKeyWords(articleTitle, rep.Find, true), replaceWith = Tools.ApplyKeyWords(articleTitle, PrepareReplacePart(rep.Replace));

            string comma = @", ";
            if (Variables.LangCode.Equals("ar") || Variables.LangCode.Equals("arz") || Variables.LangCode.Equals("fa"))
                comma = @"، ";

            // T350636 1-minute timeout to guard against regex backtracking
            Regex findRegex = new Regex(findThis, rep.RegularExpressionOptions, TimeSpan.FromSeconds(60));

            int Repcount = 0, Remcount = 0;

            // use first replace that changes article text to generate edit summary
            string res = findRegex.Replace(articleText, m =>  {
                                               string mres = m.Result(replaceWith);

                                               if (!m.Value.Equals(mres))
                                               {
                                                   if (!string.IsNullOrEmpty(mres))
                                                   {
                                                       if (Repcount == 0)
                                                       {
                                                           if (!string.IsNullOrEmpty(ReplacedSummary)) //Add comma before next replaced
                                                               ReplacedSummary += comma;

                                                           ReplacedSummary += m.Value + Arrow + mres;
                                                       }
                                                       Repcount++;
                                                   }
                                                   else
                                                   {
                                                       if (Remcount == 0)
                                                       {
                                                           if (!string.IsNullOrEmpty(RemovedSummary)) //Add comma before next removed
                                                               RemovedSummary += comma;

                                                           RemovedSummary += m.Value;
                                                       }
                                                       Remcount++;
                                                   }
                                               }
                                               
                                               return mres;
                                           } );

            // update summaries with count of changes
            if (Repcount > 1)
                ReplacedSummary += " (" + Repcount + ")";

            if (Remcount > 1)
                RemovedSummary += " (" + Remcount + ")";

            changeMade = (Repcount + Remcount > 0);

            return res;
        }

    }

    /// <summary>
    /// 
    /// </summary>
    public class Replacement
    {
        public Replacement()
        {
            RegularExpressionOptions = RegexOptions.None;
        }

        public Replacement(string find, string replace, bool isRegex, bool enabled, bool minor, bool beforeOrAfter,
                           RegexOptions regularExpressionOptions, string comment)
        {
            Find = find;
            Replace = replace;
            IsRegex = isRegex;
            Enabled = enabled;
            Minor = minor;
            BeforeOrAfter = beforeOrAfter;
            RegularExpressionOptions = regularExpressionOptions;
            Comment = comment;
        }

        public Replacement(Replacement repl)
            : this(
                repl.Find, repl.Replace, repl.IsRegex, repl.Enabled, repl.Minor,
                repl.BeforeOrAfter, repl.RegularExpressionOptions, repl.Comment)
        {
        }

        public string Find,
            Replace, Comment;

        public bool IsRegex,
            Enabled, Minor, BeforeOrAfter;

        public RegexOptions RegularExpressionOptions;
    }
}


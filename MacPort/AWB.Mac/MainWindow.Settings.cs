// Settings files: the same XML format as Windows AWB (WikiFunctions.UserPrefs), following
// AWB/AWB/UserSettings.cs (MakePrefs / LoadPrefs). The last loaded UserPrefs object is kept and
// updated in place, so settings the Mac has no controls for yet survive a load/save round trip.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using WikiFunctions;
using WikiFunctions.AWBSettings;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace AutoWikiBrowser.Mac
{
    public partial class MainWindow
    {
        private UserPrefs prefs = new UserPrefs();
        private string settingsFile = "";
        private readonly MacSettings macSettings = MacSettings.Load();
        private readonly ObservableCollection<string> summaries = new ObservableCollection<string>();
        private NativeMenu recentMenu;

        private static readonly string[] DefaultSummaries =
        {
            "clean up",
            "re-categorisation per [[WP:CFD|CFD]]",
            "clean up and re-categorisation per [[WP:CFD|CFD]]",
            "removing category per [[WP:CFD|CFD]]",
            "[[Wikipedia:Template substitution|subst:'ing]]",
            "[[Wikipedia:WikiProject Stub sorting|stub sorting]]",
            "[[WP:AWB/T|Typo fixing]]",
            "bad link repair",
            "Fixing [[Wikipedia:Disambiguation pages with links|links to disambiguation pages]]",
            "Unicodifying"
        };

        #region Menu
        private void BuildMenu()
        {
            NativeMenuItem Item(string header, KeyGesture gesture, Action action)
            {
                var item = new NativeMenuItem(header);
                if (gesture != null) item.Gesture = gesture;
                item.Click += (s, e) => action();
                return item;
            }
            const KeyModifiers Cmd = KeyModifiers.Meta;

            var file = new NativeMenu();
            file.Add(Item("Open Settings…", new KeyGesture(Key.O, Cmd), async () => await OpenSettingsAsync()));
            recentMenu = new NativeMenu();
            file.Add(new NativeMenuItem("Open Recent") { Menu = recentMenu });
            file.Add(new NativeMenuItemSeparator());
            file.Add(Item("Save Settings", new KeyGesture(Key.S, Cmd | KeyModifiers.Shift), async () => await SaveSettingsAsync(false)));
            file.Add(Item("Save Settings As…", new KeyGesture(Key.S, Cmd | KeyModifiers.Shift | KeyModifiers.Alt), async () => await SaveSettingsAsync(true)));
            file.Add(Item("Save as Default Settings", null, () => { SaveSettingsFile(AwbDirs.DefaultSettings, false); StatusText = "Saved as default settings"; }));
            file.Add(new NativeMenuItemSeparator());
            file.Add(Item("Reset to Default Settings", null, async () => await ApplyPrefsAsync(new UserPrefs(), "")));
            file.Add(new NativeMenuItemSeparator());
            file.Add(Item("Log In…", new KeyGesture(Key.L, Cmd | KeyModifiers.Shift), async () => await LoginAsync()));

            var page = new NativeMenu();
            page.Add(Item("Start", new KeyGesture(Key.Enter, Cmd), StartProcessing));
            page.Add(Item("Stop", new KeyGesture(Key.OemPeriod, Cmd), StopProcessing));
            page.Add(new NativeMenuItemSeparator());
            page.Add(Item("Save Page", new KeyGesture(Key.S, Cmd), SaveCurrent));
            page.Add(Item("Skip Page", new KeyGesture(Key.I, Cmd), () => SkipCurrent("user")));
            page.Add(new NativeMenuItemSeparator());
            page.Add(Item("Show Changes", new KeyGesture(Key.D, Cmd), ShowDiff));
            page.Add(Item("Preview", new KeyGesture(Key.P, Cmd | KeyModifiers.Shift), ShowPreview));
            page.Add(Item("Edit Text", new KeyGesture(Key.E, Cmd), () => { if (pageReady) { editorTabs.SelectedIndex = 1; editBox.Focus(); } }));
            page.Add(Item("Open in Browser", new KeyGesture(Key.B, Cmd | KeyModifiers.Shift), OpenCurrentInBrowser));

            var tools = new NativeMenu();
            tools.Add(Item("Find and Replace Rules…", new KeyGesture(Key.F, Cmd | KeyModifiers.Shift), async () => await EditFindReplaceAsync()));
            tools.Add(Item("Advanced Find and Replace (Replace Special)…", null, async () => await EditReplaceSpecialAsync()));

            var root = new NativeMenu();
            root.Add(new NativeMenuItem("File") { Menu = file });
            root.Add(new NativeMenuItem("Page") { Menu = page });
            root.Add(new NativeMenuItem("Tools") { Menu = tools });
            NativeMenu.SetMenu(this, root);
            RefreshRecentMenu();
        }

        private void RefreshRecentMenu()
        {
            if (recentMenu == null) return;
            recentMenu.Items.Clear();
            foreach (string path in macSettings.RecentSettingsFiles.Where(File.Exists))
            {
                var item = new NativeMenuItem(Path.GetFileName(path));
                string p = path;
                item.Click += async (s, e) => await LoadSettingsFileAsync(p);
                recentMenu.Add(item);
            }
            if (recentMenu.Items.Count == 0) recentMenu.Add(new NativeMenuItem("No recent settings") { IsEnabled = false });
        }
        #endregion

        #region Open / save
        private static readonly FilePickerFileType SettingsFileType =
            new FilePickerFileType("AWB settings") { Patterns = new[] { "*.xml" } };

        private async Task OpenSettingsAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open AWB settings",
                FileTypeFilter = new[] { SettingsFileType, FilePickerFileTypes.All }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path)) await LoadSettingsFileAsync(path);
        }

        private async Task LoadSettingsFileAsync(string path)
        {
            UserPrefs loaded = null;
            if (!await RunBackground("Loading settings…", () => loaded = UserPrefs.LoadPrefs(path)) || loaded == null)
                return;
            bool isDefault = path == AwbDirs.DefaultSettings;
            await ApplyPrefsAsync(loaded, isDefault ? "" : path);
            if (!isDefault) macSettings.AddRecent(path);
            RefreshRecentMenu();
            StatusText = "Settings loaded" + (isDefault ? "" : " from " + Path.GetFileName(path));
        }

        private async Task SaveSettingsAsync(bool askForFile)
        {
            string path = settingsFile;
            if (askForFile || string.IsNullOrEmpty(path))
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save AWB settings",
                    SuggestedFileName = string.IsNullOrEmpty(path) ? "AWB settings.xml" : Path.GetFileName(path),
                    DefaultExtension = "xml",
                    FileTypeChoices = new[] { SettingsFileType }
                });
                path = file?.TryGetLocalPath();
                if (string.IsNullOrEmpty(path)) return;
            }
            if (SaveSettingsFile(path, true))
            {
                StatusText = "Settings saved to " + Path.GetFileName(path);
                RefreshRecentMenu();
            }
        }

        /// <summary>Writes the current settings; true on success. Named files become the current settings file.</summary>
        private bool SaveSettingsFile(string path, bool makeCurrent)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                UserPrefs.SavePrefs(MakePrefs(), path);
                if (makeCurrent && path != AwbDirs.DefaultSettings)
                {
                    settingsFile = path;
                    macSettings.AddRecent(path);
                    UpdateTitle();
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex);
                return false;
            }
        }

        private void SaveDefaultSettingsOnExit()
        {
            try { UserPrefs.SavePrefs(MakePrefs(), AwbDirs.DefaultSettings); } catch { }
            macSettings.LastUser = lastUser;
            macSettings.Save();
        }

        private void UpdateTitle() =>
            Title = string.IsNullOrEmpty(settingsFile) ? "AutoWikiBrowser" : "AutoWikiBrowser – " + Path.GetFileName(settingsFile);
        #endregion

        #region Mapping (UserSettings.cs)
        /// <summary>UserSettings.MakePrefs: current UI into the kept UserPrefs</summary>
        private UserPrefs MakePrefs()
        {
            var p = prefs ?? new UserPrefs();
            p.LanguageCode = Variables.LangCode;
            p.Project = Variables.Project;
            p.CustomProject = Variables.CustomProject ?? "";
            p.Protocol = Variables.Protocol;
            p.LoginDomain = Variables.LoginDomain ?? "";

            p.FindAndReplace = new FaRPrefs(findAndReplace, replaceSpecial, substTemplates) { Enabled = farCheck.IsChecked == true };

            p.List.ListSource = sourceInput.Text ?? "";
            if (sourceBox.SelectedItem is ListSource src) p.List.SelectedProvider = src.ProviderName;
            p.List.ArticleList = p.General.SaveArticleList ? pages.Select(t => new Article(t)).ToList() : new List<Article>();

            p.Editprefs.GeneralFixes = genFixesCheck.IsChecked == true;
            p.Editprefs.Tagger = autoTagCheck.IsChecked == true;
            p.Editprefs.RegexTypoFix = typosCheck.IsChecked == true;
            p.Editprefs.Unicodify = unicodifyCheck.IsChecked == true;
            p.Editprefs.Recategorisation = Math.Max(0, catModeBox.SelectedIndex);
            p.Editprefs.NewCategory = catBox1.Text ?? "";
            p.Editprefs.NewCategory2 = catBox2.Text ?? "";
            p.Editprefs.SkipIfNoCatChange = skipNoCatCheck.IsChecked == true;
            p.Editprefs.RemoveSortKey = removeSortKeyCheck.IsChecked == true;
            p.Editprefs.ReImage = Math.Max(0, fileModeBox.SelectedIndex);
            p.Editprefs.ImageFind = fileBox1.Text ?? "";
            p.Editprefs.Replace = fileBox2.Text ?? "";
            p.Editprefs.SkipIfNoImgChange = skipNoFileCheck.IsChecked == true;
            p.Editprefs.AppendText = appendCheck.IsChecked == true;
            p.Editprefs.Append = appendRadio.IsChecked == true;
            p.Editprefs.Text = appendTextBox.Text ?? "";
            p.Editprefs.Newlines = (int)(newlinesBox.Value ?? 2);
            p.Editprefs.AppendTextMetaDataSort = appendSortCheck.IsChecked == true;

            var s = p.SkipOptions;
            if (skipNonExistentCheck.IsChecked == true) { s.SkipNonexistent = true; s.Skipexistent = false; s.SkipDontCare = false; }
            else if (s.SkipNonexistent) { s.SkipNonexistent = false; s.Skipexistent = false; s.SkipDontCare = true; }
            s.SkipWhenNoChanges = skipNoChangesCheck.IsChecked == true;
            s.SkipWhenOnlyWhitespaceChanged = skipWhitespaceCheck.IsChecked == true;
            s.SkipOnlyCasingChanged = skipCasingCheck.IsChecked == true;
            s.SkipOnlyGeneralFixChanges = skipGenFixesCheck.IsChecked == true;
            s.SkipOnlyMinorGeneralFixChanges = skipMinorGenFixesCheck.IsChecked == true;
            s.SkipNoFindAndReplace = skipNoFarCheck.IsChecked == true;
            s.SkipNoRegexTypoFix = skipNoTypoCheck.IsChecked == true;
            s.SkipIfRedirect = skipRedirectCheck.IsChecked == true;
            s.SkipInuse = skipInUseCheck.IsChecked == true;

            p.General.Summaries = summaries.ToList();
            p.General.SelectedSummary = summaryBox.Text ?? "";
            p.General.Minor = minorCheck.IsChecked == true;
            p.General.AddToWatchlist = Math.Max(0, watchBox.SelectedIndex);
            return p;
        }

        /// <summary>UserSettings.LoadPrefs(UserPrefs): settings into the UI</summary>
        private async Task ApplyPrefsAsync(UserPrefs p, string fromFile)
        {
            prefs = p;
            settingsFile = fromFile ?? "";
            UpdateTitle();

            typosCheck.IsChecked = false;
            await SwitchSiteAsync(p.LanguageCode, p.Project, p.CustomProject ?? "", string.IsNullOrEmpty(p.Protocol) ? "https://" : p.Protocol);
            Variables.LoginDomain = p.LoginDomain;

            findAndReplace.Clear();
            findAndReplace.IgnoreLinks = p.FindAndReplace.IgnoreSomeText;
            findAndReplace.IgnoreMore = p.FindAndReplace.IgnoreMoreText;
            findAndReplace.AppendToSummary = p.FindAndReplace.AppendSummary;
            findAndReplace.AddNew(p.FindAndReplace.Replacements);
            farCheck.IsChecked = p.FindAndReplace.Enabled;

            replaceSpecial.Clear();
            replaceSpecial.AddNewRule(p.FindAndReplace.AdvancedReps);

            substTemplates.Clear();
            substTemplates.TemplateList = p.FindAndReplace.SubstTemplates;
            substTemplates.ExpandRecursively = p.FindAndReplace.ExpandRecursively;
            substTemplates.IgnoreUnformatted = p.FindAndReplace.IgnoreUnformatted;
            substTemplates.IncludeComments = p.FindAndReplace.IncludeComments;

            sourceInput.Text = p.List.ListSource ?? "";
            var source = Sources.FirstOrDefault(x => x.ProviderName == p.List.SelectedProvider);
            if (source != null) sourceBox.SelectedItem = source;
            if (p.List.ArticleList != null)
            {
                var existing = new HashSet<string>(pages);
                foreach (Article a in p.List.ArticleList)
                    if (a != null && !string.IsNullOrEmpty(a.Name) && existing.Add(a.Name)) pages.Add(a.Name);
            }

            genFixesCheck.IsChecked = p.Editprefs.GeneralFixes;
            autoTagCheck.IsChecked = p.Editprefs.Tagger && Variables.IsWikipediaEN;
            unicodifyCheck.IsChecked = p.Editprefs.Unicodify;
            catModeBox.SelectedIndex = Math.Max(0, Math.Min(3, p.Editprefs.Recategorisation));
            catBox1.Text = p.Editprefs.NewCategory ?? "";
            catBox2.Text = p.Editprefs.NewCategory2 ?? "";
            skipNoCatCheck.IsChecked = p.Editprefs.SkipIfNoCatChange;
            removeSortKeyCheck.IsChecked = p.Editprefs.RemoveSortKey;
            fileModeBox.SelectedIndex = Math.Max(0, Math.Min(3, p.Editprefs.ReImage));
            fileBox1.Text = p.Editprefs.ImageFind ?? "";
            fileBox2.Text = p.Editprefs.Replace ?? "";
            skipNoFileCheck.IsChecked = p.Editprefs.SkipIfNoImgChange;
            appendCheck.IsChecked = p.Editprefs.AppendText;
            appendRadio.IsChecked = p.Editprefs.Append;
            C2<RadioButton>("PrependRadio").IsChecked = !p.Editprefs.Append;
            appendTextBox.Text = p.Editprefs.Text ?? "";
            newlinesBox.Value = Math.Max(0, Math.Min(10, p.Editprefs.Newlines));
            appendSortCheck.IsChecked = p.Editprefs.AppendTextMetaDataSort;

            var s = p.SkipOptions;
            skipNonExistentCheck.IsChecked = s.SkipNonexistent;
            skipNoChangesCheck.IsChecked = s.SkipWhenNoChanges;
            skipWhitespaceCheck.IsChecked = s.SkipWhenOnlyWhitespaceChanged;
            skipCasingCheck.IsChecked = s.SkipOnlyCasingChanged;
            skipGenFixesCheck.IsChecked = s.SkipOnlyGeneralFixChanges;
            skipMinorGenFixesCheck.IsChecked = s.SkipOnlyMinorGeneralFixChanges;
            skipNoFarCheck.IsChecked = s.SkipNoFindAndReplace;
            skipNoTypoCheck.IsChecked = s.SkipNoRegexTypoFix;
            skipRedirectCheck.IsChecked = s.SkipIfRedirect;
            skipInUseCheck.IsChecked = s.SkipInuse;

            summaries.Clear();
            foreach (string x in p.General.Summaries.Count > 0 ? (IEnumerable<string>)p.General.Summaries : DefaultSummaries)
                summaries.Add(x);
            summaryBox.Text = p.General.SelectedSummary ?? "";
            minorCheck.IsChecked = p.General.Minor;
            watchBox.SelectedIndex = Math.Max(0, Math.Min(3, p.General.AddToWatchlist));

            typosCheck.IsChecked = p.Editprefs.RegexTypoFix;
        }

        private T C2<T>(string name) where T : Control => this.FindControl<T>(name);

        private void RememberSummary()
        {
            string s = (summaryBox.Text ?? "").Trim();
            if (s.Length > 0 && !summaries.Contains(s)) summaries.Add(s);
        }
        #endregion
    }
}

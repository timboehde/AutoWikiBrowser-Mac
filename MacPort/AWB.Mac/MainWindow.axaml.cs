// Main window. The processing flow follows AWB/AWB/Main.cs (Start → OpenPage → PageLoaded →
// ProcessPage → RunSkipChecks → CompleteProcessPage → Save/Skip → next page), using the same
// engine calls in the same order, minus features that aren't ported yet (plugins, custom modules,
// disambiguation, categorisation, append/prepend text, file replacement, bot mode).
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AutoWikiBrowser.Mac.Dialogs;
using WikiFunctions;
using WikiFunctions.API;
using WikiFunctions.AWBSettings;
using WikiFunctions.Lists.Providers;
using WikiFunctions.Mac;
using WikiFunctions.Options;
using WikiFunctions.Parse;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace AutoWikiBrowser.Mac
{
    public partial class MainWindow : Window
    {
        #region Controls
        private readonly TextBox langBox, sourceInput, finalSummaryBox, editBox;
        private readonly AutoCompleteBox summaryBox, wikiAddressBox;
        private readonly TextBlock fandomSuffix;
        private readonly ComboBox projectBox, sourceBox, watchBox, catModeBox, fileModeBox;
        private readonly TextBox catBox1, catBox2, fileBox1, fileBox2, appendTextBox;
        private readonly CheckBox unicodifyCheck, skipNoCatCheck, removeSortKeyCheck, skipNoFileCheck, appendCheck, appendSortCheck;
        private readonly RadioButton appendRadio;
        private readonly NumericUpDown newlinesBox;
        private readonly Button advancedRulesButton, previewButton;
        private readonly ListBox pageList, logList;
        private readonly TextBlock statusLabel, userLabel, siteLabel, countsLabel, listCountLabel, pageTitleLabel;
        private readonly ProgressBar busyBar;
        private readonly TabControl editorTabs;
        private readonly DiffView diff;
        private readonly CheckBox genFixesCheck, autoTagCheck, typosCheck, farCheck, minorCheck,
            skipNoChangesCheck, skipWhitespaceCheck, skipCasingCheck, skipGenFixesCheck, skipMinorGenFixesCheck,
            skipNoFarCheck, skipNoTypoCheck, skipNonExistentCheck, skipRedirectCheck, skipInUseCheck;
        private readonly Button loginButton, loadSiteButton, makeListButton, addPageButton, removeButton, clearButton,
            saveListButton, farRulesButton, startButton, stopButton, saveButton, skipButton, diffButton, editTabButton,
            openPageButton;
        #endregion

        #region Engine state
        private Session session;
        private Parsers parser;
        private readonly HideText removeText = new HideText(false, true, true);
        private readonly MacSkipOptions skipOptions = new MacSkipOptions();
        private readonly FindandReplace findAndReplace = new FindandReplace();
        private readonly SubstTemplates substTemplates = new SubstTemplates();
        private readonly WikiFunctions.ReplaceSpecial.ReplaceSpecial replaceSpecial = new WikiFunctions.ReplaceSpecial.ReplaceSpecial();
        private RegExTypoFix typos;
        private bool templateRedirectsLoaded, datedTemplatesLoaded, renamedParametersLoaded;
        public bool TyposNeedReload { get; set; } = true;

        private Article current;
        private string originalLf = "";
        private bool running, pageReady, showingDiff;
        private int skipped, retries, busyCount;
        private string lastUser = "";
        public int NumberOfEdits { get; private set; }

        private readonly ObservableCollection<string> pages = new ObservableCollection<string>();
        private readonly ObservableCollection<string> log = new ObservableCollection<string>();
        #endregion

        private sealed class ListSource
        {
            public ListSource(string name, Func<IListProvider> make, string hint, bool needsInput = true, bool needsFile = false)
            { Name = name; Make = make; Hint = hint; NeedsInput = needsInput; NeedsFile = needsFile; }
            public string Name { get; }
            public Func<IListProvider> Make { get; }
            public string Hint { get; }
            public bool NeedsInput { get; }
            public bool NeedsFile { get; }
            private string providerName;
            /// <summary>Provider class name, as stored in settings files (ListPrefs.SelectedProvider)</summary>
            public string ProviderName => providerName ?? (providerName = Make().GetType().Name);
            public override string ToString() => Name;
        }

        private static readonly ListSource[] Sources =
        {
            new ListSource("Category", () => new CategoryListProvider(), "Category name, e.g. Rivers of Iowa"),
            new ListSource("Category (recursive)", () => new CategoryRecursiveListProvider(), "Category name (includes subcategories)"),
            new ListSource("What links here", () => new WhatLinksHereListProvider(), "Page title"),
            new ListSource("What transcludes page", () => new WhatTranscludesPageListProvider(), "Template, e.g. Template:Infobox river"),
            new ListSource("Links on page", () => new LinksOnPageListProvider(), "Page title"),
            new ListSource("Wiki search (text)", () => new WikiSearchListProvider(), "Search text"),
            new ListSource("User contributions", () => new UserContribsListProvider(), "Username"),
            new ListSource("My watchlist", () => new MyWatchlistListProvider(), "", needsInput: false),
            new ListSource("Text file…", () => new TextFileListProviderUFT8(), "", needsInput: false, needsFile: true),
        };

        private static readonly ProjectEnum[] Projects =
        {
            ProjectEnum.wikipedia, ProjectEnum.wiktionary, ProjectEnum.wikisource, ProjectEnum.wikiquote,
            ProjectEnum.wikibooks, ProjectEnum.wikinews, ProjectEnum.wikiversity, ProjectEnum.wikivoyage,
            ProjectEnum.species, ProjectEnum.commons, ProjectEnum.meta, ProjectEnum.mediawiki, ProjectEnum.wikidata,
            ProjectEnum.fandom, ProjectEnum.custom
        };

        public MainWindow()
        {
            AvaloniaXamlLoader.Load(this);

            T C<T>(string name) where T : Control => this.FindControl<T>(name);
            langBox = C<TextBox>("LangBox"); sourceInput = C<TextBox>("SourceInput"); summaryBox = C<AutoCompleteBox>("SummaryBox"); wikiAddressBox = C<AutoCompleteBox>("WikiAddressBox");
            fandomSuffix = C<TextBlock>("FandomSuffix");
            finalSummaryBox = C<TextBox>("FinalSummaryBox"); editBox = C<TextBox>("EditBox");
            projectBox = C<ComboBox>("ProjectBox"); sourceBox = C<ComboBox>("SourceBox"); watchBox = C<ComboBox>("WatchBox");
            pageList = C<ListBox>("PageList"); logList = C<ListBox>("LogList");
            statusLabel = C<TextBlock>("StatusLabel"); userLabel = C<TextBlock>("UserLabel"); siteLabel = C<TextBlock>("SiteLabel");
            countsLabel = C<TextBlock>("CountsLabel"); listCountLabel = C<TextBlock>("ListCountLabel"); pageTitleLabel = C<TextBlock>("PageTitleLabel");
            busyBar = C<ProgressBar>("BusyBar"); editorTabs = C<TabControl>("EditorTabs"); diff = C<DiffView>("Diff");
            genFixesCheck = C<CheckBox>("GenFixesCheck"); autoTagCheck = C<CheckBox>("AutoTagCheck"); typosCheck = C<CheckBox>("TyposCheck");
            farCheck = C<CheckBox>("FarCheck"); minorCheck = C<CheckBox>("MinorCheck");
            skipNoChangesCheck = C<CheckBox>("SkipNoChangesCheck"); skipWhitespaceCheck = C<CheckBox>("SkipWhitespaceCheck");
            skipCasingCheck = C<CheckBox>("SkipCasingCheck"); skipGenFixesCheck = C<CheckBox>("SkipGenFixesCheck");
            skipMinorGenFixesCheck = C<CheckBox>("SkipMinorGenFixesCheck"); skipNoFarCheck = C<CheckBox>("SkipNoFarCheck");
            skipNoTypoCheck = C<CheckBox>("SkipNoTypoCheck"); skipNonExistentCheck = C<CheckBox>("SkipNonExistentCheck");
            skipRedirectCheck = C<CheckBox>("SkipRedirectCheck"); skipInUseCheck = C<CheckBox>("SkipInUseCheck");
            loginButton = C<Button>("LoginButton"); loadSiteButton = C<Button>("LoadSiteButton"); makeListButton = C<Button>("MakeListButton");
            addPageButton = C<Button>("AddPageButton"); removeButton = C<Button>("RemoveButton"); clearButton = C<Button>("ClearButton");
            saveListButton = C<Button>("SaveListButton"); farRulesButton = C<Button>("FarRulesButton"); startButton = C<Button>("StartButton");
            stopButton = C<Button>("StopButton"); saveButton = C<Button>("SaveButton"); skipButton = C<Button>("SkipButton");
            catModeBox = C<ComboBox>("CatModeBox"); fileModeBox = C<ComboBox>("FileModeBox");
            catBox1 = C<TextBox>("CatBox1"); catBox2 = C<TextBox>("CatBox2"); fileBox1 = C<TextBox>("FileBox1"); fileBox2 = C<TextBox>("FileBox2");
            appendTextBox = C<TextBox>("AppendTextBox"); unicodifyCheck = C<CheckBox>("UnicodifyCheck");
            skipNoCatCheck = C<CheckBox>("SkipNoCatCheck"); removeSortKeyCheck = C<CheckBox>("RemoveSortKeyCheck");
            skipNoFileCheck = C<CheckBox>("SkipNoFileCheck"); appendCheck = C<CheckBox>("AppendCheck"); appendSortCheck = C<CheckBox>("AppendSortCheck");
            appendRadio = C<RadioButton>("AppendRadio"); newlinesBox = C<NumericUpDown>("NewlinesBox");
            advancedRulesButton = C<Button>("AdvancedRulesButton"); previewButton = C<Button>("PreviewButton");
            diffButton = C<Button>("DiffButton"); editTabButton = C<Button>("EditTabButton"); openPageButton = C<Button>("OpenPageButton");

            projectBox.ItemsSource = Projects;
            projectBox.SelectedIndex = 0;
            sourceBox.ItemsSource = Sources;
            sourceBox.SelectedIndex = 0;
            watchBox.ItemsSource = new[] { "Add pages I edit", "Remove pages I edit", "Don't change watchlist", "Use my preferences" };
            watchBox.SelectedIndex = 3;
            // same items, in the same order, as Windows AWB (the index is what settings files store)
            catModeBox.ItemsSource = new[] { "Choose a task…", "Replace category:", "Add category:", "Remove category:" };
            catModeBox.SelectedIndex = 0;
            fileModeBox.ItemsSource = new[] { "Choose a task…", "Replace file:", "Remove file:", "Comment out file:" };
            fileModeBox.SelectedIndex = 0;
            catModeBox.SelectionChanged += (s, e) => UpdateMoreFields();
            fileModeBox.SelectionChanged += (s, e) => UpdateMoreFields();
            UpdateMoreFields();
            pageList.ItemsSource = pages;
            logList.ItemsSource = log;
            summaryBox.ItemsSource = summaries;
            wikiAddressBox.ItemsSource = macSettings.CustomWikis;
            projectBox.SelectionChanged += (s, e) => UpdateSiteFields();
            lastUser = macSettings.LastUser ?? "";
            pages.CollectionChanged += (s, e) => listCountLabel.Text = "Pages: " + pages.Count;
            diff.ShowMessage("No page loaded", "Make a list, enter an edit summary, then press Start.");

            WireEvents();
            BuildMenu();
            UpdateButtons();
            Opened += async (s, e) => await InitAsync();
            Closing += (s, e) => SaveDefaultSettingsOnExit();
        }

        #region Status helpers
        public string StatusText
        {
            get => statusLabel.Text;
            set => statusLabel.Text = value;
        }

        public bool IsBusy
        {
            get => busyCount > 0;
            set
            {
                busyCount = Math.Max(0, busyCount + (value ? 1 : -1));
                busyBar.IsVisible = busyCount > 0;
            }
        }

        private void UpdateCounts() => countsLabel.Text = "Edits: " + NumberOfEdits + "  Skipped: " + skipped;

        private void UpdateButtons()
        {
            bool registered = session != null && session.Status == WikiStatusResult.Registered;
            startButton.IsEnabled = registered && !running;
            stopButton.IsEnabled = running;
            saveButton.IsEnabled = skipButton.IsEnabled = pageReady;
            diffButton.IsEnabled = editTabButton.IsEnabled = finalSummaryBox.IsEnabled = previewButton.IsEnabled = pageReady;
            openPageButton.IsEnabled = current != null;
            makeListButton.IsEnabled = session != null;
        }

        private void AddLog(string line)
        {
            log.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + line);
        }

        private Task<DialogResult> Message(string text, string caption = "AutoWikiBrowser",
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
            => MessageDialog.ShowAsync(this, text, caption, buttons, icon);

        /// <summary>Runs engine work off the UI thread, with the busy indicator. False if it threw.</summary>
        private async Task<bool> RunBackground(string status, Action work)
        {
            IsBusy = true;
            if (status != null) StatusText = status;
            try
            {
                await Task.Run(work);
                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action action;
            public RelayCommand(Action action) { this.action = action; }
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => action();
        }
        #endregion

        #region Startup, site and login
        private void WireEvents()
        {
            loginButton.Click += async (s, e) => await LoginAsync();
            loadSiteButton.Click += async (s, e) => await SwitchSiteFromBarAsync();
            sourceBox.SelectionChanged += (s, e) =>
            {
                if (sourceBox.SelectedItem is ListSource src)
                {
                    sourceInput.Watermark = src.Hint;
                    sourceInput.IsEnabled = src.NeedsInput;
                }
            };
            makeListButton.Click += async (s, e) => await MakeListAsync();
            sourceInput.KeyDown += async (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; await MakeListAsync(); } };
            addPageButton.Click += (s, e) => AddPage((sourceInput.Text ?? "").Trim());
            removeButton.Click += (s, e) =>
            {
                foreach (var item in pageList.SelectedItems.Cast<string>().ToList()) pages.Remove(item);
            };
            clearButton.Click += (s, e) => pages.Clear();
            saveListButton.Click += async (s, e) => await SaveListAsync();
            farRulesButton.Click += async (s, e) => await EditFindReplaceAsync();
            advancedRulesButton.Click += async (s, e) => await EditReplaceSpecialAsync();
            previewButton.Click += (s, e) => ShowPreview();
            typosCheck.IsCheckedChanged += async (s, e) =>
            {
                if (typosCheck.IsChecked == true && (typos == null || TyposNeedReload) && session?.Status == WikiStatusResult.Registered)
                    await LoadTyposAsync();
            };

            startButton.Click += (s, e) => StartProcessing();
            stopButton.Click += (s, e) => StopProcessing();
            saveButton.Click += (s, e) => SaveCurrent();
            skipButton.Click += (s, e) => SkipCurrent("user");
            diffButton.Click += (s, e) => ShowDiff();
            editTabButton.Click += (s, e) => { editorTabs.SelectedIndex = 1; editBox.Focus(); };
            openPageButton.Click += (s, e) => OpenCurrentInBrowser();
            editorTabs.SelectionChanged += (s, e) =>
            {
                if (ReferenceEquals(e.Source, editorTabs) && editorTabs.SelectedIndex == 0 && !showingDiff) ShowDiff();
            };
            diff.LineClicked += GoToLine;
            // keyboard shortcuts come from the menu bar (MainWindow.Settings.cs BuildMenu)
        }

        private async Task EditFindReplaceAsync()
        {
            var window = new FindReplaceWindow(findAndReplace);
            await window.ShowDialog<bool>(this);
            if (findAndReplace.HasReplacements) farCheck.IsChecked = true;
        }

        private async Task EditReplaceSpecialAsync()
        {
            var window = new ReplaceSpecialWindow(replaceSpecial);
            if (await window.ShowDialog<bool>(this) && replaceSpecial.HasRules) farCheck.IsChecked = true;
        }

        private void UpdateMoreFields()
        {
            int cat = catModeBox.SelectedIndex, file = fileModeBox.SelectedIndex;
            catBox1.IsEnabled = skipNoCatCheck.IsEnabled = cat > 0;
            catBox2.IsVisible = cat == 1;
            removeSortKeyCheck.IsEnabled = cat > 0;
            fileBox1.IsEnabled = skipNoFileCheck.IsEnabled = file > 0;
            fileBox2.IsEnabled = file > 0;
            fileBox2.Watermark = file == 1 ? "Replace with this file" : file == 3 ? "Comment text (optional)" : "";
        }

        private void OpenCurrentInBrowser()
        {
            if (current != null)
                MacSystem.OpenUrl(Variables.URLIndex + "?title=" + Uri.EscapeDataString(current.Name.Replace(' ', '_')));
        }

        private async Task InitAsync()
        {
            AvaloniaSynchronizationContext.InstallIfNeeded();
            UiHost.Current = new AvaloniaUiHost(this);
            System.Windows.Forms.Application.ProductName = "AutoWikiBrowser (Mac)";

            // Created on the UI thread, so engine callbacks are marshalled back here
            session = new Session(new System.Windows.Forms.Control());
            Variables.MainForm = new AwbHost(this, session);

            session.OpenComplete += (sender, page) => PageLoaded(page);
            session.SaveComplete += (sender, info) => PageSaved(info);
            session.ExceptionCaught += (sender, ex) => EditorException(ex);
            session.MaxlagExceeded += (sender, maxlag, retryAfter) => Maxlag(retryAfter);
            session.LoggedOff += sender => { userLabel.Text = "Logged off"; StopProcessing(); };
            session.PreviewComplete += (sender, html) => PreviewReady(sender, html);

            await RunBackground("Starting…", () => parser = new Parsers(500, false));

            // like Windows AWB: start from the default settings file if there is one
            if (File.Exists(AwbDirs.DefaultSettings)) await LoadSettingsFileAsync(AwbDirs.DefaultSettings);
            else await ApplyPrefsAsync(new UserPrefs(), "");
            await AutoLoginAsync();
        }

        private async Task SwitchSiteAsync(string lang, ProjectEnum project, string customProject = "", string protocol = "https://")
        {
            StopProcessing();
            string name = project == ProjectEnum.custom ? customProject : lang + "." + project;
            bool ok = await RunBackground("Loading " + name + "…",
                () => Variables.SetProject(lang, project, customProject ?? "", protocol));

            if (parser != null) parser.InterWikiOrder = InterWikiOrderFor(Variables.LangCode, project);
            templateRedirectsLoaded = datedTemplatesLoaded = renamedParametersLoaded = false;
            typos = null;
            TyposNeedReload = true;
            if (!Variables.IsWikipediaEN) autoTagCheck.IsChecked = false;

            langBox.Text = Variables.LangCode;
            projectBox.SelectedItem = Variables.Project;
            if (Variables.Project == ProjectEnum.custom) wikiAddressBox.Text = Variables.Protocol + Variables.CustomProject;
            else if (Variables.Project == ProjectEnum.fandom) wikiAddressBox.Text = Variables.CustomProject;
            UpdateSiteFields();
            siteLabel.Text = Variables.URL;
            if (ok && Variables.TryLoadingAgainAfterLogin)
            {
                await Message("This seems to be a private wiki. Log in, and the site will finish loading after that.", "Restricted wiki");
                StatusText = "Private wiki: log in to finish loading";
                UpdateButtons();
                return;
            }
            RefreshUser();
            StatusText = ok ? (session.User.IsLoggedIn ? "Site loaded" : "Site loaded. Log in to start editing.") : "Couldn't load the site";
            UpdateButtons();
        }

        /// <summary>Same choices as Main.SetProject</summary>
        private static InterWikiOrderEnum InterWikiOrderFor(string lang, ProjectEnum project)
        {
            if (project == ProjectEnum.commons) return InterWikiOrderEnum.Alphabetical;
            switch (lang)
            {
                case "en": case "lb": case "pl": case "no": case "sv": case "simple":
                    return InterWikiOrderEnum.LocalLanguageAlpha;
                case "he": case "hu": case "te": case "yi":
                    return InterWikiOrderEnum.AlphabeticalEnFirst;
                case "ms": case "et": case "nn": case "fi": case "vi": case "ur":
                    return InterWikiOrderEnum.LocalLanguageFirstWord;
                default:
                    return InterWikiOrderEnum.Alphabetical;
            }
        }

        private void RefreshUser()
        {
            if (session == null || !session.User.IsLoggedIn) { userLabel.Text = "Not logged in"; loginButton.Content = "Log in…"; return; }
            userLabel.Text = session.User.Name + (session.User.IsBot ? " (bot)" : "");
            loginButton.Content = "Switch account…";
        }

        private async Task LoginAsync()
        {
            if (session == null) return;
            if (session.IsBusy)
            {
                await Message("Can't log in while a page is being saved. Try again in a moment.", "Session busy");
                return;
            }
            var choice = await LoginDialog.ShowAsync(this, Variables.URL, lastUser, macSettings.SavedUsers,
                macSettings.AutoLoginUser, Keychain.GetPassword);
            macSettings.Save(); // "Forget saved password" may have changed the list
            if (choice == null) return;

            if (!await LogInAsync(choice.User, choice.Password)) return;

            // credentials were accepted: update the Keychain and startup login as asked
            if (choice.RememberPassword)
            {
                if (Keychain.SavePassword(choice.User, choice.Password))
                {
                    if (!macSettings.SavedUsers.Contains(choice.User)) macSettings.SavedUsers.Add(choice.User);
                }
                else
                {
                    await Message("The password couldn't be saved to your Keychain.", "Keychain", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (macSettings.SavedUsers.Remove(choice.User))
            {
                Keychain.DeletePassword(choice.User);
            }

            if (choice.LogInAtStartup && macSettings.SavedUsers.Contains(choice.User))
                macSettings.AutoLoginUser = choice.User;
            else if (macSettings.AutoLoginUser == choice.User)
                macSettings.AutoLoginUser = "";
            macSettings.LastUser = choice.User;
            macSettings.Save();
        }

        /// <summary>Logs in and checks status (AWBProfilesForm.PerformLogin + Main.CheckStatus).
        /// False if the wiki rejected the username/password.</summary>
        private async Task<bool> LogInAsync(string user, string password)
        {
            lastUser = user;
            StopProcessing();

            string loginError = null;
            WikiStatusResult status = WikiStatusResult.Error;
            bool ok = await RunBackground("Logging in as " + user + "…", () =>
            {
                try
                {
                    session.Editor.SynchronousEditor.Login(user, password, Variables.LoginDomain);
                }
                catch (LoginException ex)
                {
                    loginError = ex.Message;
                    return;
                }
                if (Variables.TryLoadingAgainAfterLogin)
                {
                    var r = Variables.ReloadProjectSettings;
                    Variables.SetProject(r.langCode, r.projectName, r.customProject, r.protocol);
                }
                status = session.Update();
            });

            RefreshUser();
            if (loginError != null)
            {
                StatusText = "Login failed";
                await Message(loginError, "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateButtons();
                return false;
            }
            if (ok)
            {
                templateRedirectsLoaded = datedTemplatesLoaded = renamedParametersLoaded = false;
                await HandleStatusAsync(status);
            }
            UpdateButtons();
            return ok;
        }

        private async Task AutoLoginAsync()
        {
            string user = macSettings.AutoLoginUser;
            if (string.IsNullOrEmpty(user) || !macSettings.SavedUsers.Contains(user)) return;
            string password = Keychain.GetPassword(user);
            if (password == null)
            {
                StatusText = "No saved password for " + user + " in the Keychain";
                return;
            }
            await LogInAsync(user, password);
        }

        /// <summary>Same outcomes as Main.CheckStatus</summary>
        private async Task HandleStatusAsync(WikiStatusResult status)
        {
            switch (status)
            {
                case WikiStatusResult.Registered:
                    StatusText = "Logged in, user and software enabled. Bot = " + session.User.IsBot + ", Admin = " + session.User.IsSysop;
                    if (typosCheck.IsChecked == true) await LoadTyposAsync();
                    break;
                case WikiStatusResult.NotLoggedIn:
                    StatusText = "Not logged in";
                    await Message("You're not logged in. Check the username and password and try again.", "Not logged in");
                    break;
                case WikiStatusResult.NotRegistered:
                    StatusText = "Software disabled";
                    await Message(session.User.Name + " is not enabled to use AutoWikiBrowser on this wiki. " +
                                  "Request access on the wiki's AutoWikiBrowser CheckPage, which will now open.",
                        "Not enabled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    MacSystem.OpenUrl(Variables.URLIndex + "?title=Project:AutoWikiBrowser/CheckPageJSON");
                    break;
                case WikiStatusResult.OldVersion:
                    StatusText = "Software disabled";
                    await Message("This version of AWB (" + Session.AWBVersion + ") is not enabled. Editing is turned off.",
                        "Version disabled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case WikiStatusResult.NoRights:
                    StatusText = "Software disabled";
                    await Message("Your account doesn't have the rights needed to edit through the API.", "No rights",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                default:
                    StatusText = "User check problem";
                    await Message("Check page failed to load.\n\nCheck your Internet is working and that the wiki is online.",
                        "User check problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }

        private async Task LoadTyposAsync()
        {
            RegExTypoFix loaded = null;
            await RunBackground("Loading typos…", () => loaded = new RegExTypoFix(false));
            typos = loaded;
            TyposNeedReload = false;
            if (typos == null || !typos.TyposLoaded)
            {
                typosCheck.IsChecked = false;
                StatusText = "Couldn't load the typo list";
            }
            else StatusText = "Typos loaded";
        }
        #endregion

        #region List
        private async Task MakeListAsync()
        {
            if (session == null || !(sourceBox.SelectedItem is ListSource src)) return;
            string input = (sourceInput.Text ?? "").Trim();
            string[] criteria;

            if (src.NeedsFile)
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Load list from text file",
                    AllowMultiple = true,
                    FileTypeFilter = new[] { new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } }, FilePickerFileTypes.All }
                });
                criteria = files.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (criteria.Length == 0) return;
            }
            else if (src.NeedsInput)
            {
                if (input.Length == 0) { StatusText = "Enter what to make the list from"; return; }
                criteria = input.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            }
            else criteria = new string[0];

            List<Article> made = null;
            makeListButton.IsEnabled = false;
            await RunBackground("Making list…", () => made = src.Make().MakeList(criteria));
            makeListButton.IsEnabled = true;

            int added = 0;
            if (made != null)
            {
                var existing = new HashSet<string>(pages);
                foreach (Article a in made)
                {
                    if (a == null || string.IsNullOrEmpty(a.Name) || !existing.Add(a.Name)) continue;
                    pages.Add(a.Name);
                    added++;
                }
            }
            StatusText = "Added " + added + " page" + (added == 1 ? "" : "s") + " from " + src.Name.TrimEnd('…');
        }

        private void AddPage(string title)
        {
            if (title.Length == 0) return;
            title = WikiFunctions.Controls.Lists.ListMaker.NormalizeTitleCore(title);
            if (!pages.Contains(title)) pages.Add(title);
        }

        private async Task SaveListAsync()
        {
            if (pages.Count == 0) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save list",
                SuggestedFileName = "AWB list.txt",
                DefaultExtension = "txt"
            });
            string path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            foreach (string p in pages) sb.Append("# [[").Append(p).Append("]]\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            StatusText = "List saved";
        }
        #endregion

        #region Processing (mirrors Main.cs)
        private sealed class ProcessOptions
        {
            public bool GenFixes, AutoTag, Typos, FindAndReplace, SkipNoFindAndReplace, SkipNoTypo,
                SkipNoChanges, SkipWhitespace, SkipCasing, SkipGenFixes, SkipMinorGenFixes,
                Unicodify, SkipNoCat, RemoveSortKey, SkipNoFile, Append, AppendAtEnd, AppendSort,
                ReplaceReferenceTags, RestrictDefaultsort, NoMOSComplianceFixes, RestrictOrphanTagging, IgnoreNoBots;
            public int CatMode, FileMode, Newlines;
            public string Cat1 = "", Cat2 = "", File1 = "", File2 = "", AppendText = "";
        }

        private ProcessOptions ReadOptions() => new ProcessOptions
        {
            GenFixes = genFixesCheck.IsChecked == true,
            AutoTag = autoTagCheck.IsChecked == true,
            Typos = typosCheck.IsChecked == true,
            FindAndReplace = farCheck.IsChecked == true,
            SkipNoFindAndReplace = skipNoFarCheck.IsChecked == true,
            SkipNoTypo = skipNoTypoCheck.IsChecked == true,
            SkipNoChanges = skipNoChangesCheck.IsChecked == true,
            SkipWhitespace = skipWhitespaceCheck.IsChecked == true,
            SkipCasing = skipCasingCheck.IsChecked == true,
            SkipGenFixes = skipGenFixesCheck.IsChecked == true,
            SkipMinorGenFixes = skipMinorGenFixesCheck.IsChecked == true,
            Unicodify = unicodifyCheck.IsChecked == true,
            CatMode = Math.Max(0, catModeBox.SelectedIndex),
            Cat1 = (catBox1.Text ?? "").Trim(),
            Cat2 = (catBox2.Text ?? "").Trim(),
            SkipNoCat = skipNoCatCheck.IsChecked == true,
            RemoveSortKey = removeSortKeyCheck.IsChecked == true,
            FileMode = Math.Max(0, fileModeBox.SelectedIndex),
            File1 = (fileBox1.Text ?? "").Trim(),
            File2 = (fileBox2.Text ?? "").Trim(),
            SkipNoFile = skipNoFileCheck.IsChecked == true,
            Append = appendCheck.IsChecked == true,
            AppendAtEnd = appendRadio.IsChecked == true,
            AppendText = appendTextBox.Text ?? "",
            Newlines = (int)(newlinesBox.Value ?? 2),
            AppendSort = appendSortCheck.IsChecked == true,
            // options that are only in settings files for now (Windows AWB menus)
            ReplaceReferenceTags = prefs.General.ReplaceReferenceTags,
            RestrictDefaultsort = prefs.General.restrictDefaultsortAddition,
            NoMOSComplianceFixes = prefs.General.noMOSComplianceFixes,
            RestrictOrphanTagging = prefs.General.restrictOrphanTagging,
            IgnoreNoBots = prefs.General.IgnoreNoBots
        };

        public async void StartProcessing()
        {
            if (session == null || session.Status != WikiStatusResult.Registered)
            {
                await Message("Log in first.", "Not logged in");
                return;
            }
            if (Variables.Project != ProjectEnum.custom && string.IsNullOrWhiteSpace(summaryBox.Text))
            {
                await Message("Please enter an edit summary.", "Edit summary", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                summaryBox.Focus();
                return;
            }
            RememberSummary();
            if (typosCheck.IsChecked == true && (typos == null || TyposNeedReload)) await LoadTyposAsync();
            running = true;
            retries = 0;
            UpdateButtons();
            LoadNext();
        }

        public void StopProcessing()
        {
            if (!running && busyCount == 0) return;
            running = false;
            if (session?.Editor != null && session.Editor.IsActive) session.Editor.Abort();
            busyCount = 0;
            busyBar.IsVisible = false;
            StatusText = "Stopped";
            UpdateButtons();
        }

        private void ClearPage()
        {
            pageReady = false;
            current = null;
            originalLf = "";
            editBox.Text = "";
            finalSummaryBox.Text = "";
            pageTitleLabel.Text = "No page loaded";
            UpdateButtons();
        }

        private void LoadNext()
        {
            if (!running || session.IsBusy) return;
            ClearPage();

            if (pages.Count == 0)
            {
                running = false;
                StatusText = NumberOfEdits + skipped > 0 ? "Finished the list" : "No pages in the list. Make a list first.";
                diff.ShowMessage("List finished", "");
                UpdateButtons();
                return;
            }

            string title = pages[0];
            if (!Tools.IsValidTitle(title))
            {
                current = new Article(title, "");
                SkipPage("Invalid page title");
                return;
            }
            string fixedTitle = Parsers.CanonicalizeTitleAggressively(title);
            if (fixedTitle != title)
            {
                pages[0] = fixedTitle;
                title = fixedTitle;
            }

            current = new Article(title, "");
            pageTitleLabel.Text = title;
            diff.ShowMessage("Loading…", title);
            StatusText = "Loading " + title + "…";
            IsBusy = true;
            // if "skip redirects" is on, don't follow them (same rule as Main.OpenPage)
            session.Editor.Open(title, prefs.General.BypassRedirect && skipRedirectCheck.IsChecked != true);
        }

        private static readonly Regex UnicodePUA = new Regex(@"\p{IsPrivateUse}", RegexOptions.Compiled);

        private async void PageLoaded(PageInfo page)
        {
            IsBusy = false;
            if (!running) return;
            retries = 0;

            if (!page.Exists && skipNonExistentCheck.IsChecked == true) { SkipPage("Non-existent page"); return; }

            if (session.User.HasMessages)
            {
                StopProcessing();
                await Message("You have new messages on your talk page. Read them, then press Start to carry on.", "New messages");
                MacSystem.OpenUrl(Variables.URLIndex + "?title=User_talk:" + Uri.EscapeDataString(session.User.Name.Replace(' ', '_')));
                return;
            }
            if (!session.User.IsLoggedIn)
            {
                StopProcessing();
                RefreshUser();
                await Message("You've been logged off, probably due to loss of session data. Please log in again.",
                    "Logged off", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string requested = current?.Name;
            current = new Article(page);
            pageTitleLabel.Text = page.Title;

            if (skipRedirectCheck.IsChecked == true && Tools.IsRedirect(page.Text)) { SkipPage("Page is a redirect"); return; }

            if ((PageInfo.WasRedirected(page) || page.TitleChangedStatus == PageTitleStatus.Normalised)
                && requested != null && requested != page.Title)
            {
                int i = pages.IndexOf(requested);
                if (i >= 0) pages[i] = page.Title;
            }

            ErrorHandler.CurrentRevision = page.RevisionID;

            if (current.IsInUse && skipInUseCheck.IsChecked == true) { SkipPage("Page contains {{inuse}}"); return; }
            if (UnicodePUA.IsMatch(page.Text)) { SkipPage("Page has character in Unicode Private Use Area"); return; }

            var options = ReadOptions();
            var article = current;
            bool ok = false;
            StatusText = "Processing " + article.Name + "…";
            IsBusy = true;
            await Task.Run(() => ok = ProcessPage(article, options));
            IsBusy = false;

            if (!running || !ReferenceEquals(article, current)) return;
            if (!ok) { StopProcessing(); return; }
            RunSkipChecks(article, options);
        }

        /// <summary>Main.ProcessPage, for the options available on the Mac. Runs off the UI thread.</summary>
        private bool ProcessPage(Article article, ProcessOptions o)
        {
            try
            {
                ErrorHandler.CurrentPage = article.Name;
                bool process = session.NoGenfixes == null || !session.NoGenfixes.Contains(article.Name);

                article.AWBChangeArticleText("Fixes for Unicode compatibility", parser.FixUnicode(article.ArticleText), true);

                if (!o.IgnoreNoBots && !Parsers.CheckNoBots(article.ArticleText, session.User.Name))
                {
                    article.AWBSkip("Restricted by {{bots}}/{{nobots}}");
                    return true;
                }

                if (process && o.Unicodify)
                    article.Unicodify(skipOptions.SkipNoUnicode, parser, removeText);

                if (o.FindAndReplace)
                {
                    article.PerformFindAndReplace(findAndReplace, substTemplates, replaceSpecial, o.SkipNoFindAndReplace, false, false);
                    article.DoFaRSkips(findAndReplace);
                    if (article.SkipArticle) return true;
                }

                if (o.CatMode != 0)
                {
                    article.Categorisation((CategorisationOptions)o.CatMode, parser, o.SkipNoCat, o.Cat1, o.Cat2, o.RemoveSortKey);
                    if (article.SkipArticle) return true;
                    if (!o.GenFixes)
                        article.AWBChangeArticleText("Fix categories", Parsers.FixCategories(article.ArticleText), true);
                }

                if (process)
                {
                    if (o.GenFixes) article.PerformUniversalGeneralFixes();

                    if (article.CanDoGeneralFixes)
                    {
                        if (o.GenFixes)
                        {
                            LoadGeneralFixData();
                            article.PerformGeneralFixes(parser, removeText, skipOptions,
                                o.ReplaceReferenceTags, o.RestrictDefaultsort, o.NoMOSComplianceFixes);
                        }
                        if (o.AutoTag)
                        {
                            article.AutoTag(parser, false, o.RestrictOrphanTagging);
                            if (article.SkipArticle) return true;
                        }
                    }
                    else if (o.GenFixes && article.NameSpaceKey != Namespace.UserTalk && article.CanDoTalkGeneralFixes)
                    {
                        article.PerformTalkGeneralFixes(removeText);
                    }
                }

                if (o.Typos && typos != null && typos.TyposLoaded && !Namespace.IsTalk(article.NameSpaceKey))
                {
                    if (session.NoRETF == null || !session.NoRETF.Contains(article.Name))
                    {
                        article.PerformTypoFixes(typos, o.SkipNoTypo);
                        if (article.SkipArticle) return true;
                    }
                    else if (o.SkipNoTypo)
                    {
                        article.AWBSkip("No typo fixes (Title blacklisted from RegExTypoFix Typo Fixing)");
                        return true;
                    }
                }

                if (o.FindAndReplace)
                {
                    article.PerformFindAndReplace(findAndReplace, substTemplates, replaceSpecial, o.SkipNoFindAndReplace, false, true);
                    article.DoFaRSkips(findAndReplace);
                    if (article.SkipArticle) return true;
                }

                if (o.Append)
                {
                    string newlines = new string('\n', Math.Max(0, o.Newlines));
                    string text = Tools.ApplyKeyWords(article.Name, o.AppendText);
                    if (o.AppendAtEnd)
                        article.AWBChangeArticleText("Appended your message", article.ArticleText + newlines + text, false);
                    else
                        article.AWBChangeArticleText("Prepended your message", text + newlines + article.ArticleText, false);
                    if (o.AppendSort) article.PerformMetaDataSort(parser);
                }

                if (o.FileMode != 0)
                {
                    article.UpdateImages((ImageReplaceOptions)o.FileMode, o.File1, o.File2, o.SkipNoFile);
                    if (article.SkipArticle) return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex);
                return false;
            }
        }

        /// <summary>Main.LoadTemplateRedirects / LoadDatedTemplates / LoadRenameTemplateParameters</summary>
        private void LoadGeneralFixData()
        {
            string Load(string page)
            {
                try { return session.Editor.SynchronousEditor.Clone().Open(page, true); }
                catch { return ""; }
            }

            if (!templateRedirectsLoaded)
            {
                templateRedirectsLoaded = true;
                WikiRegexes.TemplateRedirects = Parsers.LoadTemplateRedirects(Load("Project:AutoWikiBrowser/Template redirects"));
            }
            if (!datedTemplatesLoaded)
            {
                datedTemplatesLoaded = true;
                string text = Load("Project:AutoWikiBrowser/Dated templates");
                if (text.Length > 0) WikiRegexes.DatedTemplates = Parsers.LoadDatedTemplates(text);
            }
            if (!renamedParametersLoaded)
            {
                renamedParametersLoaded = true;
                string text = Load("Project:AutoWikiBrowser/Rename template parameters");
                if (text.Length > 0) WikiRegexes.RenamedTemplateParameters = Parsers.LoadRenamedTemplateParameters(text);
            }
        }

        /// <summary>Main.RunSkipChecks</summary>
        private void RunSkipChecks(Article a, ProcessOptions o)
        {
            if (a.SkipArticle) { SkipPage(null); return; }
            if (o.SkipNoChanges && a.NoArticleTextChanged) { SkipPage("No change"); return; }
            if (o.SkipWhitespace && o.SkipCasing && a.OnlyWhiteSpaceAndCasingChanged) { SkipPage("Only whitespace/casing changed"); return; }
            if (o.SkipWhitespace && a.OnlyWhiteSpaceChanged) { SkipPage("Only whitespace changed"); return; }
            if (o.SkipCasing && a.OnlyCasingChanged) { SkipPage("Only casing changed"); return; }
            if (o.SkipMinorGenFixes && o.GenFixes && a.OnlyMinorGeneralFixesChanged) { SkipPage("Only minor general fix changes"); return; }
            if (o.SkipGenFixes && o.GenFixes && a.OnlyGeneralFixesChanged) { SkipPage("Only general fix changes"); return; }
            CompleteProcessPage();
        }

        /// <summary>Main.CompleteProcessPage: show the result for review</summary>
        private void CompleteProcessPage()
        {
            originalLf = Ui.ToLf(current.OriginalArticleText);
            editBox.Text = Ui.ToLf(current.ArticleText);
            finalSummaryBox.Text = MakeDefaultEditSummary();
            pageReady = true;
            StatusText = "Ready to save: review the changes, then Save (⌘S) or Skip (⌘I)";
            UpdateButtons();
            ShowDiff();
        }

        /// <summary>Main.MakeDefaultEditSummary</summary>
        private string MakeDefaultEditSummary()
        {
            if (current == null) return "";
            string summary = (summaryBox.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(current.EditSummary))
            {
                bool arabicComma = Variables.LangCode == "ar" || Variables.LangCode == "arz" || Variables.LangCode == "fa";
                summary += (summary.Length == 0 ? "" : arabicComma ? "، " : ", ") + current.EditSummary;
            }
            if (!prefs.General.noSectionEditSummary)
            {
                string section = Summary.ModifiedSection(originalLf, editBox.Text ?? "");
                if (!string.IsNullOrEmpty(section))
                    summary = "/* " + section + " */ " + summary.TrimStart();
            }
            return summary;
        }

        public void ShowDiff()
        {
            if (!pageReady || current == null) return;
            showingDiff = true;
            string edited = editBox.Text ?? "";
            if (originalLf == edited)
                diff.ShowMessage("No changes", "Press Skip to go to the next page.");
            else
                diff.ShowHtml(new WikiDiff().GetDiff(originalLf, edited, 2));
            editorTabs.SelectedIndex = 0;
            showingDiff = false;
        }

        /// <summary>Main.GetPreview: the wiki renders the edit box text; shown in the default browser</summary>
        public void ShowPreview()
        {
            if (!pageReady || current == null || session.Editor.IsActive) return;
            IsBusy = true;
            StatusText = "Getting preview…";
            session.Editor.Preview(current.Name, editBox.Text ?? "");
        }

        private void PreviewReady(AsyncApiEdit sender, string html)
        {
            IsBusy = false;
            string title = WebUtility.HtmlEncode(current?.Name ?? "");
            string page = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><base href=\"" + Variables.URL + "/\">"
                          + "<title>Preview: " + title + "</title>" + sender.HtmlHeaders + "</head>"
                          + "<body class=\"mediawiki\" style=\"background:white; margin:16px 24px; text-align:left;\">"
                          + "<div style=\"font:13px -apple-system,sans-serif; background:#fef6e7; border:1px solid #fc3; padding:8px 12px; margin-bottom:16px;\">"
                          + "AutoWikiBrowser preview: not saved yet</div>"
                          + "<h1 class=\"firstHeading\">" + title + "</h1>"
                          + "<div class=\"mw-body-content\"><div class=\"mw-parser-output\">" + html + "</div></div></body></html>";
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "AutoWikiBrowser preview.html");
                File.WriteAllText(path, page, new UTF8Encoding(false));
                System.Diagnostics.Process.Start("/usr/bin/open", "\"" + path + "\"");
                StatusText = "Preview opened in your browser";
            }
            catch (Exception ex)
            {
                StatusText = "Couldn't show the preview: " + ex.Message;
            }
        }

        private void GoToLine(int line)
        {
            string text = editBox.Text ?? "";
            int index = 0;
            for (int i = 0; i < line && index >= 0; i++)
            {
                index = text.IndexOf('\n', index);
                if (index >= 0) index++;
            }
            editorTabs.SelectedIndex = 1;
            editBox.CaretIndex = Math.Max(0, Math.Min(index < 0 ? text.Length : index, text.Length));
            editBox.Focus();
        }

        public async void SaveCurrent()
        {
            if (!pageReady || current == null || session.Editor.IsActive) return;
            if (session.Page == null || current.Name != session.Page.Title)
            {
                await Message("The page in the editor doesn't match the page that was loaded, so it wasn't saved.", "Not saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string text = editBox.Text ?? "";
            if (text.Length == 0 &&
                (current.Exists != Exists.Yes ||
                 await Message("Do you really want to save a blank page?", "Save?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes))
            {
                SkipPage("Nothing to save - blank page");
                return;
            }

            string summary = finalSummaryBox.Text ?? "";
            if (summary.StartsWith("/*"))
            {
                // drop a section marker that no longer matches what was edited (Main.SaveArticle)
                string section = Summary.ModifiedSection(originalLf, text);
                if (section.Length == 0 || !summary.Contains("/* " + section + " */"))
                {
                    int end = summary.IndexOf("*/", StringComparison.Ordinal);
                    if (end >= 0) summary = summary.Substring(end + 2);
                }
            }
            if (Variables.IsWikimediaProject) summary = Summary.Trim(summary) + Variables.SummaryTag;

            WatchOptions watch;
            switch (watchBox.SelectedIndex)
            {
                case 0: watch = WatchOptions.Watch; break;
                case 1: watch = WatchOptions.Unwatch; break;
                case 3: watch = WatchOptions.UsePreferences; break;
                default: watch = WatchOptions.NoChange; break;
            }

            pageReady = false;
            UpdateButtons();
            IsBusy = true;
            StatusText = "Saving " + current.Name + "…";
            session.Editor.Save(text, summary, minorCheck.IsChecked == true, watch);
        }

        private void PageSaved(SaveInfo info)
        {
            IsBusy = false;
            retries = 0;
            NumberOfEdits++;
            if (current != null)
            {
                AddLog("Saved      " + current.Name);
                pages.Remove(current.Name);
            }
            UpdateCounts();
            StatusText = "Saved";
            if (running) LoadNext();
            else ClearPage();
        }

        public void SkipCurrent(string reason)
        {
            if (current == null || (!pageReady && reason == "user")) return;
            SkipPage(reason);
        }

        /// <summary>Main.SkipPage: log, drop from the list, go to the next page</summary>
        private void SkipPage(string reason)
        {
            if (current == null) return;
            if (reason == null) reason = current.LogListener?.SkipReason;
            string shown = reason == "user" ? "skipped by you" : string.IsNullOrEmpty(reason) ? "skipped" : reason;
            AddLog("Skipped    " + current.Name + "  (" + shown + ")");
            skipped++;
            pages.Remove(current.Name);
            UpdateCounts();
            StatusText = "Skipped " + current.Name + ": " + shown;
            ClearPage();
            if (running) LoadNext();
        }

        private void Maxlag(int retryAfter)
        {
            IsBusy = false;
            if (++retries < 10)
            {
                StatusText = "The server is busy (maxlag). Retrying in " + retryAfter + " s…";
                DispatcherTimer.RunOnce(() => { if (running) LoadNext(); }, TimeSpan.FromSeconds(Math.Max(1, retryAfter)));
            }
            else
            {
                StopProcessing();
                _ = Message("Maxlag exceeded " + retries + " times in a row. Processing stopped; please try later when the servers are less busy.",
                    "Stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Main.ApiEditExceptionCaught, for the common cases</summary>
        private async void EditorException(Exception ex)
        {
            IsBusy = false;
            switch (ex)
            {
                case InterwikiException _:
                    SkipPage(ex.Message);
                    return;
                case SpamlistException spam:
                    SkipPage("Spam blacklist: " + spam.URL);
                    return;
                case ApiErrorException api:
                    switch (api.ErrorCode)
                    {
                        case "editconflict":
                            await Message("There has been an edit conflict. AWB will now re-apply its changes on the updated page. " +
                                          "Please review the changes again before saving. Any manual edits will need to be redone.",
                                "Edit conflict");
                            LoadNext();
                            return;
                        case "customcssprotected": SkipPage("You're not allowed to edit custom CSS pages"); return;
                        case "customjsprotected": SkipPage("You're not allowed to edit custom JavaScript pages"); return;
                        case "badmd5": SkipPage("API MD5 hash error: the page may contain an unsupported or invalid Unicode character"); return;
                        case "tpt-target-page": SkipPage("Translation pages cannot currently be edited"); return;
                        case "titleblacklist-forbidden-edit": SkipPage("TitleBlacklist prevents this title from being created"); return;
                        case "blocked":
                            StopProcessing();
                            await Message("The API reports this user is blocked from editing.", "User blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        case "readonly":
                            StopProcessing();
                            await Message(api.ApiErrorMessage, "Wiki read-only", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                    }
                    break;
            }

            StopProcessing();
            await Message(ex.Message, "Problem talking to the wiki", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}

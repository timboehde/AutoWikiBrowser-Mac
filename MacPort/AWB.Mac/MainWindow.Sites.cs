// Choosing the wiki: Wikimedia projects by language code, Fandom wikis by name, and any other
// MediaWiki wiki ("custom" project) by address, as in AWB/AWB/Preferences.cs.
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WikiFunctions;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace AutoWikiBrowser.Mac
{
    public partial class MainWindow
    {
        private void UpdateSiteFields()
        {
            var project = projectBox.SelectedItem is ProjectEnum p ? p : ProjectEnum.wikipedia;
            bool custom = project == ProjectEnum.custom, fandom = project == ProjectEnum.fandom;
            langBox.IsVisible = !custom && !fandom;
            wikiAddressBox.IsVisible = custom || fandom;
            fandomSuffix.IsVisible = fandom;
            wikiAddressBox.Watermark = fandom ? "wiki name, e.g. starwars" : "https://wiki.example.org/w/  or any page address";
        }

        private async Task SwitchSiteFromBarAsync()
        {
            var project = projectBox.SelectedItem is ProjectEnum p ? p : ProjectEnum.wikipedia;
            if (project == ProjectEnum.custom)
            {
                string input = (wikiAddressBox.Text ?? "").Trim();
                if (input.Length == 0) { await Message("Enter the wiki's address.", "Custom wiki"); return; }

                (string protocol, string customProject)? site = null;
                await RunBackground("Finding the wiki at " + input + "…", () => site = ResolveCustomWiki(input));
                if (site == null)
                {
                    await Message("That doesn't look like a wiki address. Try the address of the wiki's main page, " +
                                  "or its script path, such as https://wiki.example.org/w/", "Custom wiki",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await SwitchSiteAsync("en", ProjectEnum.custom, site.Value.customProject, site.Value.protocol);
                if (Variables.Project == ProjectEnum.custom)
                {
                    string entry = Variables.Protocol + Variables.CustomProject;
                    macSettings.CustomWikis.Remove(entry);
                    macSettings.CustomWikis.Insert(0, entry);
                    macSettings.Save();
                    wikiAddressBox.ItemsSource = null;
                    wikiAddressBox.ItemsSource = macSettings.CustomWikis;
                }
            }
            else if (project == ProjectEnum.fandom)
            {
                string name = Regex.Replace((wikiAddressBox.Text ?? "").Trim().ToLowerInvariant(),
                    @"^(?:https?://)?([^./]+)(?:\.fandom\.com.*)?$", "$1");
                if (name.Length == 0) { await Message("Enter the Fandom wiki's name, e.g. starwars.", "Fandom"); return; }
                await SwitchSiteAsync("en", ProjectEnum.fandom, name, "https://");
            }
            else
            {
                string lang = (langBox.Text ?? "").Trim().ToLowerInvariant();
                if (lang.Length == 0) lang = "en";
                await SwitchSiteAsync(lang, project);
            }
        }

        private static readonly Regex ScriptPathFromUrl = new Regex(@"^(.*?/)(?:index|api|load)\.php\b", RegexOptions.IgnoreCase);
        private static readonly Regex EditUriLink = new Regex(
            @"<link\b[^>]*\brel\s*=\s*[""']EditURI[""'][^>]*\bhref\s*=\s*[""']([^""']+?)api\.php",
            RegexOptions.IgnoreCase);
        private static readonly Regex EditUriLinkHrefFirst = new Regex(
            @"<link\b[^>]*\bhref\s*=\s*[""']([^""']+?)api\.php[^""']*[""'][^>]*\brel\s*=\s*[""']EditURI[""']",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Works out (protocol, host + script path) for a custom wiki, the form Variables.SetProject expects
        /// (e.g. "https://", "wiki.example.org/w/"). Accepts a script path, an api.php/index.php URL, or any page
        /// URL, in which case the page's EditURI link (which every MediaWiki page has) gives the script path.
        /// </summary>
        private static (string protocol, string customProject)? ResolveCustomWiki(string input)
        {
            string text = input.Trim();
            if (!Regex.IsMatch(text, "^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)) text = "https://" + text;
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                return null;

            // 1. explicit api.php / index.php address
            var m = ScriptPathFromUrl.Match(uri.GetLeftPart(UriPartial.Path));
            if (m.Success) return Split(new Uri(m.Groups[1].Value));

            // 2. ask the page itself
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("AutoWikiBrowser/" + Session.AWBVersion + " (Mac)");
                    var response = http.GetAsync(uri).GetAwaiter().GetResult();
                    string html = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    var link = EditUriLink.Match(html);
                    if (!link.Success) link = EditUriLinkHrefFirst.Match(html);
                    if (link.Success)
                    {
                        string href = System.Net.WebUtility.HtmlDecode(link.Groups[1].Value);
                        // relative to the page that was actually served (after redirects)
                        var baseUri = response.RequestMessage?.RequestUri ?? uri;
                        return Split(new Uri(baseUri, href));
                    }
                }
            }
            catch { /* fall through to treating the address as the script path */ }

            // 3. as typed (Windows AWB's behaviour): host + path is the script path
            if (uri.AbsolutePath.Contains("/wiki/")) return null; // a page URL whose wiki we couldn't reach
            return Split(uri);
        }

        private static (string protocol, string customProject) Split(Uri scriptPath)
        {
            string path = scriptPath.AbsolutePath;
            if (!path.EndsWith("/")) path += "/";
            return (scriptPath.Scheme + "://", scriptPath.Authority + path);
        }
    }
}

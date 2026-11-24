using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AutoWikiBrowser.Mac.Dialogs
{
    internal sealed class LoginChoice
    {
        public string User;
        public string Password;
        public bool RememberPassword;
        public bool LogInAtStartup;
    }

    internal static class LoginDialog
    {
        /// <summary>Returns the user's choices, or null if cancelled</summary>
        public static async Task<LoginChoice> ShowAsync(Window owner, string site, string defaultUser,
            IList<string> savedUsers, string autoLoginUser, Func<string, string> savedPassword)
        {
            LoginChoice result = null;
            var user = new AutoCompleteBox
            {
                Text = defaultUser ?? "", ItemsSource = savedUsers, MinimumPrefixLength = 0, FilterMode = AutoCompleteFilterMode.StartsWith,
                Watermark = "Username (or Username@BotPasswordName)", Width = 360
            };
            var pass = new TextBox { PasswordChar = '•', Watermark = "Password", Width = 360 };
            var remember = new CheckBox { Content = "Remember password in my Keychain" };
            var startup = new CheckBox { Content = "Log in automatically when AutoWikiBrowser starts" };
            var forget = new Button { Content = "Forget saved password", IsVisible = false, Padding = new Thickness(8, 2) };

            string filledFor = null;
            void FillFromKeychain()
            {
                string u = (user.Text ?? "").Trim();
                bool saved = savedUsers.Contains(u);
                forget.IsVisible = saved;
                remember.IsChecked = saved || remember.IsChecked == true;
                startup.IsChecked = !string.IsNullOrEmpty(u) && u == autoLoginUser;
                if (saved && filledFor != u)
                {
                    filledFor = u;
                    pass.Text = savedPassword(u) ?? "";
                }
            }
            user.LostFocus += (s, e) => FillFromKeychain();
            user.SelectionChanged += (s, e) => FillFromKeychain();
            remember.IsCheckedChanged += (s, e) => { if (remember.IsChecked != true) startup.IsChecked = false; };
            startup.IsCheckedChanged += (s, e) => { if (startup.IsChecked == true) remember.IsChecked = true; };

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "Log in to " + site, FontSize = 16, FontWeight = FontWeight.SemiBold });
            panel.Children.Add(user);
            panel.Children.Add(pass);
            panel.Children.Add(remember);
            panel.Children.Add(startup);
            panel.Children.Add(forget);
            panel.Children.Add(DialogHelper.Wrapped(
                "A bot password is recommended (Special:BotPasswords on the wiki), with the \"Edit existing pages\" grant. " +
                "Your account must also be approved on the wiki's AutoWikiBrowser CheckPage.", 360));

            var window = DialogHelper.Make("Log in", panel);
            forget.Click += (s, e) =>
            {
                string u = (user.Text ?? "").Trim();
                Keychain.DeletePassword(u);
                savedUsers.Remove(u);
                pass.Text = "";
                remember.IsChecked = startup.IsChecked = false;
                forget.IsVisible = false;
                filledFor = null;
            };
            var botPasswords = new Button { Content = "Bot passwords…" };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
            var ok = new Button { Content = "Log in", IsDefault = true, MinWidth = 80 };
            ok.Classes.Add("accent");
            botPasswords.Click += (s, e) => MacSystem.OpenUrl(WikiFunctions.Variables.URLIndex + "?title=Special:BotPasswords");
            cancel.Click += (s, e) => window.Close();
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(user.Text) || string.IsNullOrEmpty(pass.Text)) return;
                result = new LoginChoice
                {
                    User = user.Text.Trim(),
                    Password = pass.Text,
                    RememberPassword = remember.IsChecked == true,
                    LogInAtStartup = startup.IsChecked == true
                };
                window.Close();
            };
            panel.Children.Add(DialogHelper.ButtonRow(new[] { botPasswords, cancel, ok }));
            window.Opened += (s, e) =>
            {
                FillFromKeychain();
                if (string.IsNullOrEmpty(user.Text)) user.Focus(); else pass.Focus();
            };

            await DialogHelper.ShowAsync(window, owner);
            return result;
        }
    }
}

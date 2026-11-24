using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using Avalonia.Markup.Xaml;

namespace AutoWikiBrowser.Mac
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        private void OnAboutClicked(object sender, EventArgs e) =>
            AboutWindow.Show((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}

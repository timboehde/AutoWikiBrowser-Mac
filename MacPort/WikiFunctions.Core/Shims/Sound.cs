// macOS replacement for System.Media.SoundPlayer (Windows-only in .NET 8): plays through /usr/bin/afplay.
using System.Diagnostics;
using System.IO;

namespace System.Media
{
    public class SoundPlayer
    {
        private string tempFile;
        private Stream stream;

        public Stream Stream
        {
            get => stream;
            set { stream = value; tempFile = null; }
        }

        public void Play()
        {
            try
            {
                if (stream == null) return;
                if (tempFile == null)
                {
                    tempFile = Path.Combine(Path.GetTempPath(), "awb-sound-" + Guid.NewGuid().ToString("N") + ".wav");
                    stream.Position = 0;
                    using (var f = File.Create(tempFile)) stream.CopyTo(f);
                }
                Process.Start(new ProcessStartInfo("/usr/bin/afplay", "\"" + tempFile + "\"") { UseShellExecute = false });
            }
            catch { /* a missing beep is never fatal */ }
        }
    }
}

namespace WikiFunctions.Properties
{
    /// <summary>Only the resources the engine uses; images and icons belong to the app.</summary>
    internal static class Resources
    {
        internal static UnmanagedMemoryStream beep1 =>
            (UnmanagedMemoryStream)typeof(Resources).Assembly.GetManifestResourceStream("WikiFunctions.beep.wav");
    }
}

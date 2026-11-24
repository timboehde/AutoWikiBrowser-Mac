// Mac-only app state that Windows AWB keeps in the Registry (recent settings files, etc.).
// Stored as JSON in ~/Library/Application Support/AutoWikiBrowser/MacSettings.json.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WikiFunctions;

namespace AutoWikiBrowser.Mac
{
    public sealed class MacSettings
    {
        public List<string> RecentSettingsFiles { get; set; } = new List<string>();
        public string LastUser { get; set; } = "";
        /// <summary>Usernames with a password saved in the Keychain (the passwords themselves are only in the Keychain)</summary>
        public List<string> SavedUsers { get; set; } = new List<string>();
        /// <summary>Log in as this user at startup (Windows AWB's startup profile)</summary>
        public string AutoLoginUser { get; set; } = "";
        /// <summary>Non-Wikimedia wikis used before, as protocol + host/script path (Windows AWB's custom project list)</summary>
        public List<string> CustomWikis { get; set; } = new List<string>();

        private static string FilePath => Path.Combine(AwbDirs.UserData, "MacSettings.json");

        public static MacSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<MacSettings>(File.ReadAllText(FilePath)) ?? new MacSettings();
            }
            catch { /* a damaged file just means starting fresh */ }
            return new MacSettings();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public void AddRecent(string path)
        {
            RecentSettingsFiles.RemoveAll(p => string.Equals(p, path, StringComparison.Ordinal));
            RecentSettingsFiles.Insert(0, path);
            if (RecentSettingsFiles.Count > 10) RecentSettingsFiles.RemoveRange(10, RecentSettingsFiles.Count - 10);
            Save();
        }
    }
}

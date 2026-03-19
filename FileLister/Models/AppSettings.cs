using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FileLister.Models
{
    public class AppSettings
    {
        // 하위 호환용 (구버전 설정 → LastFolders 마이그레이션)
        public string? LastFolder { get; set; }

        public List<string> LastFolders { get; set; } = new();
        public bool IncludeSubfolders { get; set; } = true;
        public bool ExcludeHiddenFiles { get; set; } = false;
        public bool ExcludeSystemFiles { get; set; } = true;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileLister",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}

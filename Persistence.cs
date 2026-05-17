using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WiFiDeviceScanner
{
    public class AppConfig
    {
        public Dictionary<string, string> DeviceNames { get; set; } = new Dictionary<string, string>();
        public List<string> RecentNetworks { get; set; } = new List<string>();
        public Dictionary<string, DateTime> LastSeen { get; set; } = new Dictionary<string, DateTime>();
        public Dictionary<string, string> KnownIPs { get; set; } = new Dictionary<string, string>();
        public string LastNetworkSelection { get; set; } = string.Empty;
    }

    public static class ConfigStore
    {
        public const int MaxRecentNetworks = 10;

        public static string ConfigPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "WiFiDeviceScanner", "config.json");
            }
        }

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string? dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, options));
            }
            catch
            {
                // Silent failure: persistence is best-effort
            }
        }

        public static string NormalizeMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return string.Empty;
            return mac.Trim().Replace('-', ':').ToUpperInvariant();
        }

        public static void AddRecentNetwork(AppConfig config, string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return;

            entry = entry.Trim();
            config.RecentNetworks.RemoveAll(s => string.Equals(s, entry, StringComparison.OrdinalIgnoreCase));
            config.RecentNetworks.Insert(0, entry);
            if (config.RecentNetworks.Count > MaxRecentNetworks)
                config.RecentNetworks.RemoveRange(MaxRecentNetworks, config.RecentNetworks.Count - MaxRecentNetworks);
        }
    }
}

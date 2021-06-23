using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace kellerkompanie_sync
{
    public sealed class Settings
    {
        public static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kellerkompanie-sync");
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");
        public static readonly string LogFile = Path.Combine(SettingsDirectory, "logs", "kellerkompanie-sync.log");
        public static readonly string IndexFile = Path.Combine(SettingsDirectory, "index.json");

        [JsonProperty("AddonSearchDirectories")]
        private readonly ObservableCollection<FilePath> addonSearchDirectories = new();
        public string ExecutableLocation { get; set; } = "";
        public bool ParamShowScriptErrors { get; set; } = false;
        public bool ParamNoPause { get; set; } = true;
        public bool ParamWindowMode { get; set; } = false;
        public bool ParamNoSplashScreen { get; set; } = true;
        public bool ParamDefaultWorldEmpty { get; set; } = false;
        public bool ParamNoLogs { get; set; } = false;
        public string ParamAdditional { get; set; } = "";
        public Dictionary<string, string> SubscribedAddonGroups { get; set; } = new();
        public double WindowX { get; set; }
        public double WindowY { get; set; }
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 600;
        public int SimultaneousDownloads { get; set; } = 10;

        private Settings()
        {
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));

            WindowX = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - WindowWidth / 2;
            WindowY = System.Windows.SystemParameters.PrimaryScreenHeight / 2 - WindowHeight / 2;
        }

        public static Settings Instance { get; private set; } = new();

        internal void AddAddonSearchDirectory(FilePath directory)
        {
            if (!addonSearchDirectories.Contains(directory))
            {
                addonSearchDirectories.Add(directory);
                SaveSettings();
            }
        }

        public ObservableCollection<FilePath> GetAddonSearchDirectories()
        {
            return addonSearchDirectories;
        }

        public static void SaveSettings()
        {
            Directory.CreateDirectory(SettingsDirectory);

            using StreamWriter file = File.CreateText(@SettingsFile);
            JsonSerializer serializer = new();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(file, Instance);
        }

        public static void LoadSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                return;
            }

            using StreamReader file = File.OpenText(@SettingsFile);
            JsonSerializer serializer = new();
            Settings settings = (Settings)serializer.Deserialize(file, typeof(Settings));
            Instance = settings;
        }
    }
}

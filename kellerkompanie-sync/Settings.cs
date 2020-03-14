using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace kellerkompanie_sync
{
    public sealed class Settings
    {
        public static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kellerkompanie-sync");
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");
        public static readonly string LogFile = Path.Combine(SettingsDirectory, "logs", "kellerkompanie-sync.log");

        public HashSet<string> AddonSearchDirectories { get; set; }
        public string ExecutableLocation { get; set; }
        public bool ParamShowScriptErrors { get; set; }
        public bool ParamNoPause { get; set; }
        public bool ParamWindowMode { get; set; }
        public bool ParamNoSplashScreen{ get; set; }
        public bool ParamDefaultWorldEmpty { get; set; }
        public bool ParamNoLogs { get; set; }
        public string ParamAdditional { get; set; }
        public Dictionary<string, string> SubscribedAddonGroups { get; set; }
        public double WindowX { get; set; }
        public double WindowY { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public int SimultaneousDownloads { get; set; }

        private Settings()
        {
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));

            AddonSearchDirectories = new HashSet<string>();
            ExecutableLocation = "";
            ParamShowScriptErrors = false;
            ParamNoPause = true;
            ParamWindowMode = false;
            ParamNoSplashScreen = true;
            ParamDefaultWorldEmpty = false;
            ParamNoLogs = false;
            ParamAdditional = "";
            SubscribedAddonGroups = new Dictionary<string, string>();
            SimultaneousDownloads = 10;

            WindowWidth = 800;
            WindowHeight = 600;
            WindowX = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - WindowWidth / 2;
            WindowY = System.Windows.SystemParameters.PrimaryScreenHeight / 2 - WindowHeight / 2;
        }

        public static Settings Instance { get; private set; } = new Settings();

        internal void AddAddonSearchDirectory(string directory)
        {
            if (!AddonSearchDirectories.Contains(directory))
            {
                AddonSearchDirectories.Add(directory);
                SaveSettings();
            }
        }

        public void SaveSettings()
        {
            Directory.CreateDirectory(SettingsDirectory);

            using (StreamWriter file = File.CreateText(@SettingsFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, Instance);
            }
        }

        public void LoadSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                return;
            }

            using (StreamReader file = File.OpenText(@SettingsFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                Settings settings = (Settings)serializer.Deserialize(file, typeof(Settings));
                Instance = settings;
            }
        }
    }
}

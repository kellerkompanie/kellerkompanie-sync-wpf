using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace kellerkompanie_sync_wpf
{
    public sealed class Settings
    {
        private static Settings instance = new Settings();
        public static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kellerkompanie-sync");
        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");

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

        private Settings()
        {
            this.AddonSearchDirectories = new HashSet<string>();
            this.ExecutableLocation = "";
            this.ParamShowScriptErrors = false;
            this.ParamNoPause = true;
            this.ParamWindowMode = false;
            this.ParamNoSplashScreen = true;
            this.ParamDefaultWorldEmpty = false;
            this.ParamNoLogs = false;
            this.ParamAdditional = "";
            this.SubscribedAddonGroups = new Dictionary<string, string>();

            this.WindowWidth = 800;
            this.WindowHeight = 600;
            this.WindowX = System.Windows.SystemParameters.PrimaryScreenWidth / 2 - WindowWidth / 2;
            this.WindowY = System.Windows.SystemParameters.PrimaryScreenHeight / 2 - WindowHeight / 2;
        }

        public static Settings Instance
        {
            get
            {
                return instance;
            }
        }             

        internal void AddAddonSearchDirectory(string directory)
        {
            if (!this.AddonSearchDirectories.Contains(directory))
            {
                this.AddonSearchDirectories.Add(directory);
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
                serializer.Serialize(file, instance);
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
                instance = settings;
            }
        }
    }
}

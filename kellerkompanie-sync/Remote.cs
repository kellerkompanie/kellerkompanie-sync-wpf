using Newtonsoft.Json;
using System.Collections.Generic;

namespace kellerkompanie_sync
{
    public class RemoteAddon
    {
        [JsonProperty("addon_name")]
        public string AddonName { get; set; }

        [JsonProperty("addon_uuid")]
        public string AddonUuid { get; set; }

        [JsonProperty("addon_version")]
        public string AddonVersion { get; set; }

        [JsonProperty("addon_files")]
        public Dictionary<string, RemoteAddonFile> AddonFiles { get; set; }
    }

    public class RemoteAddonFile
    {
        [JsonProperty("file_path")]
        public string FilePath { get; set; }

        [JsonProperty("file_size")]
        public int FileSize { get; set; }

        [JsonProperty("file_hash")]
        public string FileHash { get; set; }
    }

    public class RemoteIndex
    {
        public RemoteIndex(Dictionary<string, RemoteAddon> map)
        {
            Map = map;
        }

        public Dictionary<string, RemoteAddon> Map { get; set; }
    }
}

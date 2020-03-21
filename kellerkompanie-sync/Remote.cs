using Newtonsoft.Json;
using System.Collections.Generic;

namespace kellerkompanie_sync
{
    public class RemoteIndex
    {
        [JsonProperty("files_index")]
        public Dictionary<string, RemoteAddon> FilesIndex { get; set; }
        [JsonProperty("addon_groups")]
        public List<WebAddonGroup> AddonGroups { get; set; }
    }

    public class RemoteAddon
    {
        [JsonProperty("addon_name")]
        public string Name { get; set; }

        [JsonProperty("addon_uuid")]
        public string Uuid { get; set; }

        [JsonProperty("addon_version")]
        public string Version { get; set; }

        [JsonProperty("addon_files")]
        public Dictionary<string, RemoteAddonFile> Files { get; set; }
    }

    public class RemoteAddonFile
    {
        [JsonProperty("file_path")]
        public string Path { get; set; }

        [JsonProperty("file_size")]
        public int Size { get; set; }

        [JsonProperty("file_hash")]
        public string Hash { get; set; }
    }
}

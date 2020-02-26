using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

namespace kellerkompanie_sync
{
    public class WebAddon
    {
        [JsonProperty("addon_foldername")]
        public string Foldername { get; set; }

        [JsonProperty("addon_group_id")]
        public int GroupId { get; set; }

        [JsonProperty("addon_id")]
        public int Id { get; set; }

        [JsonProperty("addon_name")]
        public string Name { get; set; }

        [JsonProperty("addon_uuid")]
        public string Uuid { get; set; }

        [JsonProperty("addon_version")]
        public string Version { get; set; }
    }

    public class WebAddonGroup : WebAddonGroupBase
    {
        [JsonProperty("addons")]
        public List<WebAddon> Addons { get; set; }
    }

    public class WebAddonGroupBase
    {
        [JsonProperty("addon_group_author")]
        public string Author { get; set; }

        [JsonProperty("addon_group_id")]
        public int Id { get; set; }

        [JsonProperty("addon_group_name")]
        public string Name { get; set; }

        [JsonProperty("addon_group_uuid")]
        public string Uuid { get; set; }

        [JsonProperty("addon_group_version")]
        public string Version { get; set; }
    }

    class WebAPI
    {
        private static readonly string IndexUrl = "http://server.kellerkompanie.com/repository/index.json";
        public static readonly string RepoUrl = "http://server.kellerkompanie.com/repository/mods";
        private static readonly string APIUrl = "https://server.kellerkompanie.com:5000/";
        private static readonly string AddonGroupsUrl = APIUrl + "addon_groups";
        private static readonly string AddonGroupUrl = APIUrl + "addon_group";
        private static readonly string AddonsUrl = APIUrl + "addons";

        public static List<WebAddonGroupBase> GetAddonGroups()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(AddonGroupsUrl);
                List<WebAddonGroupBase> addonGroups = JsonConvert.DeserializeObject<List<WebAddonGroupBase>>(json);
                return addonGroups;
            }
        }

        public static WebAddonGroup GetAddonGroup(WebAddonGroupBase addonGroupBase)
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(AddonGroupUrl + "/" + addonGroupBase.Uuid);
                WebAddonGroup addonGroup = JsonConvert.DeserializeObject<WebAddonGroup>(json);
                return addonGroup;
            }
        }

        public static List<WebAddon> GetAddons()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(AddonsUrl);
                List<WebAddon> addons = JsonConvert.DeserializeObject<List<WebAddon>>(json);
                return addons;
            }
        }

        public static RemoteIndex GetRemoteIndex()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(IndexUrl);
                Dictionary<string, RemoteAddon> map = JsonConvert.DeserializeObject<Dictionary<string, RemoteAddon>>(json);
                RemoteIndex index = new RemoteIndex(map);
                return index;
            }
        }

        public static void Download() 
        {
            List<WebAddonGroupBase> addonGroupBases = GetAddonGroups();
            foreach (WebAddonGroupBase addonGroupBase in addonGroupBases)
            {
                WebAddonGroup addonGroup = GetAddonGroup(addonGroupBase);
            }
        }

        internal static string LookUpAddonName(string addonName)
        {
            addonName = addonName.ToLower();
            List<WebAddon> webAddons = GetAddons();
            foreach (WebAddon webAddon in webAddons)
            {
                if (webAddon.Foldername.ToLower().Equals(addonName))
                {
                    return webAddon.Uuid;
                }
            }

            return null;
        }
    }
}

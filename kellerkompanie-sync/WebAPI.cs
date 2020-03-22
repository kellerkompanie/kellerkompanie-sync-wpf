using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace kellerkompanie_sync
{
    public class WebAddon
    {
        [JsonProperty("addon_foldername")]
        public string Foldername { get; set; }

        [JsonProperty("addon_id")]
        public int Id { get; set; }

        [JsonProperty("addon_name")]
        public string Name { get; set; }

        [JsonProperty("addon_uuid")]
        public string Uuid { get; set; }

        [JsonProperty("addon_version")]
        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            return obj is WebAddon addon && Id == addon.Id && Uuid == addon.Uuid;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Uuid);
        }
    }

    public class WebAddonGroup
    {
        [JsonProperty("addon_group_author")]
        public string Author { get; set; }

        [JsonProperty("addon_group_name")]
        public string Name { get; set; }

        [JsonProperty("addon_group_uuid")]
        public string Uuid { get; set; }

        [JsonProperty("addon_group_version")]
        public string Version { get; set; }

        [JsonProperty("addons")]
        public List<WebAddon> Addons { get; set; }
    }

    public class WebNews
    {
        [JsonProperty("news_id")]
        public int Id { get; set; }

        [JsonProperty("news_title")]
        public string Title { get; set; }

        [JsonProperty("news_content")]
        public string Content { get; set; }

        [JsonProperty("news_weblink")]
        public string Weblink { get; set; }

        [JsonProperty("news_timestamp")]
        public long Timestamp { get; set; }
    }

    public class WebEvent
    {
        [JsonProperty("event_title")]
        public string Title { get; set; }

        [JsonProperty("event_description")]
        public string Description { get; set; }

        [JsonProperty("event_timestamp")]
        public long Timestamp { get; set; }

        public string ExtractContent()
        {
            Regex regex = new Regex(@"(.*) - <a.*>(.*)<\/a>");
            Match match = regex.Match(Description);
            return match.Groups[1].ToString();
        }

        public string ExtractWeblink()
        {
            Regex regex = new Regex(@".*<a.*>(.*)<\/a>");
            Match match = regex.Match(Description);
            return match.Groups[1].ToString();
        }
    }

    class WebAPI
    {
        private static readonly string IndexUrl = "http://server.kellerkompanie.com/repository/index.json";
        public static readonly string RepoUrl = "http://server.kellerkompanie.com/repository/mods";
        private static readonly string NewsUrl = "https://kellerkompanie.com/news_json.php";
        private static readonly string CalendarUrl = "https://kellerkompanie.com/calendar_json.php";

        public static RemoteIndex GetIndex()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(IndexUrl);
                var jRoot = JObject.Parse(json);

                List<WebAddonGroup> addonGroups = new List<WebAddonGroup>();
                JToken jAddonGroups = jRoot["addon_groups"];
                foreach (JToken jAddonGroup in jAddonGroups)
                {
                    string addonGroupAuthor = (string)jAddonGroup["addon_group_author"];
                    string addonGroupName = (string)jAddonGroup["addon_group_name"];
                    string addonGroupUuid = (string)jAddonGroup["addon_group_uuid"];
                    string addonGroupVersion = (string)jAddonGroup["addon_group_version"];

                    List<WebAddon> addons = new List<WebAddon>();
                    var jAddons = jAddonGroup["addons"];
                    foreach (JToken jAddon in jAddons)
                    {
                        string addonFoldername = (string)jAddon["addon_foldername"];
                        string addonName = (string)jAddon["addon_name"];
                        string addonUuid = (string)jAddon["addon_uuid"];
                        string addonVersion = (string)jAddon["addon_version"];

                        WebAddon webAddon = new WebAddon()
                        {
                            Foldername = addonFoldername,
                            Name = addonName,
                            Uuid = addonUuid,
                            Version = addonVersion,
                        };
                        addons.Add(webAddon);
                    }

                    WebAddonGroup webAddonGroup = new WebAddonGroup
                    {
                        Author = addonGroupAuthor,
                        Name = addonGroupName,
                        Uuid = addonGroupUuid,
                        Version = addonGroupVersion,
                        Addons = addons,
                    };
                    addonGroups.Add(webAddonGroup);
                }

                Dictionary<string, RemoteAddon> filesIndex = new Dictionary<string, RemoteAddon>();
                JToken jFilesIndex = jRoot["files_index"];
                foreach (JToken jFileIndexTuple in jFilesIndex)
                {
                    JToken value = ((JProperty)jFileIndexTuple).Value;
                    string addonName = (string)value["addon_name"];
                    string addonUuid = (string)value["addon_uuid"];
                    string addonVersion = (string)value["addon_version"];

                    Dictionary<FilePath, RemoteAddonFile> files = new Dictionary<FilePath, RemoteAddonFile>();
                    var jAddonFiles = value["addon_files"];
                    foreach (JToken jAddonFile in jAddonFiles)
                    {
                        JToken jAddonFileValue = ((JProperty)jAddonFile).Value;
                        string addonFileHash = (string)jAddonFileValue["file_hash"];
                        FilePath addonFilePath = new FilePath { Value = (string)jAddonFileValue["file_path"] };
                        long addonFileSize = (long)jAddonFileValue["file_size"];

                        RemoteAddonFile remoteAddonFile = new RemoteAddonFile
                        {
                            Hash = addonFileHash,
                            Path = addonFilePath,
                            Size = addonFileSize,
                        };
                        files.Add(addonFilePath, remoteAddonFile);
                    }

                    RemoteAddon remoteAddon = new RemoteAddon
                    {
                        Uuid = addonUuid,
                        Name = addonName,
                        Version = addonVersion,
                        Files = files,
                    };
                    filesIndex.Add(addonName, remoteAddon);
                }

                // RemoteIndex index = JsonConvert.DeserializeObject<RemoteIndex>(json);
                return new RemoteIndex()
                {
                    AddonGroups = addonGroups,
                    FilesIndex = filesIndex
                };
            }
        }

        public static List<WebNews> GetNews()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(NewsUrl);
                List<WebNews> news = JsonConvert.DeserializeObject<List<WebNews>>(json);
                return news;
            }
        }

        public static List<WebEvent> GetEvents()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString(CalendarUrl);
                List<WebEvent> events = JsonConvert.DeserializeObject<List<WebEvent>>(json);
                return events;
            }
        }
    }
}

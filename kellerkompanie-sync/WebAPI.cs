using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;

namespace kellerkompanie_sync
{
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
        public DateTime Date { get; set; }
    }

    public class WebCalendar
    {
        [JsonProperty("all")]
        public List<WebEvent> All { get; set; }

        [JsonProperty("upcoming")]
        public List<WebEvent> Upcoming { get; set; }
    }

    public class WebEvent
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Description { get; set; }

        [JsonProperty("event_date")]
        public DateTime Date { get; set; }

        [JsonProperty("contact")]
        public string Contact { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }

        public string ExtractContent()
        {
            Regex regex = new Regex(@"(.*) - <a.*>(.*)<\/a>");
            Match match = regex.Match(Description);
            return match.Groups[1].ToString();
        }

        public string ExtractWeblink()
        {
            return Link;
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
            using WebClient wc = new();
            var json = wc.DownloadString(IndexUrl);
            var jRoot = JObject.Parse(json);

            Dictionary<Uuid, RemoteAddon> filesIndex = new Dictionary<Uuid, RemoteAddon>();
            JToken jFilesIndex = jRoot["files_index"];
            foreach (JToken jFileIndexTuple in jFilesIndex)
            {
                JToken value = ((JProperty)jFileIndexTuple).Value;
                string addonName = (string)value["name"];
                Uuid addonUuid = new Uuid((string)value["uuid"]);
                string addonVersion = (string)value["version"];

                Dictionary<FilePath, RemoteAddonFile> files = new Dictionary<FilePath, RemoteAddonFile>();
                var jAddonFiles = value["files"];
                foreach (JToken jAddonFile in jAddonFiles)
                {
                    JToken jAddonFileValue = ((JProperty)jAddonFile).Value;
                    string addonFileHash = (string)jAddonFileValue["hash"];
                    FilePath addonFilePath = new FilePath((string)jAddonFileValue["path"]);
                    long addonFileSize = (long)jAddonFileValue["size"];

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
                filesIndex.Add(addonUuid, remoteAddon);
            }

            List<AddonGroup> addonGroups = new List<AddonGroup>();
            JToken jAddonGroups = jRoot["addon_groups"];
            foreach (JToken jAddonGroup in jAddonGroups)
            {
                string addonGroupAuthor = (string)jAddonGroup["author"];
                string addonGroupName = (string)jAddonGroup["name"];
                string addonGroupUuid = (string)jAddonGroup["uuid"];
                string addonGroupVersion = (string)jAddonGroup["version"];

                List<RemoteAddon> addons = new List<RemoteAddon>();
                var jAddonUuids = jAddonGroup["addons"];
                foreach (JToken jAddonUuid in jAddonUuids)
                {
                    Uuid addonUuid = new Uuid((string)jAddonUuid);

                    Debug.Assert(filesIndex.ContainsKey(addonUuid));
                    RemoteAddon remoteAddon = filesIndex[addonUuid];
                    Debug.Assert(remoteAddon.Uuid.Equals(addonUuid));

                    addons.Add(remoteAddon);
                }

                AddonGroup addonGroup = new AddonGroup(addonGroupName, addonGroupAuthor, addonGroupUuid, addonGroupVersion, addons);
                addonGroups.Add(addonGroup);
            }

            return new RemoteIndex()
            {
                AddonGroups = addonGroups,
                FilesIndex = filesIndex
            };
        }

        public static List<WebNews> GetNews()
        {
            using WebClient wc = new();
            var json = wc.DownloadString(NewsUrl);
            List<WebNews> news = JsonConvert.DeserializeObject<List<WebNews>>(json);
            return news;
        }

        public static List<WebEvent> GetEvents()
        {
            using WebClient wc = new();
            var json = wc.DownloadString(CalendarUrl);
            WebCalendar webCalendar = JsonConvert.DeserializeObject<WebCalendar>(json);
            return webCalendar.Upcoming;
        }
    }
}

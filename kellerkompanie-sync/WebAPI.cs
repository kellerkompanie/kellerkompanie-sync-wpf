using Newtonsoft.Json;
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

        [JsonProperty("addon_group_id")]
        public int Id { get; set; }

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
                RemoteIndex index = JsonConvert.DeserializeObject<RemoteIndex>(json);
                return index;
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

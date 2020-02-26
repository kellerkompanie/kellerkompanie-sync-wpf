using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace kellerkompanie_sync_wpf
{
    public enum NewsType
    {
        News,
        Mission,
        Donation
    }

    public class News
    {
        public string uuid { get; set; }
        public NewsType newsType { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string weblink { get; set; }
        public long timestamp { get; set; }
    }

    public partial class NewsPage : Page
    {
        private readonly List<News> news = new List<News>();
        private const string LoremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

        public NewsPage()
        {
            InitializeComponent();

            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            news.Add(new News { uuid = "xxx", newsType = NewsType.News, title = "News Title", content = LoremIpsum, weblink = "http://kellerkompanie.com", timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });

            NewsListView.ItemsSource = news;
        }
    }
}

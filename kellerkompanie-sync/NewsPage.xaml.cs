using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace kellerkompanie_sync
{
    public class NewsItem
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public long Timestamp { get; set; }
        public string Weblink { get; set; }
        public string Icon { get; set; }
    }

    public partial class NewsPage : Page
    {
        private readonly List<NewsItem> news = new List<NewsItem>();
       
        public NewsPage()
        {
            InitializeComponent();

            foreach (WebNews webNews in WebAPI.GetNews()) {
                NewsItem newsItem = new NewsItem
                {
                    Title = webNews.Title,
                    Content = webNews.Content,
                    Timestamp = webNews.Timestamp,
                    Weblink = webNews.Weblink,
                    Icon = "/Images/news.png"
                };
                news.Add(newsItem);
            }

            foreach (WebEvent webEvent in WebAPI.GetEvents())
            {
                NewsItem newsItem = new NewsItem
                {
                    Title = webEvent.Title,
                    Content = webEvent.ExtractContent(),
                    Timestamp = webEvent.Timestamp,
                    Weblink = webEvent.ExtractWeblink(),
                    Icon = "/Images/event.png"
                };
                news.Add(newsItem);
            }

            news.Sort(delegate (NewsItem n1, NewsItem n2) { return n2.Timestamp.CompareTo(n1.Timestamp); });

            NewsListView.ItemsSource = news;
        }
        
        private void ListViewItem_Selected(object sender, RoutedEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null && item.IsSelected)
            {
                NewsItem newsItem = (NewsItem)item.DataContext;
                MainWindow.LaunchUri(newsItem.Weblink);
            }
        }
    }
}

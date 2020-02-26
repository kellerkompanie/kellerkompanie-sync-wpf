using kellerkompanie_sync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                NewsItem newsItem = new NewsItem();
                newsItem.Title = webNews.Title;
                newsItem.Content = webNews.Content;
                newsItem.Timestamp = webNews.Timestamp;
                newsItem.Weblink = webNews.Weblink;
                newsItem.Icon = "/Images/news.png";
                news.Add(newsItem);
            }

            foreach (WebEvent webEvent in WebAPI.GetEvents())
            {
                NewsItem newsItem = new NewsItem();
                newsItem.Title = webEvent.Title;
                newsItem.Content = webEvent.ExtractContent();
                newsItem.Timestamp = webEvent.Timestamp;
                newsItem.Weblink = webEvent.ExtractWeblink();
                newsItem.Icon = "/Images/event.png";
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

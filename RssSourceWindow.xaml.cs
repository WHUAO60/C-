using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NewsAggregator.Models;

namespace NewsAggregator
{
    public partial class RssSourceWindow : Window
    {
        public RssSourceWindow()
        {
            InitializeComponent();
            LoadSources();
            LvSources.SelectionChanged += LvSources_SelectionChanged;
        }

        private void LoadSources()
        {
            using (var db = new AppDbContext())
            {
                LvSources.ItemsSource = db.RssSources.OrderBy(s => s.Name).ToList();
            }
        }

        private void LvSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvSources.SelectedItem is RssSource selected)
            {
                TxtSourceName.Text = selected.Name;
                TxtSourceUrl.Text = selected.Url;
            }
        }

        private void BtnAddOrUpdate_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtSourceName.Text.Trim();
            string url = TxtSourceUrl.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("网站名称和 RSS 地址不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new AppDbContext())
            {
                if (LvSources.SelectedItem is RssSource selected)
                {
                    var sourceInDb = db.RssSources.Find(selected.Id);
                    if (sourceInDb != null) { sourceInDb.Name = name; sourceInDb.Url = url; }
                }
                else
                {
                    db.RssSources.Add(new RssSource { Name = name, Url = url, IsEnabled = true });
                }
                db.SaveChanges();
            }

            TxtSourceName.Clear();
            TxtSourceUrl.Clear();
            LoadSources();
        }

        private void ChkEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is RssSource source)
            {
                using (var db = new AppDbContext())
                {
                    var sourceInDb = db.RssSources.Find(source.Id);
                    if (sourceInDb != null) { sourceInDb.IsEnabled = cb.IsChecked ?? false; db.SaveChanges(); }
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LvSources.SelectedItem is RssSource selected)
            {
                if (MessageBox.Show($"确定删除 \"{selected.Name}\" 吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        var sourceInDb = db.RssSources.Find(selected.Id);
                        if (sourceInDb != null) { db.RssSources.Remove(sourceInDb); db.SaveChanges(); }
                    }
                    TxtSourceName.Clear();
                    TxtSourceUrl.Clear();
                    LoadSources();
                }
            }
            else
            {
                MessageBox.Show("请先选中一个订阅源。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
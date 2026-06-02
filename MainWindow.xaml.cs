// MainWindow.xaml.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.ViewModels;

namespace NewsAggregator
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly RssService _rssService;
        private System.Timers.Timer _autoTimer;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            _rssService = new RssService();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _viewModel.StatusText = "正在初始化...";

            await Task.Run(() =>
            {
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();

                    if (!db.RssSources.Any())
                    {
                        db.RssSources.AddRange(
                            new RssSource { Name = "博客园", Url = "https://www.cnblogs.com/rss", IsEnabled = true },
                            new RssSource { Name = "少数派", Url = "https://sspai.com/feed", IsEnabled = true },
                            new RssSource { Name = "阮一峰", Url = "http://www.ruanyifeng.com/blog/atom.xml", IsEnabled = true }
                        );
                        db.SaveChanges();
                    }
                }
            });

            await LoadFromDatabaseAsync();
            StartAutoTimer();
        }

        private void StartAutoTimer()
        {
            DateTime now = DateTime.Now;
            DateTime next8AM = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);
            if (now >= next8AM) next8AM = next8AM.AddDays(1);

            double msUntil8AM = (next8AM - now).TotalMilliseconds;

            _autoTimer = new System.Timers.Timer();
            _autoTimer.AutoReset = true;
            _autoTimer.Interval = msUntil8AM;
            _autoTimer.Elapsed += OnAutoTimerElapsed;
            _autoTimer.Start();
        }

        private void OnAutoTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Math.Abs(_autoTimer.Interval - (24 * 60 * 60 * 1000)) > 1000)
                _autoTimer.Interval = 24 * 60 * 60 * 1000;

            Dispatcher.Invoke(async () =>
            {
                _viewModel.StatusText = "⏰ 定时抓取触发中...";
                await RefreshArticlesAsync();
            });
        }

        private async Task LoadFromDatabaseAsync()
        {
            using (var db = new AppDbContext())
            {
                var articles = await db.Articles.OrderByDescending(a => a.FetchTime).ToListAsync();
                _viewModel.SetArticles(articles);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshArticlesAsync();
        }

        private async Task RefreshArticlesAsync()
        {
            if (BtnRefresh.IsEnabled == false) return;

            BtnRefresh.IsEnabled = false;
            BtnRefresh.Content = "⏳ 正在刷新...";
            this.Cursor = Cursors.Wait;
            _viewModel.StatusText = "正在抓取 RSS 源...";

            try
            {
                var fetchedArticles = await _rssService.FetchAllAsync();
                int newCount = 0;

                await Task.Run(() =>
                {
                    using (var db = new AppDbContext())
                    {
                        foreach (var article in fetchedArticles)
                        {
                            if (string.IsNullOrWhiteSpace(article.Link)) continue;
                            bool exists = db.Articles.Any(a => a.Link == article.Link);
                            if (!exists) { db.Articles.Add(article); newCount++; }
                        }
                        db.SaveChanges();
                    }
                });

                await LoadFromDatabaseAsync();
                _viewModel.StatusText = $"✅ 刷新完成，新增 {newCount} 篇";
            }
            catch (HttpRequestException ex)
            {
                _viewModel.StatusText = $"❌ 网络错误：{ex.Message}";
            }
            catch (Exception ex)
            {
                _viewModel.StatusText = $"❌ 错误：{ex.Message}";
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
                BtnRefresh.Content = "🔄 刷新文章";
                this.Cursor = Cursors.Arrow;
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchText = "";
            TxtSearch.Text = "";
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchText = "";
            _viewModel.MinReadCount = 0;
            TxtSearch.Text = "";
            TxtMinReadCount.Text = "";
        }

        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedArticle == null)
            {
                MessageBox.Show("请先在列表中选中一篇文章。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _viewModel.ToggleFavorite(_viewModel.SelectedArticle);
        }

        private void BtnExportOne_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportSelected(_viewModel.SelectedArticle);
        }

        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportAll();
        }

        private void BtnManageSources_Click(object sender, RoutedEventArgs e)
        {
            var window = new RssSourceWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnHelp_Click(object sender, MouseButtonEventArgs e)
        {
            var helpWindow = new HelpWindow();
            helpWindow.Owner = this;
            helpWindow.ShowDialog();
        }

        private void BtnRssMode_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsSearchMode = false;
            BtnRssMode.Style = (Style)FindResource("PrimaryButtonStyle");
            BtnSearchMode.Style = (Style)FindResource("SecondaryButtonStyle");
            TxtSearchKeyword.Visibility = Visibility.Collapsed;
            BtnSearchWeb.Visibility = Visibility.Collapsed;
            BtnRefresh.Visibility = Visibility.Visible;
        }

        private void BtnSearchMode_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.IsSearchMode = true;
            BtnSearchMode.Style = (Style)FindResource("PrimaryButtonStyle");
            BtnRssMode.Style = (Style)FindResource("SecondaryButtonStyle");
            TxtSearchKeyword.Visibility = Visibility.Visible;
            BtnSearchWeb.Visibility = Visibility.Visible;
            BtnRefresh.Visibility = Visibility.Collapsed;
        }

        private void BtnSearchWeb_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtSearchKeyword.Text.Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                MessageBox.Show("请输入搜索关键词。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string searchUrl = $"https://www.baidu.com/s?wd={Uri.EscapeDataString(keyword)}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = searchUrl,
                    UseShellExecute = true
                });
                _viewModel.StatusText = $"已在浏览器中打开百度搜索：{keyword}";
            }
            catch (Exception ex)
            {
                _viewModel.StatusText = $"打开浏览器失败：{ex.Message}";
                MessageBox.Show($"打开浏览器失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ArticleListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedArticle != null && !string.IsNullOrWhiteSpace(_viewModel.SelectedArticle.Link))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _viewModel.SelectedArticle.Link,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _viewModel.StatusText = $"❌ 无法打开链接：{ex.Message}";
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoTimer?.Stop();
            _autoTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
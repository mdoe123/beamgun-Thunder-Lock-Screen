using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using BeamgunApp.Models;

namespace BeamgunApp
{
    /// <summary>
    /// 攻击触发后的全屏锁定窗口：显示 sp.png 并等待输入正确密码解锁。
    /// </summary>
    public partial class LockScreenWindow : Window
    {
        private readonly PasswordStore _passwordStore;
        private readonly AttackLogger _logger;
        private readonly Action _onUnlocked;

        public LockScreenWindow(PasswordStore passwordStore, AttackLogger logger, Action onUnlocked)
        {
            InitializeComponent();
            _passwordStore = passwordStore;
            _logger = logger;
            _onUnlocked = onUnlocked;

            TimeText.Text = $"锁定时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            LoadImage();
            LoadLockText();
            Loaded += (s, e) => PasswordBox.Focus();
        }

        private void LoadImage()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sp.png");
            if (!File.Exists(path)) return;
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                LockImage.Source = bitmap;
            }
            catch
            {
                // 图片加载失败时保留纯黑背景。
            }
        }

        private void LoadLockText()
        {
            var lockText = new LockScreenText();
            TitleText.Text = lockText.Title;
            MessageText.Text = lockText.Message;
        }

        private void UnlockButton_Click(object sender, RoutedEventArgs e)
        {
            TryUnlock();
        }

        private void TryUnlock()
        {
            if (_passwordStore.Verify(PasswordBox.Password))
            {
                _logger.Log("全屏锁已解锁。");
                _onUnlocked?.Invoke();
                Close();
            }
            else
            {
                _logger.Log("全屏锁解锁失败：密码错误。");
                ErrorText.Text = "密码错误";
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // 拦截 Alt+F4，避免绕过全屏锁定窗口。
            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}

using System.Windows;
using BeamgunApp.Models;

namespace BeamgunApp
{
    /// <summary>
    /// 修改 Server酱 SendKey 的对话框，保存到 serverchan.json。
    /// </summary>
    public partial class ServerChanWindow : Window
    {
        private readonly ServerChanNotify _serverChan;

        public ServerChanWindow(ServerChanNotify serverChan)
        {
            InitializeComponent();
            _serverChan = serverChan;
            // 不回填已保存的 key（避免明文泄露），用户需手动输入新 key。
            Loaded += (s, e) => SendKeyBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var sendKey = SendKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(sendKey))
            {
                ErrorText.Text = "SendKey 不能为空";
                return;
            }

            _serverChan.SetSendKey(sendKey);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
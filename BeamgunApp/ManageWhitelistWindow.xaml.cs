using System.Windows;

namespace BeamgunApp
{
    /// <summary>
    /// 管理已授权设备（白名单）的窗口：列出 whitelist.cfg 中的设备，支持选中删除。
    /// </summary>
    public partial class ManageWhitelistWindow : Window
    {
        public ManageWhitelistWindow()
        {
            InitializeComponent();
            RefreshList();
        }

        private void RefreshList()
        {
            var devices = WhiteList.GetAll();
            DeviceList.ItemsSource = devices;
            HintText.Text = devices.Count == 0
                ? "当前没有已授权设备。"
                : $"已授权设备（共 {devices.Count} 个）：";
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = DeviceList.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                MessageBox.Show("请先选中要删除的设备。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"确定从白名单删除该设备吗？\n\n{selected}",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            if (WhiteList.Remove(selected))
            {
                RefreshList();
            }
            else
            {
                MessageBox.Show("删除失败，请检查文件权限。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

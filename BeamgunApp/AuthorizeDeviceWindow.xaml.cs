using System.Windows;

namespace BeamgunApp
{
    /// <summary>
    /// 设备授权对话框。解锁后弹出，询问用户是否授权当前插入的 USB 设备。
    /// 选择"授权"则加入白名单，选择"不授权"则安全弹出设备。
    /// </summary>
    public partial class AuthorizeDeviceWindow : Window
    {
        /// <summary>true=授权, false=不授权, null=关闭窗口未选择</summary>
        public bool? Result { get; private set; }

        public AuthorizeDeviceWindow(string deviceName, string deviceId)
        {
            InitializeComponent();
            DeviceNameText.Text = $"设备名称：{deviceName}";
            DeviceIdText.Text = $"设备ID：{deviceId}";
        }

        private void AuthorizeButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
        }
    }
}

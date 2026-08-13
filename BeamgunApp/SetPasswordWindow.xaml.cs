using System.Windows;
using BeamgunApp.Models;

namespace BeamgunApp
{
    /// <summary>
    /// 设置解锁密码的对话框，密码以 PBKDF2 加盐哈希形式保存到 password.txt。
    /// </summary>
    public partial class SetPasswordWindow : Window
    {
        private readonly PasswordStore _passwordStore;

        public SetPasswordWindow(PasswordStore passwordStore)
        {
            InitializeComponent();
            _passwordStore = passwordStore;
            Loaded += (s, e) => NewPasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var newPassword = NewPasswordBox.Password;
            var confirm = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(newPassword))
            {
                ErrorText.Text = "密码不能为空";
                return;
            }
            if (newPassword != confirm)
            {
                ErrorText.Text = "两次输入的密码不一致";
                return;
            }

            _passwordStore.SetPassword(newPassword);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

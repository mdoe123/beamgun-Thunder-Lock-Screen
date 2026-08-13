using System;
using System.Windows;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 以全屏 sp.png + 密码输入框的方式锁定工作站，替代系统锁屏。
    /// </summary>
    public class LockScreenLocker : ILocker
    {
        private readonly PasswordStore _passwordStore;
        private readonly AttackLogger _logger;
        private LockScreenWindow _window;
        private string _pendingDeviceId;
        private string _pendingDeviceName;

        /// <summary>用户成功输入正确密码解锁后的回调。</summary>
        public Action Unlocked;

        /// <summary>陌生设备触发锁定并成功解锁后的回调，参数为 (设备ID, 设备名称)。</summary>
        public Action<string, string> DeviceUnlocked;

        public LockScreenLocker(PasswordStore passwordStore, AttackLogger logger)
        {
            _passwordStore = passwordStore;
            _logger = logger;
        }

        public bool IsVisible => _window != null && _window.IsVisible;

        public bool Lock()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return false;
            dispatcher.Invoke((Action)ShowWindow);
            return true;
        }

        /// <summary>
        /// 以全屏锁定方式锁定工作站，并在解锁后触发设备授权流程。
        /// </summary>
        public bool LockWithDevice(string deviceId, string deviceName)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return false;
            dispatcher.Invoke((Action)(() =>
            {
                _pendingDeviceId = deviceId;
                _pendingDeviceName = deviceName;
                ShowWindow();
            }));
            return true;
        }

        public void Activate()
        {
            var dispatcher = Application.Current?.Dispatcher;
            dispatcher?.Invoke((Action)(() =>
            {
                if (_window == null) return;
                _window.Topmost = true;
                _window.Activate();
            }));
        }

        private void ShowWindow()
        {
            if (_window != null)
            {
                _window.Topmost = true;
                _window.Activate();
                return;
            }
            _window = new LockScreenWindow(_passwordStore, _logger, () => Unlocked?.Invoke());
            _window.Closed += (s, e) =>
            {
                _window = null;
                // 锁屏关闭后，如果有待授权设备，触发授权流程
                if (_pendingDeviceId != null)
                {
                    var id = _pendingDeviceId;
                    var name = _pendingDeviceName;
                    _pendingDeviceId = null;
                    _pendingDeviceName = null;
                    DeviceUnlocked?.Invoke(id, name);
                }
            };
            _window.Show();
        }
    }
}

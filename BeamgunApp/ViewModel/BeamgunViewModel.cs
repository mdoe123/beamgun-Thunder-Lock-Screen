using System;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Input;
using BeamgunApp.Commands;
using BeamgunApp.Models;

namespace BeamgunApp.ViewModel
{
    public interface IViewModel
    {
        bool IsVisible { get; set; }
        void DoStealFocus();
        void Reset();
        void DisableUntil(DateTime minutes);
        void ClearAlerts();
        void SetPassword();
        void ManageWhitelist();
    }

    public class BeamgunViewModel : IDisposable, IViewModel
    {
        public BeamgunState BeamgunState { get; }
        public ICommand DisableCommand { get; }
        public ICommand TrayIconCommand { get; }
        public ICommand LoseFocusCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ClearAlertsCommand { get; }
        public ICommand SetPasswordCommand { get; }
        public ICommand ManageWhitelistCommand { get; }
        public Action StealFocus { get; set; }

        public bool IsVisible
        {
            get
            {
                return BeamgunState.MainWindowVisibility == Visibility.Visible;
            }
            set
            {
                BeamgunState.MainWindowVisibility = value ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public BeamgunViewModel()
        {
            var dictionary = new RegistryBackedDictionary();
            var beamgunSettings = new BeamgunSettings(dictionary);
            BeamgunState = new BeamgunState(beamgunSettings)
            {
                MainWindowVisibility = Visibility.Hidden
            };
            // TODO: 这种双向关系不太好。
            dictionary.BadCastReport += BeamgunState.AppendToAlert;

            _attackLogger = new AttackLogger();
            _passwordStore = new PasswordStore();
            _passwordStore.ExternalChangeDetected += OnPasswordFileChanged;
            _deviceEjector = new DeviceEjector();
            _autoStartManager = new AutoStartManager();
            BeamgunState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(BeamgunState.StartWithWindows)) return;
                ApplyAutoStart(BeamgunState.StartWithWindows);
            };
            _lockScreen = new LockScreenLocker(_passwordStore, _attackLogger);
            _lockScreen.Unlocked += Reset;
            _lockScreen.DeviceUnlocked += OnDeviceUnlocked;
            _workstationLocker = new WorkstationLocker();

            BeamgunState.Disabler = new Disabler(BeamgunState);
            BeamgunState.Disabler.Enable();
            DisableCommand = new DisableCommand(this, beamgunSettings);
            TrayIconCommand = new TrayIconCommand(this);
            LoseFocusCommand = new DeactivatedCommand(this);
            ResetCommand = new ResetCommand(this);
            ExitCommand = new ExitCommand(this);
            ClearAlertsCommand = new ClearAlertsCommand(this);
            SetPasswordCommand = new SetPasswordCommand(this);
            ManageWhitelistCommand = new ManageWhitelistCommand(this);
            _keystrokeHooker = InstallKeystrokeHooker();
            _usbStorageGuard = InstallUsbStorageGuard(beamgunSettings);
            _alarm = InstallAlarm(beamgunSettings);
            _networkWatcher = new NetworkWatcher(beamgunSettings,
                new NetworkAdapterDisabler(),
                Report,
                x =>
                {
                    _attackLogger.Log("网络适配器攻击：" + x);
                    _alarm.Trigger(x);
                    BeamgunState.SetGraphicsLanAlert();
                },
                () => BeamgunState.Disabler.IsDisabled);
            _keyboardWatcher = new KeyboardWatcher(beamgunSettings,
                _lockScreen,
                Report,
                x =>
                {
                    _attackLogger.Log("键盘攻击：" + x);
                    _alarm.Trigger(x);
                    BeamgunState.SetGraphicsKeyboardAlert();
                },
                () => BeamgunState.Disabler.IsDisabled);
            _mouseWatcher = new MouseWatcher(beamgunSettings,
                _lockScreen,
                Report,
                x =>
                {
                    _attackLogger.Log("鼠标攻击：" + x);
                    _alarm.Trigger(x);
                    BeamgunState.SetGraphicsMouseAlert();
                },
                () => BeamgunState.Disabler.IsDisabled);
            _usbDeviceWatcher = new UsbDeviceWatcher(beamgunSettings,
                _lockScreen,
                Report,
                x =>
                {
                    _attackLogger.Log("陌生USB设备：" + x);
                    _alarm.Trigger(x);
                    BeamgunState.SetGraphicsKeyboardAlert();
                },
                () => BeamgunState.Disabler.IsDisabled);
            var checker = new VersionChecker();
            _updateTimer = new VersionCheckerTimer(beamgunSettings,
                checker,
                Report);
        }

        private void Report(string message)
        {
            BeamgunState.AppendToAlert(message);
            _attackLogger.Log(message);
        }

        private Alarm InstallAlarm(IBeamgunSettings beamgunSettings)
        {
            var alarm = new Alarm(beamgunSettings.StealFocusInterval, BeamgunState);
            alarm.AlarmCallback += () =>
            {
                if (_lockScreen.IsVisible)
                {
                    _lockScreen.Activate();
                }
                else
                {
                    BeamgunState.MainWindowState = WindowState.Normal;
                    BeamgunState.MainWindowVisibility = Visibility.Visible;
                    DoStealFocus();
                }
            };
            return alarm;
        }

        private UsbStorageGuard InstallUsbStorageGuard(IBeamgunSettings beamgunSettings)
        {
            var usbGuard = new UsbStorageGuard(beamgunSettings);
            BeamgunState.UsbMassStorageDisabled = usbGuard.UsbStorageDisabled;
            BeamgunState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(BeamgunState.UsbMassStorageDisabled)) return;
                if (!beamgunSettings.IsAdmin)
                {
                    BeamgunState.AppendToAlert("没有管理员权限无法更改 USB 大容量存储设置。");
                }
                try
                {
                    usbGuard.UsbStorageDisabled = BeamgunState.UsbMassStorageDisabled;
                }
                catch (PrivilegeNotHeldException e)
                {
                    BeamgunState.AppendToAlert($"权限异常：{e.Message}");
                }
            };
            return usbGuard;
        }

        private KeystrokeHooker InstallKeystrokeHooker()
        {
            var converter = new Models.KeyConverter();
            var keystrokeHooker = new KeystrokeHooker();
            keystrokeHooker.Callback += key =>
            {
                // 自定义锁屏显示期间，检测到敏感操作（Win 键、Alt+Tab、Alt+Esc 等）则触发系统锁屏。
                if (_lockScreen.IsVisible && IsSensitiveKey(key))
                {
                    _attackLogger.Log("锁屏期间检测到敏感操作，已触发系统锁屏。");
                    _workstationLocker.Lock();
                    return;
                }

                if (!_alarm.Triggered) return;
                BeamgunState.AppendToKeyLog(converter.Convert(key));
            };
            return keystrokeHooker;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VkMenu = 0x12;
        private const int VkControl = 0x11;

        /// <summary>
        /// 判断是否为需要触发系统锁屏的敏感操作按键。
        /// </summary>
        private bool IsSensitiveKey(System.Windows.Forms.Keys key)
        {
            if (key == System.Windows.Forms.Keys.LWin || key == System.Windows.Forms.Keys.RWin)
                return true;

            var altDown = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
            var ctrlDown = (GetAsyncKeyState(VkControl) & 0x8000) != 0;

            if (altDown && (key == System.Windows.Forms.Keys.Tab || key == System.Windows.Forms.Keys.Escape))
                return true;

            if (ctrlDown && key == System.Windows.Forms.Keys.Escape)
                return true;

            return false;
        }
        public void DoStealFocus()
        {
            StealFocus();
        }
        public void DisableUntil(DateTime time)
        {
            BeamgunState.Disabler.DisableUntil(time);
        }

        public void ClearAlerts()
        {
            BeamgunState.AlertLog = "";
            BeamgunState.AppendToAlert("日志已清空。");
        }

        public void SetPassword()
        {
            var window = new SetPasswordWindow(_passwordStore);
            window.ShowDialog();
            if (window.DialogResult == true)
            {
                BeamgunState.AppendToAlert("解锁密码已更新。");
                _attackLogger.Log("解锁密码已更新。");
            }
        }

        public void ManageWhitelist()
        {
            var window = new ManageWhitelistWindow();
            window.ShowDialog();
        }

        /// <summary>
        /// 根据开关状态创建或删除开机自启动计划任务。
        /// </summary>
        private void ApplyAutoStart(bool enabled)
        {
            try
            {
                if (enabled)
                    _autoStartManager.Enable();
                else
                    _autoStartManager.Disable();
                BeamgunState.AppendToAlert(enabled ? "已开启开机自启动。" : "已关闭开机自启动。");
            }
            catch (Exception ex)
            {
                BeamgunState.AppendToAlert($"开机自启动设置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 陌生设备触发锁定并成功解锁后，弹出授权对话框询问用户是否授权该设备。
        /// 授权则加入白名单，不授权则安全弹出设备。
        /// </summary>
        private void OnDeviceUnlocked(string deviceId, string deviceName)
        {
            var window = new AuthorizeDeviceWindow(deviceName, deviceId);
            window.ShowDialog();
            if (window.Result == true)
            {
                WhiteList.Add(deviceId);
                BeamgunState.AppendToAlert($"设备已授权（加入白名单）：{deviceName}");
                _attackLogger.Log($"设备已授权：{deviceId}");
            }
            else if (window.Result == false)
            {
                if (_deviceEjector.Eject(deviceId))
                {
                    BeamgunState.AppendToAlert($"设备已安全弹出：{deviceName}");
                    _attackLogger.Log($"设备已安全弹出：{deviceId}");
                }
                else
                {
                    BeamgunState.AppendToAlert($"无法安全弹出设备，请手动拔出：{deviceName}");
                    _attackLogger.Log($"无法安全弹出设备：{deviceId}");
                }
            }
        }

        /// <summary>
        /// 密码文件被外部进程改写时触发：记录日志，并在 UI 线程弹出安全告警。
        /// </summary>
        private void OnPasswordFileChanged(string message)
        {
            _attackLogger.Log(message);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                BeamgunState.AppendToAlert(message);
                MessageBox.Show(message, "安全警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        public void Dispose()
        {
            _keystrokeHooker?.Dispose();
            _updateTimer?.Dispose();
            _keyboardWatcher?.Dispose();
            _mouseWatcher?.Dispose();
            _networkWatcher?.Dispose();
            _usbDeviceWatcher?.Dispose();
            _usbStorageGuard?.Dispose();
            _passwordStore?.Dispose();
        }

        public void Reset()
        {
            BeamgunState.AppendToAlert("正在重置告警。");
            BeamgunState.Disabler.Enable();
            _alarm.Reset();
            _networkWatcher.Triggered = false;
        }

        private readonly KeystrokeHooker _keystrokeHooker;
        private readonly Alarm _alarm;
        private readonly NetworkWatcher _networkWatcher;
        private readonly UsbStorageGuard _usbStorageGuard;
        private readonly VersionCheckerTimer _updateTimer;
        private readonly KeyboardWatcher _keyboardWatcher;
        private readonly MouseWatcher _mouseWatcher;
        private readonly UsbDeviceWatcher _usbDeviceWatcher;
        private readonly AttackLogger _attackLogger;
        private readonly PasswordStore _passwordStore;
        private readonly LockScreenLocker _lockScreen;
        private readonly DeviceEjector _deviceEjector;
        private readonly AutoStartManager _autoStartManager;
        private readonly WorkstationLocker _workstationLocker;
    }
}

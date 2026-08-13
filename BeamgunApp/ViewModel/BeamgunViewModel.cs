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
            _deviceEjector = new DeviceEjector();
            _lockScreen = new LockScreenLocker(_passwordStore, _attackLogger);
            _lockScreen.Unlocked += Reset;
            _lockScreen.DeviceUnlocked += OnDeviceUnlocked;

            BeamgunState.Disabler = new Disabler(BeamgunState);
            BeamgunState.Disabler.Enable();
            DisableCommand = new DisableCommand(this, beamgunSettings);
            TrayIconCommand = new TrayIconCommand(this);
            LoseFocusCommand = new DeactivatedCommand(this);
            ResetCommand = new ResetCommand(this);
            ExitCommand = new ExitCommand(this);
            ClearAlertsCommand = new ClearAlertsCommand(this);
            SetPasswordCommand = new SetPasswordCommand(this);
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
                if (!_alarm.Triggered) return;
                BeamgunState.AppendToKeyLog(converter.Convert(key));
            };
            return keystrokeHooker;
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

        public void Dispose()
        {
            _keystrokeHooker?.Dispose();
            _updateTimer?.Dispose();
            _keyboardWatcher?.Dispose();
            _mouseWatcher?.Dispose();
            _networkWatcher?.Dispose();
            _usbDeviceWatcher?.Dispose();
            _usbStorageGuard?.Dispose();
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
    }
}

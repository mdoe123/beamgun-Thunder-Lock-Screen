using System;
using System.Management;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 监听所有 USB PnP 设备插入事件。
    /// 排除键盘、鼠标、网络适配器（由其他 Watcher 处理），
    /// 对陌生设备触发全屏锁定，解锁后询问是否授权。
    /// </summary>
    public class UsbDeviceWatcher : IDisposable
    {
        private readonly ManagementEventWatcher _watcher;

        // 设备类 GUID，用于排除已被其他 Watcher 处理的设备
        private static readonly string KeyboardClassGuid = "{4d36e96b-e325-11ce-bfc1-08002be10318}";
        private static readonly string MouseClassGuid = "{4d36e96f-e325-11ce-bfc1-08002be10318}";
        private static readonly string NetClassGuid = "{4d36e972-e325-11ce-bfc1-08002be10318}";

        public UsbDeviceWatcher(IBeamgunSettings settings, LockScreenLocker locker,
            Action<string> report, Action<string> alarm, Func<bool> disabled)
        {
            var query = new WqlEventQuery("__InstanceCreationEvent",
                new TimeSpan(0, 0, 1),
                "TargetInstance isa \"Win32_PnPEntity\"");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += (caller, args) =>
            {
                if (disabled()) return;
                if (!settings.LockOnUsbDevice) return;

                var obj = (ManagementBaseObject)args.NewEvent["TargetInstance"];
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString();
                if (string.IsNullOrEmpty(pnpDeviceId)) return;

                // 仅监控 USB 设备
                if (!pnpDeviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                    return;

                // 排除键盘/鼠标/网络适配器（由其他 Watcher 处理）
                var classGuid = obj["ClassGuid"]?.ToString();
                if (!string.IsNullOrEmpty(classGuid))
                {
                    if (string.Equals(classGuid, KeyboardClassGuid, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(classGuid, MouseClassGuid, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(classGuid, NetClassGuid, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                // 白名单设备跳过
                if (WhiteList.WhiteListed(obj))
                {
                    report($"设备在白名单中：{pnpDeviceId}");
                    return;
                }

                var deviceName = obj["Name"]?.ToString() ?? "未知设备";
                alarm($"陌生USB设备插入告警：{deviceName}（{pnpDeviceId}）");
                locker.LockWithDevice(pnpDeviceId, deviceName);
            };
            _watcher.Start();
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}

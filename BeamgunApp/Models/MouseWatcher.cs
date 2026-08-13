using System;
using System.Management;

namespace BeamgunApp.Models
{
    public class MouseWatcher : IDisposable
    {
        private readonly ManagementEventWatcher _watcher;

        public MouseWatcher(IBeamgunSettings settings, ILocker locker, Action<string> report, Action<string> alarm, Func<bool> disabled)
        {
            var MouseQuery = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa \"Win32_PointingDevice\"");
            _watcher = new ManagementEventWatcher(MouseQuery);
            _watcher.EventArrived += (caller, args) =>
            {
                if (disabled()) return;
                var obj = (ManagementBaseObject)args.NewEvent["TargetInstance"];
                alarm($"鼠标插入告警：" +
                                   $"{obj["Name"]} " +
                                   $"{obj["Caption"]} " +
                                   $"{obj["Description"]} " +
                                   $"{obj["DeviceID"]}" +
                                   $"{obj["Manufacturer"]} " +
                                   $"{obj["PNPDeviceID"]}。");
                if (!settings.LockOnMouse) return;
                if (WhiteList.WhiteListed(obj))
                {
                    report($"设备在白名单中，如果改变主意请从 {WhiteList.WhiteFilename} 中移除 {obj["PNPDeviceID"]}。");
                    return;
                }
                report(locker.Lock()
                    ? "已成功锁定工作站。"
                    : "无法锁定工作站。");

            };
            _watcher.Start();
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}

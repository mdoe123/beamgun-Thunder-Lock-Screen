using System;
using System.Management;
using System.Threading;

namespace BeamgunApp.Models
{
    public class NetworkWatcher : IDisposable
    {
        public bool Triggered { get; set; }
        private readonly ManagementEventWatcher _watcher;
        
        public NetworkWatcher(IBeamgunSettings settings, NetworkAdapterDisabler networkAdapterDisabler, Action<string> report, Action<string> alarm, Func<bool> disabled)
        {
            var networkQuery = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa \"Win32_NetworkAdapter\"");
            _watcher = new ManagementEventWatcher(networkQuery);
            _watcher.EventArrived += (caller, args) =>
            {
                var obj = (ManagementBaseObject)args.NewEvent["TargetInstance"];
                var alertMessage = $"网络适配器插入告警：{obj["Description"]}（设备 ID {obj["DeviceID"]}）";
                if (disabled()) return;
                alarm(alertMessage);
                Triggered = settings.DisableNetworkAdapter;
                if (Triggered)
                {
                    report($"每 {settings.DisableNetworkAdapterInterval} 毫秒禁用一次 {obj["Description"]}，直到重置。");
                }
                while (Triggered)
                {
                    try
                    {
                        if (!networkAdapterDisabler.Disable(obj["DeviceID"].ToString()))
                        {
                            report($"危险：无法禁用 {obj["AdapterType"]}！");
                        }
                        Thread.Sleep((int) settings.DisableNetworkAdapterInterval);
                    }
                    catch (NetworkAdapterDisablerException e)
                    {
                        report(e.Message);
                    }
                }
            };
            _watcher.Start();
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}

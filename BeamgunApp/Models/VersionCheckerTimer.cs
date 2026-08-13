using System;
using System.Reflection;
using System.Threading;

namespace BeamgunApp.Models
{
    public class VersionCheckerTimer : IDisposable
    {
        private readonly Timer _timer;

        public VersionCheckerTimer(IBeamgunSettings beamgunSettings, VersionChecker checker, Action<string> report)
        {
            var autoEvent = new AutoResetEvent(false);
            _timer = new Timer(state =>
            {
                lock (_timer)
                {
                    try
                    {
                        checker.Update(beamgunSettings);
                        var availableVersion = beamgunSettings.LatestVersion;
                        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                        report(availableVersion > currentVersion
                                ? $"版本 {availableVersion} 可从 {beamgunSettings.DownloadUrl} 下载"
                                : $"Beamgun 已是最新版本。");
                    }
                    catch (Exception e)
                    {
                        report($"无法连接更新服务器。{e.Message}");
                    }
                }
            }, autoEvent, 0, beamgunSettings.UpdatePollInterval);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}

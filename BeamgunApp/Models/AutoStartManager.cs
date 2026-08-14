using System.Diagnostics;
using System.Reflection;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 通过 Windows 计划任务实现开机自启动（当前用户登录时以最高权限运行）。
    /// </summary>
    public class AutoStartManager
    {
        private const string TaskName = "Beamgun";

        public bool IsEnabled => RunSchTasks($"/Query /TN \"{TaskName}\"") == 0;

        public void Enable()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            // /TR 的程序路径用内层转义引号包裹，以支持含空格的路径。
            var arguments = "/Create /TN \"" + TaskName + "\" /TR \"\\\"" + exePath + "\\\"\" /SC ONLOGON /RL HIGHEST /F";
            var exitCode = RunSchTasks(arguments);
            if (exitCode != 0)
                throw new System.ComponentModel.Win32Exception(exitCode, "schtasks /Create 失败。");
        }

        public void Disable()
        {
            var exitCode = RunSchTasks($"/Delete /TN \"{TaskName}\" /F");
            // 任务不存在时 schtasks /Delete 返回非 0，忽略该情况。
            if (exitCode != 0 && TaskExists())
                throw new System.ComponentModel.Win32Exception(exitCode, "schtasks /Delete 失败。");
        }

        private bool TaskExists()
        {
            return RunSchTasks($"/Query /TN \"{TaskName}\"") == 0;
        }

        private static int RunSchTasks(string arguments)
        {
            var psi = new ProcessStartInfo("schtasks", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}

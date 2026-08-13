using System.Runtime.InteropServices;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 工作站锁定接口。
    /// </summary>
    public interface ILocker
    {
        bool Lock();
    }

    /// <summary>
    /// 通过调用 Windows API LockWorkStation 锁定工作站的实现。
    /// </summary>
    public class WorkstationLocker : ILocker
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        public bool Lock()
        {
            return LockWorkStation();
        }
    }
}

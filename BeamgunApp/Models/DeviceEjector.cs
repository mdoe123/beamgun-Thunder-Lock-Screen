using System.Runtime.InteropServices;
using System.Text;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 通过 Windows SetupDi API 安全弹出 USB 设备。
    /// </summary>
    public class DeviceEjector
    {
        private const uint CR_SUCCESS = 0;
        private const uint CM_LOCATE_DEVNODE_NORMAL = 0;

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint CM_Request_Device_EjectW(uint dnDevInst, out uint pVetoType,
            StringBuilder pszVetoName, uint ulNameLength, uint ulFlags);

        /// <summary>
        /// 尝试安全弹出指定设备。
        /// </summary>
        /// <param name="pnpDeviceId">设备的 PNPDeviceID</param>
        /// <returns>true 表示弹出成功；false 表示弹出失败</returns>
        public bool Eject(string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId)) return false;

            uint devInst;
            if (CM_Locate_DevNodeW(out devInst, pnpDeviceId, CM_LOCATE_DEVNODE_NORMAL) != CR_SUCCESS)
                return false;

            var vetoName = new StringBuilder(512);
            var result = CM_Request_Device_EjectW(devInst, out var vetoType, vetoName, 512, 0);
            return result == CR_SUCCESS && vetoType == 0;
        }
    }
}

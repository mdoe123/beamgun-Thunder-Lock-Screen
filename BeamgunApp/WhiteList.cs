using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace BeamgunApp
{
    internal static class WhiteList
    {
        internal const string WhiteFilename = "./whitelist.cfg";

        internal static List<string> GetAll()
        {
            var whitelist = new List<string>();
            try
            {
                if (File.Exists(WhiteFilename))
                {
                    whitelist = File.ReadAllLines(WhiteFilename, System.Text.Encoding.UTF8)
                        .Select(l => l.Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"读取 {WhiteFilename} 失败：{e.Message}。");
            }
            return whitelist;
        }

        internal static bool WhiteListed(ManagementBaseObject obj)
        {
            var id = obj?["PNPDeviceID"] as string;
            if (string.IsNullOrEmpty(id)) return false;
            return GetAll().Contains(id);
        }

        internal static void Add(string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId)) return;
            if (GetAll().Contains(pnpDeviceId)) return;
            try
            {
                File.AppendAllText(WhiteFilename, pnpDeviceId + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"写入 {WhiteFilename} 失败：{e.Message}。");
            }
        }

        internal static bool Remove(string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId)) return false;
            try
            {
                var remaining = GetAll().Where(id => id != pnpDeviceId).ToList();
                File.WriteAllLines(WhiteFilename, remaining, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"写入 {WhiteFilename} 失败：{e.Message}。");
                return false;
            }
        }
    }
}

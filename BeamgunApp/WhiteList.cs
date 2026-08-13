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

        internal static bool WhiteListed(ManagementBaseObject obj)
        {
            var result = false;
            var whitelist = new List<string>();
            
            try
            {
                var exists = File.Exists(WhiteFilename);
                if (exists)
                {
                    var lines = File.ReadAllLines(WhiteFilename, System.Text.Encoding.UTF8);
                    whitelist = lines.ToList();
                }
            }
            catch (Exception e) {
                System.Console.WriteLine($"读取 {WhiteFilename} 失败：{e.Message}。");
            }

            if (whitelist.Contains(obj["PNPDeviceID"]))
            {
                result = true;
            }

            return result;
        }

        internal static void Add(string pnpDeviceId)
        {
            try
            {
                File.AppendAllText(WhiteFilename, pnpDeviceId + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"写入 {WhiteFilename} 失败：{e.Message}。");
            }
        }
    }
}

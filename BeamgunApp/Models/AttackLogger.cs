using System;
using System.IO;
using System.Text;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 将攻击事件追加写入程序目录下的 beamgun.log 文件。
    /// </summary>
    public class AttackLogger
    {
        public const string LogFilename = "beamgun.log";

        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFilename);

        private readonly object _lock = new object();

        /// <summary>可选的 Server酱 推送器，写入日志后同步推送到手机。</summary>
        public ServerChanNotify Notifier { get; set; }

        public void Log(string message)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志写入失败不应中断告警流程。
            }

            // 所有日志（含解锁、授权、密码更改等）都推送到手机。
            Notifier?.Notify(line);
        }
    }
}

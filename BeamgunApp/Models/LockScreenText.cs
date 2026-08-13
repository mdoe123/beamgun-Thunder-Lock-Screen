using System;
using System.IO;
using System.Text;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 读取锁屏窗口的自定义文本。配置文件 locktext.txt 位于程序目录下，
    /// 第 1 行为主标题、第 2 行为提示语；某一行缺失或为空时该行使用默认文案。
    /// </summary>
    public class LockScreenText
    {
        public const string LockTextFilename = "locktext.txt";

        private const string DefaultTitle = "工作站已被 Beamgun 锁定";
        private const string DefaultMessage = "检测到未授权设备插入，请输入解锁密码";

        public string Title { get; }
        public string Message { get; }

        public LockScreenText()
        {
            Title = DefaultTitle;
            Message = DefaultMessage;

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LockTextFilename);
            if (!File.Exists(path)) return;

            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length >= 1 && !string.IsNullOrWhiteSpace(lines[0]))
                    Title = lines[0].Trim();
                if (lines.Length >= 2 && !string.IsNullOrWhiteSpace(lines[1]))
                    Message = lines[1].Trim();
            }
            catch
            {
                // 读取失败时使用默认文案。
            }
        }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BeamgunApp.Models
{
    /// <summary>
    /// Server酱 配置（写入程序目录下的 serverchan.json）。
    /// </summary>
    public class ServerChanConfig
    {
        public bool Enabled { get; set; }
        public string SendKey { get; set; }
    }

    /// <summary>
    /// 通过 Server酱 把日志发送到手机。
    /// 开关与 SendKey 保存在程序目录下的 serverchan.json 中。
    /// </summary>
    public class ServerChanNotify
    {
        public const string ConfigFilename = "serverchan.json";

        private const string DefaultSendKey = "SCT401341Tze7PsnxTmQaILZP9ZUTYP3Ez";
        private const string ApiBase = "https://sctapi.ftqq.com/";
        private const string LogTitle = "Beamgun 日志提醒";

        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFilename);

        private readonly HttpClient _client;
        private ServerChanConfig _config;
        private DateTime _lastSend = DateTime.MinValue;

        /// <summary>当前是否启用推送。</summary>
        public bool Enabled => _config.Enabled;

        /// <summary>当前配置的 SendKey。</summary>
        public string SendKey => _config.SendKey;

        public ServerChanNotify()
        {
            _client = new HttpClient();
            _config = LoadConfig() ?? new ServerChanConfig { Enabled = false, SendKey = DefaultSendKey };
        }

        /// <summary>
        /// 设置推送开关并写回配置文件。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (_config.Enabled == enabled) return;
            _config.Enabled = enabled;
            SaveConfig();
        }

        /// <summary>
        /// 设置 SendKey 并写回配置文件。
        /// </summary>
        public void SetSendKey(string sendKey)
        {
            _config.SendKey = sendKey ?? string.Empty;
            SaveConfig();
        }

        /// <summary>
        /// 异步发送一条日志到手机。未开启推送、未配置 SendKey 或距上次发送不足 1 秒时忽略。
        /// </summary>
        public async void Notify(string message)
        {
            if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.SendKey)) return;
            if ((DateTime.Now - _lastSend).TotalSeconds < 1) return;

            try
            {
                var url = ApiBase + Uri.EscapeDataString(_config.SendKey) + ".send" +
                          "?title=" + Uri.EscapeDataString(LogTitle) +
                          "&desp=" + Uri.EscapeDataString(message);
                var response = await _client.GetAsync(url);
                response.Dispose();
                _lastSend = DateTime.Now;
            }
            catch
            {
                // 推送失败不应干扰主流程。
            }
        }

        private static ServerChanConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                return JsonConvert.DeserializeObject<ServerChanConfig>(File.ReadAllText(ConfigPath));
            }
            catch
            {
                return null;
            }
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch
            {
                // 写配置失败不阻塞界面。
            }
        }
    }
}
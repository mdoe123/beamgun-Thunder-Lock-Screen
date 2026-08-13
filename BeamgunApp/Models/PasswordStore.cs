using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BeamgunApp.Models
{
    /// <summary>
    /// 负责读写解锁密码。密码以 PBKDF2 加盐哈希的形式保存在程序目录下的 password.txt 中，
    /// 存储格式为 {迭代次数}:{盐(Base64)}:{哈希(Base64)}。同时兼容旧版 MD5 格式的读取。
    /// </summary>
    public class PasswordStore
    {
        public const string PasswordFilename = "password.txt";

        // 当 password.txt 尚不存在时使用的默认解锁密码。
        private const string DefaultPassword = "beamgun";
        private const int Iterations = 10000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private static readonly string PasswordPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PasswordFilename);

        private readonly object _watchLock = new object();
        private string _knownHash;
        private bool _suppressWatch;
        private FileSystemWatcher _watcher;

        /// <summary>
        /// 当 password.txt 被本程序之外的进程改写（创建/修改/删除）时触发，参数为告警消息。
        /// </summary>
        public event Action<string> ExternalChangeDetected;

        public PasswordStore()
        {
            _knownHash = ComputeFileHashOrNull();
            StartWatching();
        }

        public bool IsPasswordSet => File.Exists(PasswordPath);

        public bool Verify(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return false;

            if (!IsPasswordSet)
                return string.Equals(plain, DefaultPassword, StringComparison.Ordinal);

            var stored = ReadStored();
            if (stored == null)
                return string.Equals(plain, DefaultPassword, StringComparison.Ordinal);

            // 兼容旧版无盐 MD5 格式（32 位十六进制），验证通过后可重新设置密码以升级为 PBKDF2。
            if (stored.Length == 32 && IsHex(stored))
                return string.Equals(Md5(plain), stored, StringComparison.OrdinalIgnoreCase);

            return VerifyPbkdf2(plain, stored);
        }

        public void SetPassword(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                throw new ArgumentException("密码不能为空。", nameof(plain));

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            var hash = Pbkdf2(plain, salt, Iterations);
            var line = $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";

            // 写入期间抑制文件监控，避免把本程序自身的写入误判为外部篡改。
            lock (_watchLock)
            {
                _suppressWatch = true;
                try
                {
                    File.WriteAllText(PasswordPath, line, Encoding.ASCII);
                    _knownHash = HashBytes(Encoding.ASCII.GetBytes(line));
                }
                finally
                {
                    _suppressWatch = false;
                }
            }
        }

        private static string ReadStored()
        {
            try
            {
                return File.ReadAllText(PasswordPath).Trim();
            }
            catch
            {
                return null;
            }
        }

        private static bool VerifyPbkdf2(string plain, string stored)
        {
            var parts = stored.Split(':');
            if (parts.Length != 3) return false;

            int iterations;
            byte[] salt;
            byte[] expected;
            try
            {
                iterations = int.Parse(parts[0]);
                salt = Convert.FromBase64String(parts[1]);
                expected = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return false;
            }

            var computed = Pbkdf2(plain, salt, iterations);
            return FixedTimeEquals(computed, expected);
        }

        private static byte[] Pbkdf2(string plain, byte[] salt, int iterations)
        {
            using (var derive = new Rfc2898DeriveBytes(plain, salt, iterations))
            {
                return derive.GetBytes(HashSize);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static bool IsHex(string s)
        {
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }

        private static string Md5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder();
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private void StartWatching()
        {
            try
            {
                var dir = Path.GetDirectoryName(PasswordPath);
                if (string.IsNullOrEmpty(dir)) return;

                _watcher = new FileSystemWatcher(dir, PasswordFilename)
                {
                    NotifyFilter = NotifyFilters.LastWrite |
                                   NotifyFilters.Size |
                                   NotifyFilters.FileName |
                                   NotifyFilters.CreationTime
                };
                _watcher.Changed += OnPasswordFileChanged;
                _watcher.Created += OnPasswordFileChanged;
                _watcher.Deleted += OnPasswordFileChanged;
                _watcher.Renamed += OnPasswordFileRenamed;
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                // 无法监控密码文件时静默跳过，不影响加解锁功能。
            }
        }

        private void OnPasswordFileChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileChange();
        }

        private void OnPasswordFileRenamed(object sender, RenamedEventArgs e)
        {
            HandleFileChange();
        }

        private void HandleFileChange()
        {
            lock (_watchLock)
            {
                if (_suppressWatch) return;

                var currentHash = ComputeFileHashOrNull();

                // 与基线比较；不一致说明被外部进程改动，告警并更新基线以避免重复提示。
                if (!string.Equals(currentHash, _knownHash, StringComparison.Ordinal))
                {
                    _knownHash = currentHash;
                    var message = currentHash == null
                        ? "密码文件 password.txt 已被外部程序删除，可能被篡改。请重新设置密码。"
                        : "密码文件 password.txt 已被外部程序修改，可能被篡改。请立即检查并重新设置密码。";
                    ExternalChangeDetected?.Invoke(message);
                }
            }
        }

        private static string ComputeFileHashOrNull()
        {
            try
            {
                if (!File.Exists(PasswordPath)) return null;
                return HashBytes(File.ReadAllBytes(PasswordPath));
            }
            catch
            {
                return null;
            }
        }

        private static string HashBytes(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(data));
            }
        }

        public void Dispose()
        {
            if (_watcher == null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}

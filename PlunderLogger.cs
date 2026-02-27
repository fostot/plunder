using System;
using System.IO;
using System.Reflection;
using TerrariaModder.Core.Logging;

namespace Plunder
{
    /// <summary>
    /// Wrapper logger that writes to both the TerrariaModder platform logger
    /// AND a dedicated Plunder log file at plunder/logs/plunder.log.
    /// </summary>
    public class PlunderLogger : ILogger
    {
        private readonly ILogger _inner;
        private readonly string _logFilePath;
        private readonly object _fileLock = new object();

        private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB
        private const int MaxBackups = 3;

        public LogLevel MinLevel
        {
            get => _inner.MinLevel;
            set => _inner.MinLevel = value;
        }

        public string ModId => _inner.ModId;

        public PlunderLogger(ILogger inner)
        {
            _inner = inner;

            // Determine log path: same folder as Plunder.dll / logs / plunder.log
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var logsDir = Path.Combine(dllDir, "logs");

            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);

            _logFilePath = Path.Combine(logsDir, "plunder.log");
        }

        public void Debug(string message)
        {
            _inner.Debug(message);
            WriteToFile("DEBUG", message);
        }

        public void Info(string message)
        {
            _inner.Info(message);
            WriteToFile("INFO ", message);
        }

        public void Warn(string message)
        {
            _inner.Warn(message);
            WriteToFile("WARN ", message);
        }

        public void Error(string message)
        {
            _inner.Error(message);
            WriteToFile("ERROR", message);
        }

        public void Error(string message, Exception ex)
        {
            _inner.Error(message, ex);
            WriteToFile("ERROR", $"{message}\n  {ex}");
        }

        private void WriteToFile(string level, string message)
        {
            try
            {
                lock (_fileLock)
                {
                    RotateIfNeeded();
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var line = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, line);
                }
            }
            catch
            {
                // Don't let file logging failures crash the mod
            }
        }

        private void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(_logFilePath)) return;

                var info = new FileInfo(_logFilePath);
                if (info.Length < MaxFileSize) return;

                var dir = Path.GetDirectoryName(_logFilePath);
                var name = Path.GetFileNameWithoutExtension(_logFilePath);
                var ext = Path.GetExtension(_logFilePath);

                // Shift existing backups: .3 → delete, .2 → .3, .1 → .2
                for (int i = MaxBackups; i >= 1; i--)
                {
                    var src = Path.Combine(dir, $"{name}.{i}{ext}");
                    var dst = Path.Combine(dir, $"{name}.{i + 1}{ext}");

                    if (i == MaxBackups && File.Exists(src))
                        File.Delete(src);
                    else if (File.Exists(src))
                        File.Move(src, dst);
                }

                // Current → .1
                File.Move(_logFilePath, Path.Combine(dir, $"{name}.1{ext}"));
            }
            catch
            {
                // Rotation failure is non-fatal
            }
        }
    }
}

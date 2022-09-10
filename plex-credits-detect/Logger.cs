using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect
{
    public enum LogVerbosity
    {
        None = 0,
        Exceptions,
        Full
    }
    public enum LogType
    {
        INFO,
        DEBUG,
        ERROR
    }

    public class Logger : TextWriterTraceListener, IDisposable
    {
        public static Logger log => _lazyLog.Value;

        private LoggerListener _listener = null;

        private readonly int _logChunkSize = 1048576;
        private readonly int _logChunkMaxCount = 5;
        private readonly int _logArchiveMaxCount = 10;
        private readonly int _logCleanupPeriod = 180;
        private const string LogFolderName = "logs";
        private const string LogFileName = "plex-credits-detect.log";
        private static string logFolderPath = "";
        private static string logFilePath = "";

        private static readonly Lazy<Logger> _lazyLog = new Lazy<Logger>(() => {
            return new Logger();
        });

        public class LoggerListener : TextWriterTraceListener
        {
            Logger _logger;

            public LoggerListener(Logger logger) : base(logFilePath)
            {
                _logger = logger;
            }

            public override void WriteLine(string message)
            {
                base.WriteLine(message);
                Flush();
                _logger.Rotate();
            }
        }


        public Logger()
        {
            logFolderPath = Path.Combine(Settings.globalSettingsPath, LogFolderName);
            if (!Directory.Exists(logFolderPath))
                Directory.CreateDirectory(logFolderPath);

            logFilePath = Path.Combine(logFolderPath, LogFileName);

            CreateListener();
        }

        public void CloseListener()
        {
            if (_listener != null)
            {
                try
                {
                    _listener.Flush();
                    _listener.Close();
                }
                catch { }
                _listener = null;
            }
        }

        private void CreateListener()
        {
            CloseListener();

            _listener = new LoggerListener(this);

            Trace.Listeners.Add(_listener);
        }

        public void Info(string message)
        {
            LogLine(message, LogType.INFO);
        }

        public void Debug(string message)
        {
            LogLine(message, LogType.DEBUG);
        }

        public void Error(string message, Exception ex = null)
        {
            if (ex == null)
            {
                LogLine(message, LogType.ERROR);
            }
            else
            {
                LogLine($"{message}\n${UnwrapExceptionMessages(ex)}", LogType.ERROR);
            }
        }

        public void Error(Exception e)
        {
            LogLine(UnwrapExceptionMessages(e), LogType.ERROR);
        }



        protected virtual string ComposeLogRow(string message, LogType logType) =>
            $"[{DateTime.Now.ToString(CultureInfo.InvariantCulture)} {logType}] - {message}";

        protected virtual string UnwrapExceptionMessages(Exception ex)
        {
            if (ex == null)
                return string.Empty;

            return $"{ex}, Inner exception: {UnwrapExceptionMessages(ex.InnerException)} ";
        }


        private void LogLine(string message, LogType logType)
        {
            if (string.IsNullOrEmpty(message))
            {
                Trace.WriteLine("");
                return;
            }

            var logRow = ComposeLogRow(message, logType);
            //System.Diagnostics.Debug.WriteLine(logRow);

            if (logType == LogType.ERROR)
            {
                Trace.WriteLine(logRow);
                //Trace.TraceError(logRow);
            }
            else if (logType == LogType.INFO)
            {
                Trace.WriteLine(logRow);
                //Trace.TraceInformation(logRow);
            }
            else if (logType == LogType.DEBUG)
            {
                _listener.WriteLine(logRow);
            }
        }

        private void Rotate()
        {
            if (!File.Exists(logFilePath))
                return;

            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Length < _logChunkSize)
                return;


            var fileTime = DateTime.Now.ToString("dd_MM_yy_h_m_s");
            var rotatedPath = logFilePath.Replace(".log", $".{fileTime}");

            var folderPath = Path.GetDirectoryName(rotatedPath);
            var logFolderContent = new DirectoryInfo(folderPath).GetFileSystemInfos();

            CloseListener();

            File.Move(logFilePath, rotatedPath);

            var chunks = logFolderContent.Where(x => !x.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase));

            CreateListener();

            if (chunks.Count() <= _logChunkMaxCount)
                return;

            var archiveFolderInfo = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(rotatedPath), $"{LogFolderName}_{fileTime}"));

            foreach (var chunk in chunks)
            {
                Directory.Move(chunk.FullName, Path.Combine(archiveFolderInfo.FullName, chunk.Name));
            }

            ZipFile.CreateFromDirectory(archiveFolderInfo.FullName, Path.Combine(folderPath, $"{LogFolderName}_{fileTime}.zip"));
            Directory.Delete(archiveFolderInfo.FullName, true);

            var archives = logFolderContent.Where(x => x.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (archives.Count() <= _logArchiveMaxCount)
                return;

            var oldestArchive = archives.OrderBy(x => x.CreationTime).First();
            var cleanupDate = oldestArchive.CreationTime.AddDays(_logCleanupPeriod);
            if (DateTime.Compare(cleanupDate, DateTime.Now) <= 0)
            {
                foreach (var file in logFolderContent)
                {
                    file.Delete();
                }
            }
            else
            {
                File.Delete(oldestArchive.FullName);
            }
        }

        public override string ToString() => $"Logger settings: [Type: {this.GetType().Name}, Chunk Size: {_logChunkSize/1024}kb, Max raw log count: {_logChunkMaxCount}, Max log archive count: {_logArchiveMaxCount}, Cleanup period: {_logCleanupPeriod} days]";
    }

}

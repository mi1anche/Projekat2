using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrugiProjekat.Utils
{
    public class Logger
    {
        private static readonly object _logLock = new object();
        private static StreamWriter? _fileWriter;
        private static readonly string _logPath;

        static Logger()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string logsDir = Path.Combine(projectRoot, "logs");
            Directory.CreateDirectory(logsDir);
            _logPath = Path.Combine(logsDir, $"server_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _fileWriter = new StreamWriter(_logPath, append: true) { AutoFlush = true };
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-5}] [{Thread.CurrentThread.ManagedThreadId,3}] {message}";

            lock (_logLock)
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = level switch
                {
                    "INFO" => ConsoleColor.Cyan,
                    "WARN" => ConsoleColor.Yellow,
                    "ERROR" => ConsoleColor.Red,
                    "CACHE" => ConsoleColor.Green,
                    "REQ" => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };
                Console.WriteLine(line);
                Console.ForegroundColor = prev;
                _fileWriter?.WriteLine(line);
            }
        }

        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERROR", msg);
        public static void Cache(string msg) => Write("CACHE", msg);
        public static void Req(string msg) => Write("REQ", msg);
    }
}

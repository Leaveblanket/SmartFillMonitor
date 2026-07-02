using Serilog;

namespace SmartFillMonitor.Helper
{
    public static class LogHelper
    {
        public static void Info(string message) => Log.Information(message);

        public static void Warn(string message) => Log.Warning(message);

        public static void Debug(string message) => Log.Debug(message);

        public static void Verbose(string message) => Log.Verbose(message);

        public static void Fatal(string message) => Log.Fatal(message);

        public static void Fatal(string message, Exception? ex = null) => Log.Fatal(ex, message);

        public static void Error(string message, Exception? ex = null) => Log.Error(ex, message);
    }
}

using System;
using System.IO;

namespace WeaveDoc.MarkdownEditor.Helpers
{
    internal static class Logger
    {
        private static readonly string LogFilePath;

        static Logger()
        {
            // 尝试向上查找解决方案文件 WeaveDoc.sln，把日志放在包含该文件的目录下（即项目根 WeaveDoc）
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                DirectoryInfo? candidate = dir;
                while (candidate != null)
                {
                    var sln = Path.Combine(candidate.FullName, "WeaveDoc.sln");
                    if (File.Exists(sln))
                    {
                        LogFilePath = Path.Combine(candidate.FullName, "WeaveDoc.MarkdownEditor.log");
                        return;
                    }
                    candidate = candidate.Parent;
                }

                // 未找到解决方案，则退回到当前工作目录
                var cwd = Directory.GetCurrentDirectory();
                LogFilePath = Path.Combine(cwd, "WeaveDoc.MarkdownEditor.log");
            }
            catch
            {
                // 如果任何步骤失败，回退到临时目录
                LogFilePath = Path.Combine(Path.GetTempPath(), "WeaveDoc.MarkdownEditor.log");
            }
        }

        public static void Log(string message)
        {
            try
            {
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, text);
            }
            catch
            {
                // 忽略写日志时的任何错误，避免二次抛出
            }
        }

        public static void LogException(Exception ex)
        {
            try
            {
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] EXCEPTION: {ex}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, text);
            }
            catch
            {
            }
        }

        public static string GetLogPath() => LogFilePath;
    }
}

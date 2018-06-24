using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serialize;

namespace Logs
{
    //https://www.cnblogs.com/taiyonghai/p/6124781.html
    public class LogRecord
    {
        private const string repository = "logger.file.debug";

        static LogRecord()
        {
            var config = Directory.GetCurrentDirectory() + "\\log4net.config";
            var logCfg = new FileInfo(config);
            var loggerRepository = LogManager.CreateRepository(repository);
            XmlConfigurator.ConfigureAndWatch(loggerRepository, logCfg);
        }

        public static void Log(LogLevel level, string description, string logRecord)
        {
            WriteLog(level, description, logRecord);
        }

        public static void Log(LogLevel level, string description, object logRecord)
        {
            WriteLog(level, description, logRecord.ToJson());
        }

        public static void Log(string description, Exception exception)
        {
            WriteLog(LogLevel.Error, description, exception.ToJson());
        }

        private static void WriteLog(LogLevel level, string description, string logRecord)
        {
            var logger = LogManager.GetLogger(repository, description);
            switch (level)
            {
                case LogLevel.Debug:
                    logger.Debug(logRecord);
                    break;
                case LogLevel.Info:
                    logger.Info(logRecord);
                    break;
                case LogLevel.Warn:
                    logger.Warn(logRecord);
                    break;
                case LogLevel.Error:
                    logger.Error(logRecord);
                    break;
                case LogLevel.Fatal:
                    logger.Fatal(logRecord);
                    break;
            }
        }

    }
}

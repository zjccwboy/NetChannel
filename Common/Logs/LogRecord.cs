using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using log4net.Core;
using System.Linq;

[assembly: XmlConfigurator(ConfigFile = "Log4net.config", Watch = true)]

namespace Common
{
    public class LogRecord
    {
        private static ILogger logger;
        private static Type logType;
        static LogRecord()
        {
            GlobalContext.Properties["InstanceName"] = Process.GetCurrentProcess().Id;
            var loggers = LoggerManager.GetCurrentLoggers(Assembly.GetCallingAssembly());
            var loggerSystem = loggers.SingleOrDefault(f => f.Name.Equals("LoggerNameSystem", StringComparison.OrdinalIgnoreCase));
            if (loggerSystem == null)
            {
                loggerSystem = loggers.FirstOrDefault(t => t.Name.Equals("LoggerNameRecordData", StringComparison.InvariantCultureIgnoreCase) == false);
            }
            logger = loggerSystem;
            logType = typeof(LogRecord);
        }

        public static void Log(LogLevel level, string description, string logRecord)
        {
            WriteLog(level, description, logRecord);
        }

        public static void Log(LogLevel level, string description, object logRecord)
        {
            WriteLog(level, description, logRecord.ConvertToJson());
        }

        public static void Log(LogLevel level, string description, Exception exception)
        {
            WriteLog(level, description, exception.ToString());
        }

        public static void Log(string description, Exception exception)
        {
            WriteLog(LogLevel.Error, description, exception.ToString());
        }

        private static void WriteLog(LogLevel level, string description, string logRecord)
        {
            var logLevel = GetLogLevel(level);
            var message = $"Desc:{description} Log:{logRecord}";
            logger.Log(logType, logLevel, message, null);
        }

        private static Level GetLogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return Level.Debug;
                case LogLevel.Info:
                    return Level.Info;
                case LogLevel.Notice:
                    return Level.Notice;
                case LogLevel.Warn:
                    return Level.Warn;
                case LogLevel.Error:
                    return Level.Error;
                case LogLevel.Fatal:
                    return Level.Fatal;
            }
            return Level.Log4Net_Debug;
        }

    }
}

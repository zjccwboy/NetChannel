using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试日志，项目测试阶段日志
        /// </summary>
        Debug,
        /// <summary>
        /// 信息日志，比如程序加载
        /// </summary>
        Info,
        /// <summary>
        /// 分析日志
        /// </summary>
        Notice,
        /// <summary>
        /// 警告日志
        /// </summary>
        Warn,
        /// <summary>
        /// 错误日志，异常捕获日志
        /// </summary>
        Error,
        /// <summary>
        /// 严重错误日志，比如应用程序崩溃，业务逻辑错误的错误
        /// </summary>
        Fatal,
    }
}

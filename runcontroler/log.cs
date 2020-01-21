// --------------------------------------------------------------------------------------------------------------------
// <copyright file="log.cs" company="nd@231216">
//   
// </copyright>
// <summary>
//   Defines the log type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace runcontroler
{
    using System;
    using System.IO;
    using System.Net.Mime;
    using System.Windows.Forms;

    public sealed class Log
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private Log() { }

        public static void Trace(string strMsg)
        {
            _logger.Trace(strMsg);
        }

        public static void Debug(string strMsg)
        {
            _logger.Debug(strMsg);
        }

        public static void Info(string strMsg)
        {
            _logger.Info(strMsg);
        }

        public static void Warn(string strMsg)
        {
            _logger.Warn(strMsg);
        }

        public static void Error(string strMsg)
        {
            _logger.Error(strMsg);
        }

        public static void Fatal(string strMsg)
        {
            _logger.Fatal(strMsg);
        }

    }
}
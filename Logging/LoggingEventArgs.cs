using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andraste.Host.Logging
{
    /// <summary>
    /// The event that is raised by the <see cref="FileLoggingHost"/>, whenever
    /// a new log message arrives
    /// </summary>
    public class LoggingEventArgs : EventArgs
    {
        public string Text { get; }

        public string FilePath { get; }

        public LoggingEventArgs(string text, string filePath)
        {
            Text = text;
            FilePath = filePath;
        }
    }
}

using System.IO;
using System.Threading;

namespace Andraste.Host.Logging
{
    #nullable enable
    /// <summary>
    /// The Andraste Payload supports writing it's logs into text files.
    /// This class will follow the files and emit events, whenever a new line
    /// is logged.
    /// </summary>
    public class FileLoggingHost
    {
        public string FilePath { get; }

        /// <summary>
        /// The time, in milliseconds, to wait before polling new lines.
        /// Setting this value too low can affect performance (CPU and Disk IO).
        /// </summary>
        public int Delay { get; set; } = 1000;

        public delegate void LoggingHandler(object sender, LoggingEventArgs e);
        public event LoggingHandler? LoggingEvent;

        private bool _terminated;
        private Thread? _thread;
        private long _offset;

        public FileLoggingHost(string filePath)
        {
            FilePath = filePath;
        }

        public void StartListening()
        {
            _terminated = false;
            _thread = new Thread(Run)
            {
                Name = $"FileLoggingHost: {FilePath}",
                IsBackground = true
            };
            _thread.Start();
        }

        public void StopListening()
        {
            _terminated = true;
        }

        private void Run()
        {
            // We start with a sleep, to allow the logging backend in the payload to initialize
            Thread.Sleep(Delay);
            var fi = new FileInfo(FilePath);
            Read();

            while (!_terminated)
            {
                fi.Refresh();

                if (fi.Length > _offset)
                {
                    Read();
                }

                Thread.Sleep(Delay);
            }
        }

        private void Read()
        {
            while (!File.Exists(FilePath))
            {
                Thread.Sleep(1000); // Wait for the file to be created (especially the error.log)
            }
            
            var stream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (_offset > 0)
            {
                stream.Seek(_offset, SeekOrigin.Begin);
            }

            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // https://codeblog.jonskeet.uk/2015/01/30/clean-event-handlers-invocation-with-c-6/
                    Interlocked.CompareExchange(ref LoggingEvent, null, null)?.Invoke(this, new LoggingEventArgs(line, FilePath));
                }

                _offset = stream.Position;
            }
        }
    }
    #nullable restore
}

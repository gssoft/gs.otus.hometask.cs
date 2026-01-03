
using global::GuessNumberGame.Core.Services;
using System;
using System.IO;

namespace GuessNumberGame.Core.Services
{
    public class FileLogger : LoggerBase
    {
        private readonly string _filePath;

        public FileLogger(string filePath)
        {
            _filePath = filePath;
        }

        public override void Log(string message)
        {
            File.AppendAllText(_filePath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}
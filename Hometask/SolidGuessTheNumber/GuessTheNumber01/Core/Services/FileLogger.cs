// Core/Services/FileLogger.cs


using global::GuessTheNumber.Core.Services;
using System;
using System.IO;

namespace GuessTheNumber.Core.Services
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
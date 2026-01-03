using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Services;

// Файловый логгер
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

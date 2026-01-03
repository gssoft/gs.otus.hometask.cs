using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Services;

// Консольный логгер
public class ConsoleLogger : LoggerBase
{
    public override void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}

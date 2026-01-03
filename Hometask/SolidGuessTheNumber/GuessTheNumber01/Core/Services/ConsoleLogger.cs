using System;
using System.Collections.Generic;
using System.Text;

// using global::GuessNumberGame.Core.Services;

namespace GuessTheNumber.Core.Services;

public class ConsoleLogger : LoggerBase
{
    public override void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}

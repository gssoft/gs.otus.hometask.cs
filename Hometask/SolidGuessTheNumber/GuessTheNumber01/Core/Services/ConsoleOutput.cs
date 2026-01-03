
using GuessTheNumber.Core.Interfaces;

namespace GuessTheNumber.Core.Services;

public class ConsoleOutput : IOutputProvider
{
    public void DisplayMessage(string message)
    {
        Console.WriteLine(message);
    }

    public void DisplayHint(string hint)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(hint);
        Console.ResetColor();
    }
}
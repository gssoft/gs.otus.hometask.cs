using GuessTheNumber.Core.Interfaces;
using GuessTheNumber.Core.Services;
using System.Security.Cryptography;

class Program
{
    static void Main(string[] args)
    {
        // Настройка зависимостей (простая версия без DI-контейнера)
        IGameSettings settings = new DefaultGameSettings();
        INumberGenerator generator = new RandomNumberGenerator();
        IInputProvider input = new ConsoleInput();
        IOutputProvider output = new ConsoleOutput();
        IGameLogger logger = new ConsoleLogger();

        // Создание игры с внедренными зависимостями
        var game = new GuessNumberGame(settings, generator, input, output, logger);

        // Запуск игры
        bool playAgain;
        do
        {
            var result = game.Play();

            Console.WriteLine("\nХотите сыграть еще раз? (да/нет)");
            string answer = Console.ReadLine().ToLower(System.Globalization.CultureInfo.CurrentCulture);
            playAgain = answer == "да" || answer == "д" || answer == "yes" || answer == "y";
        }
        while (playAgain);
    }
}

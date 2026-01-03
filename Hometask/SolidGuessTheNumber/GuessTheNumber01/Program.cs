// Program.cs

using GuessTheNumber.Core.Interfaces;
using GuessTheNumber.Core.Services;

// using System.Security.Cryptography;

using GuessTheNumber.Game;
Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=== Игра 'Угадай число' ===\n");

// Настройка игры
Console.WriteLine("Выберите уровень сложности:");
Console.WriteLine("1 - Легкий (1-100, 10 попыток)");
Console.WriteLine("2 - Сложный (1-1000, 5 попыток)");
Console.WriteLine("3 - Пользовательский");
Console.Write("Ваш выбор: ");

IGameSettings settings;
var choice = Console.ReadLine();

switch (choice)
{
    case "1":
        settings = new DefaultGameSettings();
        break;
    case "2":
        settings = new HardGameSettings();
        break;
    case "3":
        Console.Write("Введите минимальное число: ");
        int min = int.Parse(Console.ReadLine());
        Console.Write("Введите максимальное число: ");
        int max = int.Parse(Console.ReadLine());
        Console.Write("Введите количество попыток: ");
        int attempts = int.Parse(Console.ReadLine());
        settings = new CustomGameSettings(min, max, attempts);
        break;
    default:
        Console.WriteLine("Неверный выбор, используется легкий уровень.");
        settings = new DefaultGameSettings();
        break;
}

// Настройка зависимостей
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
    Console.WriteLine("\n=== Новая игра ===");
    var result = game.Play();

    if (result.IsWin)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nВы угадали число за {result.AttemptsUsed} попыток!");
        Console.ResetColor();
    }

    Console.WriteLine("\nХотите сыграть еще раз? (yes/no)");
    var answer = Console.ReadLine().ToLower();
    playAgain = answer == "да" || answer == "д" || answer == "yes" || answer == "y";
}
while (playAgain);

Console.WriteLine("\nСпасибо за игру! До свидания!");

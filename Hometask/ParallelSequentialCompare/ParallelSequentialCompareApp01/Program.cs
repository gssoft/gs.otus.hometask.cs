using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    // Фиксированная общая папка
    const string BasePath = @"./../../../../Files";

    // Последовательное чтение трёх файлов
    static void ReadFilesSequentially(params string[] fileNames)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long totalSpaces = 0;

        foreach (string fileName in fileNames)
        {
            string filePath = Path.Combine(BasePath, fileName);
            string text = File.ReadAllText(filePath);
            // totalSpaces += text.Count(c => char.IsWhiteSpace(c));
            totalSpaces += text.Count(c => c == ' ');
        }

        sw.Stop();
        Console.WriteLine($"Последовательное чтение трех файлов ({String.Join(", ", fileNames)}):\n" +
                          $"Кол-во пробелов: {totalSpaces}\n" +
                          $"Время выполнения: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    // Параллельное чтение трёх файлов
    static async Task ReadFilesParallelly(params string[] fileNames)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long totalSpaces = 0;

        List<Task<long>> tasks = new List<Task<long>>();

        foreach (string fileName in fileNames)
        {
            string filePath = Path.Combine(BasePath, fileName);
            tasks.Add(Task.Run(async () =>
            {
                string text = await File.ReadAllTextAsync(filePath);
                // return (long)text.Count(c => char.IsWhiteSpace(c));
                return (long)text.Count(c => c == ' ');
            }));
        }

        await Task.WhenAll(tasks);
        totalSpaces = tasks.Sum(task => task.Result);

        sw.Stop();
        Console.WriteLine($"\nПараллельное чтение трех файлов ({String.Join(", ", fileNames)}):\n" +
                         $"Кол-во пробелов: {totalSpaces}\n" +
                         $"Время выполнения: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    // Последовательное чтение папки
    static void ReadFolderSequentially()
    {
        Stopwatch sw = Stopwatch.StartNew();
        long totalSpaces = 0;

        foreach (string filePath in Directory.EnumerateFiles(BasePath))
        {
            string text = File.ReadAllText(filePath);
            // totalSpaces += text.Count(c => char.IsWhiteSpace(c));
            totalSpaces += text.Count(c => c == ' ');
        }

        sw.Stop();
        Console.WriteLine("\nПоследовательное чтение папки:");
        Console.WriteLine($"Кол-во пробелов: {totalSpaces}\n" +
                         $"Время выполнения: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    // Параллельное чтение папки
    static async Task ReadFolderParallelly()
    {
        Stopwatch sw = Stopwatch.StartNew();
        long totalSpaces = 0;

        IEnumerable<string> filePaths = Directory.EnumerateFiles(BasePath);
        List<Task<long>> tasks = new List<Task<long>>();

        foreach (string path in filePaths)
        {
            tasks.Add(Task.Run(async () =>
            {
                string text = await File.ReadAllTextAsync(path);
                // return (long)text.Count(c => char.IsWhiteSpace(c));
                return (long)text.Count(c => c == ' ');
            }));
        }

        await Task.WhenAll(tasks);
        totalSpaces = tasks.Sum(task => task.Result);

        sw.Stop();
        Console.WriteLine("\nПараллельное чтение папки:");
        Console.WriteLine($"Кол-во пробелов: {totalSpaces}\n" +
                         $"Время выполнения: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Список имен файлов для тестов
        string[] fileNames = { "file1.txt", "file2.txt", "file3.txt" };

        // Последовательное чтение трёх файлов
        ReadFilesSequentially(fileNames);

        // Параллельное чтение трёх файлов
        await ReadFilesParallelly(fileNames);

        // Последовательное чтение папки
        ReadFolderSequentially();

        // Параллельное чтение папки
        await ReadFolderParallelly();
    }
}

// See https://aka.ms/new-console-template for more information


using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Вариант 2. По лучше будет

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Files");
        string folderPath = Path.GetFullPath(baseDir);
        Console.WriteLine($"Folder with Files is Here my Boy ...: {folderPath}");

        // Кол-во прогонов
        var runs = 5;
        Console.WriteLine($"Кол-во прогонов: {runs}");

        var files = Directory.GetFiles(folderPath, "*.txt", SearchOption.AllDirectories);

        Console.WriteLine($"Найдено файлов: {files.Length}");

        await RunBenchmarksAsync(files, runs);
    }

    // Запуск нескольких прогонов и усреднение
    private static async Task RunBenchmarksAsync(string[] filePaths, int runs)
    {
        long totalSpacesSequential = 0;
        long totalSpacesParallel = 0;

        double[] seqTimes = new double[runs];
        double[] parTimes = new double[runs];

        for (int i = 0; i < runs; i++)
        {
            Console.WriteLine($"Прогон #{i + 1}");

            // Последовательный
            var swSeq = Stopwatch.StartNew();
            totalSpacesSequential = await CountSpacesSequentialAsync(filePaths);
            swSeq.Stop();
            seqTimes[i] = swSeq.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Последовательно: {totalSpacesSequential} пробелов, {swSeq.Elapsed.TotalMilliseconds:F2} мс");

            // Параллельный (асинхронный по файлам)
            var swPar = Stopwatch.StartNew();
            totalSpacesParallel = await CountSpacesParallelAsync(filePaths);
            swPar.Stop();
            parTimes[i] = swPar.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Параллельно:      {totalSpacesParallel} пробелов, {swPar.Elapsed.TotalMilliseconds:F2} мс");
        }

        Console.WriteLine();
        Console.WriteLine("Итог по всем прогонам:");

        double avgSeq = seqTimes.Average();
        double avgPar = parTimes.Average();

        Console.WriteLine($"Среднее время последовательного варианта: {avgSeq:F2} мс");
        Console.WriteLine($"Среднее время параллельного варианта:    {avgPar:F2} мс");

        Console.WriteLine();
        Console.WriteLine($"Проверка: последовательный = {totalSpacesSequential}, параллельный = {totalSpacesParallel}");
    }

    // Последовательный подсчёт
    private static async Task<long> CountSpacesSequentialAsync(string[] filePaths)
    {
        long total = 0;

        foreach (var path in filePaths)
        {
            string text = await File.ReadAllTextAsync(path);
            total += text.LongCount(c => c == ' ');
        }

        return total;
    }

    // Параллельный подсчёт: одновременно читаем несколько файлов
    private static async Task<long> CountSpacesParallelAsync(string[] filePaths)
    {

        var tasks = filePaths.Select(async path =>
        {
            string text = await File.ReadAllTextAsync(path);
            return text.LongCount(c => c == ' ');
        }).ToArray();

        long[] results = await Task.WhenAll(tasks);
        return results.Sum();
    }
}

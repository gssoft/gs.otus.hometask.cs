using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ArraySumBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.OutputEncoding = System.Text.UTF8Encoding.UTF8;
            Console.WriteLine("=== Сравнение методов вычисления суммы массива ===\n");

            // Вывод информации о системе
            PrintSystemInfo();

            // Размеры массивов для тестирования
            int[] sizes = { 100_000, 1_000_000, 10_000_000 };

            // Результаты замеров
            var results = new List<BenchmarkResult>();

            foreach (int size in sizes)
            {
                Console.WriteLine($"\n--- Тест для массива из {size:N0} элементов ---");

                // Генерация массива
                int[] array = GenerateArray(size);

                // 1. Последовательное вычисление
                long sequentialSum = 0;
                var sequentialTime = MeasureTime(() =>
                {
                    sequentialSum = SequentialSum(array);
                });
                Console.WriteLine($"Последовательный метод: {sequentialTime} мс, сумма: {sequentialSum}");

                // 2. Параллельное вычисление с использованием Thread
                long parallelSum = 0;
                var parallelTime = MeasureTime(() =>
                {
                    parallelSum = ParallelSumWithThreads(array);
                });
                Console.WriteLine($"Параллельный метод (Threads): {parallelTime} мс, сумма: {parallelSum}");

                // 3. Параллельное вычисление с использованием LINQ
                long linqSum = 0;
                var linqTime = MeasureTime(() =>
                {
                    linqSum = ParallelSumWithLinq(array);
                });
                Console.WriteLine($"Параллельный метод (LINQ): {linqTime} мс, сумма: {linqSum}");

                // Проверка корректности
                if (sequentialSum != parallelSum || sequentialSum != linqSum)
                {
                    Console.WriteLine("Ошибка: суммы не совпадают!");
                }

                results.Add(new BenchmarkResult
                {
                    ArraySize = size,
                    SequentialTime = sequentialTime,
                    ParallelThreadTime = parallelTime,
                    ParallelLinqTime = linqTime
                });
            }

            // Вывод результатов в таблице
            PrintResultsTable(results);

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        // Метод для генерации массива случайных чисел
        static int[] GenerateArray(int size)
        {
            Random rand = new Random();
            int[] array = new int[size];

            for (int i = 0; i < size; i++)
            {
                array[i] = rand.Next(1, 100); // Числа от 1 до 99
            }

            return array;
        }

        // 1. Последовательное вычисление суммы
        static long SequentialSum(int[] array)
        {
            long sum = 0;

            for (int i = 0; i < array.Length; i++)
            {
                sum += array[i];
            }

            return sum;
        }

        // 2. Параллельное вычисление суммы с использованием Thread
        static long ParallelSumWithThreads(int[] array)
        {
            int threadCount = Environment.ProcessorCount; // Количество логических процессоров
            int chunkSize = array.Length / threadCount;
            long totalSum = 0;

            // Массив для хранения результатов каждого потока
            long[] partialSums = new long[threadCount];
            Thread[] threads = new Thread[threadCount];

            // Создание и запуск потоков
            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i; // Локальная копия для замыкания
                int start = threadIndex * chunkSize;
                int end = (threadIndex == threadCount - 1) ? array.Length : start + chunkSize;

                threads[i] = new Thread(() =>
                {
                    long localSum = 0;

                    for (int j = start; j < end; j++)
                    {
                        localSum += array[j];
                    }

                    partialSums[threadIndex] = localSum;
                });

                threads[i].Start();
            }

            // Ожидание завершения всех потоков
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Суммирование результатов
            for (int i = 0; i < threadCount; i++)
            {
                totalSum += partialSums[i];
            }

            return totalSum;
        }

        // 3. Параллельное вычисление суммы с использованием LINQ
        static long ParallelSumWithLinq(int[] array)
        {
            return array.AsParallel().Sum(x => (long)x);
        }

        // Метод для измерения времени выполнения
        static long MeasureTime(Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        // Вывод информации о системе
        static void PrintSystemInfo()
        {
            Console.WriteLine("=== Информация о системе ===");
            Console.WriteLine($"ОС: {Environment.OSVersion}");
            Console.WriteLine($"Версия .NET: {Environment.Version}");
            Console.WriteLine($"Количество логических процессоров: {Environment.ProcessorCount}");
            Console.WriteLine($"Разрядность ОС: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
            Console.WriteLine($"Имя компьютера: {Environment.MachineName}");
            Console.WriteLine();
        }

        // Вывод результатов в виде таблицы
        static void PrintResultsTable(List<BenchmarkResult> results)
        {
            Console.WriteLine("\n=== Результаты замеров времени (в миллисекундах) ===");
            Console.WriteLine("=================================================================");
            Console.WriteLine("| Размер массива | Последовательный | Threads | LINQ AsParallel |");
            Console.WriteLine("|----------------|------------------|---------|-----------------|");

            foreach (var result in results)
            {
                Console.WriteLine($"| {result.ArraySize,13:N0} | {result.SequentialTime,16} | {result.ParallelThreadTime,7} | {result.ParallelLinqTime,15} |");
            }

            Console.WriteLine("=================================================================");

            // Вывод ускорения
            Console.WriteLine("\n=== Коэффициент ускорения (относительно последовательного метода) ===");
            Console.WriteLine("=================================================================");
            Console.WriteLine("| Размер массива | Threads | LINQ AsParallel |");
            Console.WriteLine("|----------------|---------|-----------------|");

            foreach (var result in results)
            {
                double speedupThreads = result.SequentialTime == 0 ? 0 :
                    (double)result.SequentialTime / result.ParallelThreadTime;
                double speedupLinq = result.SequentialTime == 0 ? 0 :
                    (double)result.SequentialTime / result.ParallelLinqTime;

                Console.WriteLine($"| {result.ArraySize,13:N0} | {speedupThreads,7:F2}x | {speedupLinq,15:F2}x |");
            }

            Console.WriteLine("=================================================================");
        }
    }

    // Класс для хранения результатов тестирования
    class BenchmarkResult
    {
        public int ArraySize { get; set; }
        public long SequentialTime { get; set; }
        public long ParallelThreadTime { get; set; }
        public long ParallelLinqTime { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DelegatesAndEvents
{
    // 1. Класс аргументов события для файлов
    public class FileArgs : EventArgs
    {
        public string FileName { get; }
        public bool CancelSearch { get; set; }

        public FileArgs(string fileName)
        {
            FileName = fileName;
            CancelSearch = false;
        }
    }

    // 2. Класс для поиска файлов с событием
    public class FileSearcher
    {
        public event EventHandler<FileArgs> FileFound;

        private bool _searchCancelled = false;

        public void SearchFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Директория не существует: {directoryPath}");
                return;
            }

            try
            {
                SearchInDirectory(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Нет доступа к директории: {directoryPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске: {ex.Message}");
            }
        }

        private void SearchInDirectory(string directory)
        {
            if (_searchCancelled) return;

            try
            {
                // Обработка файлов в текущей директории
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (_searchCancelled) return;

                    var args = new FileArgs(Path.GetFileName(file));
                    OnFileFound(args);

                    if (args.CancelSearch)
                    {
                        _searchCancelled = true;
                        Console.WriteLine("Поиск отменен пользователем.");
                        return;
                    }
                }

                // Рекурсивный поиск в поддиректориях
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    if (_searchCancelled) return;
                    SearchInDirectory(subDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Пропускаем директории без доступа
            }
        }

        protected virtual void OnFileFound(FileArgs args)
        {
            FileFound?.Invoke(this, args);
        }
    }

    // 3. Класс для демонстрации продукта
    public class Product
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public float Weight { get; set; }

        public Product(string name, decimal price, float weight)
        {
            Name = name;
            Price = price;
            Weight = weight;
        }

        public override string ToString()
        {
            return $"{Name} - Цена: {Price:C}, Вес: {Weight}кг";
        }
    }

    // 4. Статический класс с методами расширения
    public static class CollectionExtensions
    {
        public static T GetMax<T>(this IEnumerable<T> collection, Func<T, float> convertToNumber) where T : class
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (convertToNumber == null)
                throw new ArgumentNullException(nameof(convertToNumber));

            T maxItem = null;
            float maxValue = float.MinValue;
            bool hasItems = false;

            foreach (var item in collection)
            {
                if (item == null) continue;

                hasItems = true;
                float value = convertToNumber(item);
                if (value > maxValue)
                {
                    maxValue = value;
                    maxItem = item;
                }
            }

            if (!hasItems)
                throw new InvalidOperationException("Коллекция не содержит элементов");

            return maxItem;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("=== ДЕМОНСТРАЦИЯ ПОИСКА МАКСИМАЛЬНОГО ЭЛЕМЕНТА ===");

            // Создаем коллекцию продуктов
            var products = new List<Product>
            {
                new Product("Яблоки", 120.50m, 1.5f),
                new Product("Бананы", 89.99m, 2.3f),
                new Product("Апельсины", 150.75m, 1.8f),
                new Product("Виноград", 200.00m, 0.9f),
                new Product("Арбуз", 350.00m, 5.7f)
            };

            Console.WriteLine("\nСписок продуктов:");
            foreach (var product in products)
            {
                Console.WriteLine($"  {product}");
            }

            // Используем метод расширения с разными делегатами
            try
            {
                // Поиск по цене
                var maxByPrice = products.GetMax(p => (float)p.Price);
                Console.WriteLine($"\nСамый дорогой продукт: {maxByPrice}");

                // Поиск по весу
                var maxByWeight = products.GetMax(p => p.Weight);
                Console.WriteLine($"Самый тяжелый продукт: {maxByWeight}");

                // Поиск по длине названия
                var maxByNameLength = products.GetMax(p => p.Name.Length);
                Console.WriteLine($"Продукт с самым длинным названием: {maxByNameLength}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске максимального элемента: {ex.Message}");
            }

            Console.WriteLine("\n=== ДЕМОНСТРАЦИЯ ПОИСКА ФАЙЛОВ С СОБЫТИЯМИ ===");

            // Создаем экземпляр поисковика файлов
            var fileSearcher = new FileSearcher();

            // Подписываемся на событие
            fileSearcher.FileFound += (sender, fileArgs) =>
            {
                Console.WriteLine($"Найден файл: {fileArgs.FileName}");

                // Пример условия для отмены поиска
                if (fileArgs.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  Найден текстовый файл {fileArgs.FileName}. Отменяем поиск...");
                    fileArgs.CancelSearch = true;
                }
            };

            // Запускаем поиск в текущей директории
            string currentDirectory = Directory.GetCurrentDirectory();
            Console.WriteLine($"\nПоиск файлов в директории: {currentDirectory}");
            Console.WriteLine("(поиск будет отменен при нахождении .txt файла)\n");

            fileSearcher.SearchFiles(currentDirectory);

            Console.WriteLine("\n=== ПОИСК В КОНКРЕТНОЙ ДИРЕКТОРИИ ===");

            // Подписываемся на событие с другим обработчиком
            var fileSearcher2 = new FileSearcher();
            int fileCount = 0;

            fileSearcher2.FileFound += (sender, fileArgs) =>
            {
                fileCount++;
                Console.WriteLine($"Файл #{fileCount}: {fileArgs.FileName}");

                // Отменяем после 5 файлов
                if (fileCount >= 5)
                {
                    Console.WriteLine("  Найдено 5 файлов. Отменяем поиск...");
                    fileArgs.CancelSearch = true;
                }
            };

            // Ищем в директории Windows (или любой существующей директории)
            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var parentDirectory = Directory.GetParent(systemDirectory)?.FullName;

            if (parentDirectory != null && Directory.Exists(parentDirectory))
            {
                Console.WriteLine($"\nПоиск первых 5 файлов в: {parentDirectory}");
                fileSearcher2.SearchFiles(parentDirectory);
            }

            Console.WriteLine("\nПрограмма завершена. Нажмите любую клавишу...");
            Console.ReadKey();
        }
    }
}

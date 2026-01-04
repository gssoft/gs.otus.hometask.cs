using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DelegatesAndEventsAdvanced
{
    // 1. Кастомные делегаты для демонстрации различных сигнатур
    public delegate float ConvertToNumberDelegate<T>(T item);
    public delegate void FileFoundDelegate(object sender, FileArgs args);
    public delegate bool FilterPredicate<T>(T item);
    public delegate TResult AdvancedConverter<in TInput, out TResult>(TInput input);

    // 2. Аргументы события для файлов
    public class FileArgs : EventArgs
    {
        public string FileName { get; }
        public long FileSize { get; }
        public DateTime CreatedDate { get; }
        public bool CancelSearch { get; set; }

        public FileArgs(string fullPath)
        {
            FileName = Path.GetFileName(fullPath);
            var fileInfo = new FileInfo(fullPath);
            FileSize = fileInfo.Length;
            CreatedDate = fileInfo.CreationTime;
            CancelSearch = false;
        }

        public override string ToString() =>
            $"{FileName} ({FileSize:N0} байт, создан: {CreatedDate:dd.MM.yyyy})";
    }

    // 3. Усовершенствованный класс для поиска файлов
    public class AdvancedFileSearcher
    {
        // События с разными типами делегатов
        public event EventHandler<FileArgs> FileFound; // Стандартный .NET шаблон
        public event Action<object, FileArgs> FileFoundAction; // Делегат Action
        public event FileFoundDelegate FileFoundCustom; // Кастомный делегат

        private readonly List<Predicate<string>> _filters = new();
        private bool _isSearchCancelled;

        // Метод расширения для добавления фильтров
        public AdvancedFileSearcher AddFilter(Predicate<string> filter)
        {
            _filters.Add(filter);
            return this; // Fluent interface
        }

        public void Search(string directory, SearchOption option = SearchOption.AllDirectories)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Директория не найдена: {directory}");

            _isSearchCancelled = false;

            try
            {
                var files = Directory.EnumerateFiles(directory, "*", option);
                ProcessFiles(files);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске: {ex.Message}");
            }
        }

        private void ProcessFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (_isSearchCancelled) break;

                // Применяем все фильтры
                if (_filters.Any(filter => !filter(file)))
                    continue;

                var args = new FileArgs(file);

                // Вызываем все обработчики событий
                OnFileFound(args);

                if (args.CancelSearch)
                {
                    _isSearchCancelled = true;
                    Console.WriteLine("Поиск отменен обработчиком события.");
                    break;
                }
            }
        }

        protected virtual void OnFileFound(FileArgs args)
        {
            FileFound?.Invoke(this, args);
            FileFoundAction?.Invoke(this, args);
            FileFoundCustom?.Invoke(this, args);
        }
    }

    // 4. Расширенные функции расширения для коллекций
    public static class AdvancedCollectionExtensions
    {
        // 4.1. Основная функция расширения из задания
        public static T GetMax<T>(this IEnumerable<T> collection,
                                 Func<T, float> convertToNumber) where T : class
        {
            ValidateArguments(collection, convertToNumber);

            return collection.Aggregate((maxItem, nextItem) =>
                convertToNumber(nextItem) > convertToNumber(maxItem) ? nextItem : maxItem);
        }

        // 4.2. Альтернативная версия с кастомным делегатом
        public static T GetMaxWithCustomDelegate<T>(this IEnumerable<T> collection,
                                                   ConvertToNumberDelegate<T> converter) where T : class
        {
            ValidateArguments(collection, converter);

            T maxItem = null;
            float maxValue = float.MinValue;

            foreach (var item in collection)
            {
                float value = converter(item);
                if (value > maxValue)
                {
                    maxValue = value;
                    maxItem = item;
                }
            }

            return maxItem ?? throw new InvalidOperationException("Коллекция пуста");
        }

        // 4.3. Обобщенная версия с поддержкой любого типа сравнения
        public static T GetMaxBy<T, TKey>(this IEnumerable<T> collection,
                                         Func<T, TKey> selector,
                                         IComparer<TKey> comparer = null) where T : class
        {
            comparer ??= Comparer<TKey>.Default;

            return collection.Aggregate((max, next) =>
                comparer.Compare(selector(next), selector(max)) > 0 ? next : max);
        }

        // 4.4. Функция расширения с использованием предиката
        public static IEnumerable<T> WhereWithPredicate<T>(this IEnumerable<T> collection,
                                                          FilterPredicate<T> predicate)
        {
            foreach (var item in collection)
            {
                if (predicate(item))
                    yield return item;
            }
        }

        // 4.5. Демонстрация ковариантности/контравариантности
        public static IEnumerable<TResult> ConvertAll<T, TResult>(
            this IEnumerable<T> collection,
            AdvancedConverter<T, TResult> converter)
        {
            foreach (var item in collection)
            {
                yield return converter(item);
            }
        }

        private static void ValidateArguments<T>(IEnumerable<T> collection, Delegate converter)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (converter == null)
                throw new ArgumentNullException(nameof(converter));
        }
    }

    // 5. Класс для методов расширения делегатов
    public static class DelegateExtensions
    {
        // Метод расширения для композиции функций
        public static Func<T, TResult> Compose<T, TIntermediate, TResult>(
            this Func<T, TIntermediate> first,
            Func<TIntermediate, TResult> second) =>
            x => second(first(x));

        // Мемоизация (кэширование результатов функции) - метод расширения
        public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func)
        {
            var cache = new Dictionary<T, TResult>();
            return key =>
            {
                if (!cache.TryGetValue(key, out var result))
                {
                    result = func(key);
                    cache[key] = result;
                }
                return result;
            };
        }

        // Метод расширения для последовательного выполнения делегатов Action
        public static Action<T> Chain<T>(this Action<T> first, Action<T> second)
        {
            return x =>
            {
                first?.Invoke(x);
                second?.Invoke(x);
            };
        }

        // Метод расширения для создания конвейера обработки
        public static Func<T, T> Pipeline<T>(params Func<T, T>[] functions)
        {
            return input => functions.Aggregate(input, (current, func) => func(current));
        }
    }

    // 6. Демонстрационные модели
    public class Product
    {
        public string Name { get; }
        public decimal Price { get; }
        public float Weight { get; }
        public int Stock { get; }

        public Product(string name, decimal price, float weight, int stock = 0)
        {
            Name = name;
            Price = price;
            Weight = weight;
            Stock = stock;
        }

        public override string ToString() =>
            $"{Name} - {Price:C} ({Weight}кг, в наличии: {Stock})";
    }

    public class Employee
    {
        public string Name { get; }
        public decimal Salary { get; }
        public int Experience { get; }

        public Employee(string name, decimal salary, int experience)
        {
            Name = name;
            Salary = salary;
            Experience = experience;
        }

        public override string ToString() =>
            $"{Name} - {Salary:C} (опыт: {Experience} лет)";
    }

    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            // ========== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ С ДЕЛЕГАТАМИ ==========
            DemoExtensionMethods();

            // ========== ДЕМОНСТРАЦИЯ СОБЫТИЙ И РАЗНЫХ ТИПОВ ДЕЛЕГАТОВ ==========
            DemoEventsAndDelegates();

            // ========== ДОПОЛНИТЕЛЬНЫЕ ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ ДЕЛЕГАТОВ ==========
            DemoAdvancedDelegateFeatures();

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        static void DemoExtensionMethods()
        {
            Console.WriteLine("=== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ ===");

            var products = new List<Product>
            {
                new("Яблоки", 120.50m, 1.5f, 100),
                new("Бананы", 89.99m, 2.3f, 50),
                new("Апельсины", 150.75m, 1.8f, 75),
                new("Виноград", 200.00m, 0.9f, 25),
                new("Арбуз", 350.00m, 5.7f, 10)
            };

            // 1. Использование основной функции расширения с Func делегатом
            Console.WriteLine("\n1. Поиск максимального элемента:");

            var mostExpensive = products.GetMax(p => (float)p.Price);
            Console.WriteLine($"   Самый дорогой: {mostExpensive}");

            var heaviest = products.GetMax(p => p.Weight);
            Console.WriteLine($"   Самый тяжелый: {heaviest}");

            // 2. Использование с кастомным делегатом
            Console.WriteLine("\n2. Использование кастомного делегата:");
            ConvertToNumberDelegate<Product> stockConverter = p => p.Stock;
            var mostInStock = products.GetMaxWithCustomDelegate(stockConverter);
            Console.WriteLine($"   Больше всего на складе: {mostInStock}");

            // 3. Использование обобщенной версии
            Console.WriteLine("\n3. Обобщенная версия с IComparer:");
            var cheapest = products.GetMaxBy(p => p.Price, Comparer<decimal>.Create((x, y) => y.CompareTo(x)));
            Console.WriteLine($"   Самый дешевый: {cheapest}");

            // 4. Использование предиката
            Console.WriteLine("\n4. Фильтрация с использованием Predicate:");
            FilterPredicate<Product> expensiveFilter = p => p.Price > 100;
            var expensiveProducts = products.WhereWithPredicate(expensiveFilter);
            Console.WriteLine("   Дорогие товары (>100 ₽):");
            foreach (var p in expensiveProducts) Console.WriteLine($"     {p}");

            // 5. Демонстрация ковариантности/контравариантности
            Console.WriteLine("\n5. Ковариантность и контравариантность:");
            AdvancedConverter<Product, string> productDesc = p => $"Товар: {p.Name}";
            var descriptions = products.ConvertAll(productDesc);
            Console.WriteLine("   Описания товаров:");
            foreach (var desc in descriptions) Console.WriteLine($"     {desc}");
        }

        static void DemoEventsAndDelegates()
        {
            Console.WriteLine("\n\n=== ДЕМОНСТРАЦИЯ СОБЫТИЙ И РАЗНЫХ ДЕЛЕГАТОВ ===");

            var searcher = new AdvancedFileSearcher();
            int fileCount = 0;

            // 1. Подписка с использованием EventHandler (стандартный .NET шаблон)
            searcher.FileFound += StandardEventHandler;

            // 2. Подписка с использованием Action делегата
            searcher.FileFoundAction += (sender, args) =>
            {
                Console.WriteLine($"   [Action] Найден: {args.FileName}");
                fileCount++;

                // Отмена поиска после 3 файлов
                if (fileCount >= 3)
                {
                    args.CancelSearch = true;
                    Console.WriteLine("   [Action] Достигнут лимит в 3 файла");
                }
            };

            // 3. Подписка с использованием кастомного делегата
            searcher.FileFoundCustom += CustomDelegateHandler;

            // 4. Добавление фильтров через Predicate делегаты
            searcher
                .AddFilter(path => !path.Contains("temp", StringComparison.OrdinalIgnoreCase))
                .AddFilter(path => !path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                .AddFilter(path => new FileInfo(path).Length < 10_000_000); // < 10MB

            Console.WriteLine("\nПоиск файлов в текущей директории:");
            Console.WriteLine("(будут пропущены temp файлы, .log файлы и файлы >10MB)");
            Console.WriteLine("(поиск остановится после 3 файлов)");

            try
            {
                searcher.Search(Directory.GetCurrentDirectory(), SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        static void DemoAdvancedDelegateFeatures()
        {
            Console.WriteLine("\n\n=== ДОПОЛНИТЕЛЬНЫЕ ВОЗМОЖНОСТИ ДЕЛЕГАТОВ ===");

            // 1. Многоадресные делегаты
            Console.WriteLine("\n1. Многоадресные делегаты:");
            Action<string> logger = null;
            logger += msg => Console.WriteLine($"   [INFO] {msg}");
            logger += msg => Console.WriteLine($"   [DEBUG] {msg} - {DateTime.Now:HH:mm:ss}");
            logger += msg => {
                try
                {
                    File.AppendAllText("log.txt", $"{DateTime.Now}: {msg}\n");
                }
                catch { /* Игнорируем ошибки записи в файл для демо */ }
            };

            logger("Тестовое сообщение");

            // 2. Комбинирование и удаление делегатов
            Console.WriteLine("\n2. Комбинирование делегатов:");
            Func<int, int> operation = null;
            operation += x => x * 2;       // Умножение на 2
            operation += x => x + 10;      // Добавление 10
            operation += x => x * x;       // Возведение в квадрат

            // Важно: выполняется только последний делегат!
            // Для последовательного выполнения нужно другое решение
            Console.WriteLine($"   Результат (только последняя операция): {operation(5)}");

            // Удаление делегата из цепочки
            Console.WriteLine("\n2.1 Удаление делегата из цепочки:");
            Action<string> multiLogger = Console.WriteLine;
            Action<string> fileLogger = msg => Console.WriteLine($"В файл: {msg}");
            multiLogger += fileLogger;
            multiLogger("Оба обработчика");

            multiLogger -= fileLogger;
            multiLogger("Только консоль");

            // 3. Правильное последовательное выполнение
            Console.WriteLine("\n3. Последовательное выполнение операций (композиция):");
            Func<int, int> chain = x => x * 2;

            // Используем метод расширения Compose
            chain = chain.Compose(x => x + 10);
            chain = chain.Compose(x => x * x);

            Console.WriteLine($"   Результат цепочки (5 → *2 → +10 → ^2): {chain(5)}");

            // 4. Кэширование результатов (мемоизация) с использованием метода расширения
            Console.WriteLine("\n4. Мемоизация (кэширование результатов):");

            // Сначала определяем функцию без мемоизации
            Func<int, long> fibonacciRaw = null;
            fibonacciRaw = n => n <= 1 ? n : fibonacciRaw(n - 1) + fibonacciRaw(n - 2);

            // Применяем мемоизацию как метод расширения
            var fibonacci = fibonacciRaw.Memoize();

            Console.WriteLine("   Числа Фибоначчи (с мемоизацией):");
            for (int i = 0; i <= 10; i++)
            {
                Console.WriteLine($"     F({i}) = {fibonacci(i)}");
            }

            // 5. Делегаты как параметры методов
            Console.WriteLine("\n5. Делегаты как параметры методов:");
            ProcessWithRetry(() =>
            {
                Console.WriteLine("   Выполняем операцию...");
                return DateTime.Now.Second > 30; // Имитация условия успеха
            }, 3);

            // 6. Демонстрация цепочки Action делегатов
            Console.WriteLine("\n6. Цепочка Action делегатов:");
            Action<int> numberProcessor = x => Console.WriteLine($"   Число: {x}");
            numberProcessor = numberProcessor.Chain(x => Console.WriteLine($"   Квадрат: {x * x}"));
            numberProcessor = numberProcessor.Chain(x => Console.WriteLine($"   Куб: {x * x * x}"));

            numberProcessor(5);

            // 7. Конвейер обработки
            Console.WriteLine("\n7. Конвейер обработки данных:");
            Func<int, int> pipeline = DelegateExtensions.Pipeline<int>(
                x => x * 2,        // Умножить на 2
                x => x + 10,       // Добавить 10
                x => x * x,        // Возвести в квадрат
                x => x / 2         // Разделить на 2
            );

            Console.WriteLine($"   Результат конвейера (5): {pipeline(5)}");

            // 8. Анонимные методы (старый стиль C# 1.0)
            Console.WriteLine("\n8. Анонимные методы (старый стиль):");
            Func<int, int, int> oldStyleAdd = delegate (int a, int b) { return a + b; };
            Console.WriteLine($"   Сумма через анонимный метод: {oldStyleAdd(10, 20)}");

            // 9. Замыкания (closures)
            Console.WriteLine("\n9. Замыкания (closures):");
            Func<int, Func<int, int>> makeAdder = x => y => x + y;
            var add5 = makeAdder(5);
            var add10 = makeAdder(10);

            Console.WriteLine($"   add5(3) = {add5(3)}");
            Console.WriteLine($"   add10(3) = {add10(3)}");
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

        static void StandardEventHandler(object sender, FileArgs args)
        {
            Console.WriteLine($"   [EventHandler] Найден файл: {args.FileName}");
        }

        static void CustomDelegateHandler(object sender, FileArgs args)
        {
            Console.WriteLine($"   [CustomDelegate] Размер: {args.FileSize:N0} байт");
        }

        // Метод с повторными попытками (использует делегат)
        static void ProcessWithRetry(Func<bool> operation, int maxAttempts)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Console.WriteLine($"   Попытка {attempt} из {maxAttempts}...");
                if (operation())
                {
                    Console.WriteLine("   Успех!");
                    return;
                }
                System.Threading.Thread.Sleep(500);
            }
            Console.WriteLine("   Все попытки неудачны.");
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;

//namespace DelegatesAndEventsAdvanced
//{
//    // 1. Кастомные делегаты для демонстрации различных сигнатур
//    public delegate float ConvertToNumberDelegate<T>(T item);
//    public delegate void FileFoundDelegate(object sender, FileArgs args);
//    public delegate bool FilterPredicate<T>(T item);
//    public delegate TResult AdvancedConverter<in TInput, out TResult>(TInput input);

//    // 2. Аргументы события для файлов
//    public class FileArgs : EventArgs
//    {
//        public string FileName { get; }
//        public long FileSize { get; }
//        public DateTime CreatedDate { get; }
//        public bool CancelSearch { get; set; }

//        public FileArgs(string fullPath)
//        {
//            FileName = Path.GetFileName(fullPath);
//            var fileInfo = new FileInfo(fullPath);
//            FileSize = fileInfo.Length;
//            CreatedDate = fileInfo.CreationTime;
//            CancelSearch = false;
//        }

//        public override string ToString() =>
//            $"{FileName} ({FileSize:N0} байт, создан: {CreatedDate:dd.MM.yyyy})";
//    }

//    // 3. Усовершенствованный класс для поиска файлов
//    public class AdvancedFileSearcher
//    {
//        // События с разными типами делегатов
//        public event EventHandler<FileArgs> FileFound; // Стандартный .NET шаблон
//        public event Action<object, FileArgs> FileFoundAction; // Делегат Action
//        public event FileFoundDelegate FileFoundCustom; // Кастомный делегат

//        private readonly List<Predicate<string>> _filters = new();
//        private bool _isSearchCancelled;

//        // Метод расширения для добавления фильтров
//        public AdvancedFileSearcher AddFilter(Predicate<string> filter)
//        {
//            _filters.Add(filter);
//            return this; // Fluent interface
//        }

//        public void Search(string directory, SearchOption option = SearchOption.AllDirectories)
//        {
//            if (!Directory.Exists(directory))
//                throw new DirectoryNotFoundException($"Директория не найдена: {directory}");

//            _isSearchCancelled = false;

//            try
//            {
//                var files = Directory.EnumerateFiles(directory, "*", option);
//                ProcessFiles(files);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Ошибка при поиске: {ex.Message}");
//            }
//        }

//        private void ProcessFiles(IEnumerable<string> files)
//        {
//            foreach (var file in files)
//            {
//                if (_isSearchCancelled) break;

//                // Применяем все фильтры
//                if (_filters.Any(filter => !filter(file)))
//                    continue;

//                var args = new FileArgs(file);

//                // Вызываем все обработчики событий
//                OnFileFound(args);

//                if (args.CancelSearch)
//                {
//                    _isSearchCancelled = true;
//                    Console.WriteLine("Поиск отменен обработчиком события.");
//                    break;
//                }
//            }
//        }

//        protected virtual void OnFileFound(FileArgs args)
//        {
//            FileFound?.Invoke(this, args);
//            FileFoundAction?.Invoke(this, args);
//            FileFoundCustom?.Invoke(this, args);
//        }
//    }

//    // 4. Расширенные функции расширения для коллекций
//    public static class AdvancedCollectionExtensions
//    {
//        // 4.1. Основная функция расширения из задания
//        public static T GetMax<T>(this IEnumerable<T> collection,
//                                 Func<T, float> convertToNumber) where T : class
//        {
//            ValidateArguments(collection, convertToNumber);

//            return collection.Aggregate((maxItem, nextItem) =>
//                convertToNumber(nextItem) > convertToNumber(maxItem) ? nextItem : maxItem);
//        }

//        // 4.2. Альтернативная версия с кастомным делегатом
//        public static T GetMaxWithCustomDelegate<T>(this IEnumerable<T> collection,
//                                                   ConvertToNumberDelegate<T> converter) where T : class
//        {
//            ValidateArguments(collection, converter);

//            T maxItem = null;
//            float maxValue = float.MinValue;

//            foreach (var item in collection)
//            {
//                float value = converter(item);
//                if (value > maxValue)
//                {
//                    maxValue = value;
//                    maxItem = item;
//                }
//            }

//            return maxItem ?? throw new InvalidOperationException("Коллекция пуста");
//        }

//        // 4.3. Обобщенная версия с поддержкой любого типа сравнения
//        public static T GetMaxBy<T, TKey>(this IEnumerable<T> collection,
//                                         Func<T, TKey> selector,
//                                         IComparer<TKey> comparer = null) where T : class
//        {
//            comparer ??= Comparer<TKey>.Default;

//            return collection.Aggregate((max, next) =>
//                comparer.Compare(selector(next), selector(max)) > 0 ? next : max);
//        }

//        // 4.4. Функция расширения с использованием предиката
//        public static IEnumerable<T> WhereWithPredicate<T>(this IEnumerable<T> collection,
//                                                          FilterPredicate<T> predicate)
//        {
//            foreach (var item in collection)
//            {
//                if (predicate(item))
//                    yield return item;
//            }
//        }

//        // 4.5. Демонстрация ковариантности/контравариантности
//        public static IEnumerable<TResult> ConvertAll<T, TResult>(
//            this IEnumerable<T> collection,
//            AdvancedConverter<T, TResult> converter)
//        {
//            foreach (var item in collection)
//            {
//                yield return converter(item);
//            }
//        }

//        private static void ValidateArguments<T>(IEnumerable<T> collection, Delegate converter)
//        {
//            if (collection == null)
//                throw new ArgumentNullException(nameof(collection));
//            if (converter == null)
//                throw new ArgumentNullException(nameof(converter));
//        }
//    }

//    // 5. Демонстрационные модели
//    public class Product
//    {
//        public string Name { get; }
//        public decimal Price { get; }
//        public float Weight { get; }
//        public int Stock { get; }

//        public Product(string name, decimal price, float weight, int stock = 0)
//        {
//            Name = name;
//            Price = price;
//            Weight = weight;
//            Stock = stock;
//        }

//        public override string ToString() =>
//            $"{Name} - {Price:C} ({Weight}кг, в наличии: {Stock})";
//    }

//    public class Employee
//    {
//        public string Name { get; }
//        public decimal Salary { get; }
//        public int Experience { get; }

//        public Employee(string name, decimal salary, int experience)
//        {
//            Name = name;
//            Salary = salary;
//            Experience = experience;
//        }

//        public override string ToString() =>
//            $"{Name} - {Salary:C} (опыт: {Experience} лет)";

//        public static class DelegateExtensions
//        {
//            // Метод расширения для композиции функций
//            public static Func<T, TResult> Compose<T, TIntermediate, TResult>(
//                this Func<T, TIntermediate> first,
//                Func<TIntermediate, TResult> second) =>
//                x => second(first(x));

//            // Мемоизация (кэширование результатов функции) - метод расширения
//            public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func)
//            {
//                var cache = new Dictionary<T, TResult>();
//                return key =>
//                {
//                    if (!cache.TryGetValue(key, out var result))
//                    {
//                        result = func(key);
//                        cache[key] = result;
//                    }
//                    return result;
//                };
//            }

//            // Метод расширения для последовательного выполнения делегатов Action
//            public static Action<T> Chain<T>(this Action<T> first, Action<T> second)
//            {
//                return x =>
//                {
//                    first?.Invoke(x);
//                    second?.Invoke(x);
//                };
//            }

//            // Метод расширения для создания конвейера обработки
//            public static Func<T, T> Pipeline<T>(params Func<T, T>[] functions)
//            {
//                return input => functions.Aggregate(input, (current, func) => func(current));
//            }

//        }

//    class Program
//    {
//        static void Main()
//        {
//            Console.OutputEncoding = Encoding.UTF8;

//            // ========== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ С ДЕЛЕГАТАМИ ==========
//            DemoExtensionMethods();

//            // ========== ДЕМОНСТРАЦИЯ СОБЫТИЙ И РАЗНЫХ ТИПОВ ДЕЛЕГАТОВ ==========
//            DemoEventsAndDelegates();

//            // ========== ДОПОЛНИТЕЛЬНЫЕ ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ ДЕЛЕГАТОВ ==========
//            DemoAdvancedDelegateFeatures();

//            Console.WriteLine("\nНажмите любую клавишу для выхода...");
//            Console.ReadKey();
//        }

//        static void DemoExtensionMethods()
//        {
//            Console.WriteLine("=== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ ===");

//            var products = new List<Product>
//            {
//                new("Яблоки", 120.50m, 1.5f, 100),
//                new("Бананы", 89.99m, 2.3f, 50),
//                new("Апельсины", 150.75m, 1.8f, 75),
//                new("Виноград", 200.00m, 0.9f, 25),
//                new("Арбуз", 350.00m, 5.7f, 10)
//            };

//            // 1. Использование основной функции расширения с Func делегатом
//            Console.WriteLine("\n1. Поиск максимального элемента:");

//            var mostExpensive = products.GetMax(p => (float)p.Price);
//            Console.WriteLine($"   Самый дорогой: {mostExpensive}");

//            var heaviest = products.GetMax(p => p.Weight);
//            Console.WriteLine($"   Самый тяжелый: {heaviest}");

//            // 2. Использование с кастомным делегатом
//            Console.WriteLine("\n2. Использование кастомного делегата:");
//            ConvertToNumberDelegate<Product> stockConverter = p => p.Stock;
//            var mostInStock = products.GetMaxWithCustomDelegate(stockConverter);
//            Console.WriteLine($"   Больше всего на складе: {mostInStock}");

//            // 3. Использование обобщенной версии
//            Console.WriteLine("\n3. Обобщенная версия с IComparer:");
//            var cheapest = products.GetMaxBy(p => p.Price, Comparer<decimal>.Create((x, y) => y.CompareTo(x)));
//            Console.WriteLine($"   Самый дешевый: {cheapest}");

//            // 4. Использование предиката
//            Console.WriteLine("\n4. Фильтрация с использованием Predicate:");
//            FilterPredicate<Product> expensiveFilter = p => p.Price > 100;
//            var expensiveProducts = products.WhereWithPredicate(expensiveFilter);
//            Console.WriteLine("   Дорогие товары (>100 ₽):");
//            foreach (var p in expensiveProducts) Console.WriteLine($"     {p}");

//            // 5. Демонстрация ковариантности/контравариантности
//            Console.WriteLine("\n5. Ковариантность и контравариантность:");
//            AdvancedConverter<Product, string> productDesc = p => $"Товар: {p.Name}";
//            var descriptions = products.ConvertAll(productDesc);
//            Console.WriteLine("   Описания товаров:");
//            foreach (var desc in descriptions) Console.WriteLine($"     {desc}");
//        }

//        static void DemoEventsAndDelegates()
//        {
//            Console.WriteLine("\n\n=== ДЕМОНСТРАЦИЯ СОБЫТИЙ И РАЗНЫХ ДЕЛЕГАТОВ ===");

//            var searcher = new AdvancedFileSearcher();
//            int fileCount = 0;

//            // 1. Подписка с использованием EventHandler (стандартный .NET шаблон)
//            searcher.FileFound += StandardEventHandler;

//            // 2. Подписка с использованием Action делегата
//            searcher.FileFoundAction += (sender, args) =>
//            {
//                Console.WriteLine($"   [Action] Найден: {args.FileName}");
//                fileCount++;

//                // Отмена поиска после 3 файлов
//                if (fileCount >= 3)
//                {
//                    args.CancelSearch = true;
//                    Console.WriteLine("   [Action] Достигнут лимит в 3 файла");
//                }
//            };

//            // 3. Подписка с использованием кастомного делегата
//            searcher.FileFoundCustom += CustomDelegateHandler;

//            // 4. Добавление фильтров через Predicate делегаты
//            searcher
//                .AddFilter(path => !path.Contains("temp", StringComparison.OrdinalIgnoreCase))
//                .AddFilter(path => !path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
//                .AddFilter(path => new FileInfo(path).Length < 10_000_000); // < 10MB

//            Console.WriteLine("\nПоиск файлов в текущей директории:");
//            Console.WriteLine("(будут пропущены temp файлы, .log файлы и файлы >10MB)");
//            Console.WriteLine("(поиск остановится после 3 файлов)");

//            try
//            {
//                searcher.Search(Directory.GetCurrentDirectory(), SearchOption.TopDirectoryOnly);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Ошибка: {ex.Message}");
//            }
//        }

//        static void DemoAdvancedDelegateFeatures()
//        {
//            Console.WriteLine("\n\n=== ДОПОЛНИТЕЛЬНЫЕ ВОЗМОЖНОСТИ ДЕЛЕГАТОВ ===");

//            // 1. Многоадресные делегаты
//            Console.WriteLine("\n1. Многоадресные делегаты:");
//            Action<string> logger = null;
//            logger += msg => Console.WriteLine($"   [INFO] {msg}");
//            logger += msg => Console.WriteLine($"   [DEBUG] {msg} - {DateTime.Now:HH:mm:ss}");
//            logger += msg => File.AppendAllText("log.txt", $"{DateTime.Now}: {msg}\n");

//            logger("Тестовое сообщение");

//            // 2. Комбинирование и удаление делегатов
//            Console.WriteLine("\n2. Комбинирование делегатов:");
//            Func<int, int> operation = null;
//            operation += x => x * 2;       // Умножение на 2
//            operation += x => x + 10;      // Добавление 10
//            operation += x => x * x;       // Возведение в квадрат

//            // Важно: выполняется только последний делегат!
//            // Для последовательного выполнения нужно другое решение
//            Console.WriteLine($"   Результат (только последняя операция): {operation(5)}");

//            // 3. Правильное последовательное выполнение
//            Console.WriteLine("\n3. Последовательное выполнение операций:");
//            Func<int, int> chain = null;
//            chain = x => x * 2;
//            chain = chain.Compose(x => x + 10);
//            chain = chain.Compose(x => x * x);
//            Console.WriteLine($"   Результат цепочки (5 → *2 → +10 → *2): {chain(5)}");

//            // 4. Кэширование результатов (мемоизация)
//            Console.WriteLine("\n4. Мемоизация (кэширование результатов):");
//            Func<int, long> fibonacci = null;
//            fibonacci = Memoize<int, long>(n =>
//                n <= 1 ? n : fibonacci(n - 1) + fibonacci(n - 2));

//            Console.WriteLine("   Числа Фибоначчи:");
//            for (int i = 0; i <= 10; i++)
//            {
//                Console.WriteLine($"     F({i}) = {fibonacci(i)}");
//            }

//            // 5. Делегаты как параметры методов
//            Console.WriteLine("\n5. Делегаты как параметры методов:");
//            ProcessWithRetry(() =>
//            {
//                Console.WriteLine("   Выполняем операцию...");
//                return DateTime.Now.Second > 30; // Имитация условия успеха
//            }, 3);
//        }

//        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

//        static void StandardEventHandler(object sender, FileArgs args)
//        {
//            Console.WriteLine($"   [EventHandler] Найден файл: {args.FileName}");
//        }

//        static void CustomDelegateHandler(object sender, FileArgs args)
//        {
//            Console.WriteLine($"   [CustomDelegate] Размер: {args.FileSize:N0} байт");
//        }

//        // Метод расширения для композиции функций
//        public static Func<T, TResult> Compose<T, TIntermediate, TResult>(
//            this Func<T, TIntermediate> first,
//            Func<TIntermediate, TResult> second) =>
//            x => second(first(x));

//        // Мемоизация (кэширование результатов функции)
//        public static Func<T, TResult> Memoize<T, TResult>(Func<T, TResult> func)
//        {
//            var cache = new Dictionary<T, TResult>();
//            return key =>
//            {
//                if (!cache.TryGetValue(key, out var result))
//                {
//                    result = func(key);
//                    cache[key] = result;
//                }
//                return result;
//            };
//        }

//        // Метод с повторными попытками (использует делегат)
//        static void ProcessWithRetry(Func<bool> operation, int maxAttempts)
//        {
//            for (int attempt = 1; attempt <= maxAttempts; attempt++)
//            {
//                Console.WriteLine($"   Попытка {attempt} из {maxAttempts}...");
//                if (operation())
//                {
//                    Console.WriteLine("   Успех!");
//                    return;
//                }
//                System.Threading.Thread.Sleep(500);
//            }
//            Console.WriteLine("   Все попытки неудачны.");
//        }
//    }
//}

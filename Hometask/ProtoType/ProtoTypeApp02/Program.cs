// Program.cs

// Program.cs - исправленная версия
using ProtoTypeApp;
using System;

namespace TradingPrototype
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("=== Trading Prototype Demo with both interfaces ===");

            // Создаем объекты
            var strategy = new MeanReversionStrategy(
                name: "MR-1",
                description: "Mean Reversion on major FX pairs",
                symbols: new[] { "EURUSD", "USDJPY" },
                lookbackPeriod: 20,
                entryThreshold: 1.5m,
                exitThreshold: 0.5m);

            var limitOrder = new LimitOrder(
                symbol: "EURUSD",
                quantity: 100000m,
                price: 1.1000m,
                side: OrderSide.Buy,
                exchange: "FXCM",
                isPostOnly: true);

            var trade = new Trade(
                orderId: limitOrder.Id,
                symbol: "EURUSD",
                executedQuantity: 100000m,
                executedPrice: 1.1001m,
                commission: 5m);

            // Демонстрация IMyCloneable<T>
            Console.WriteLine("\n=== IMyCloneable<T> Demo ===");

            // Клонирование через IMyCloneable<T>
            var clonedStrategy = ((IMyCloneable<MeanReversionStrategy>)strategy).MyClone();
            Console.WriteLine($"Original strategy name: {strategy.Name}");
            Console.WriteLine($"Cloned strategy name: {clonedStrategy.Name}");
            Console.WriteLine($"ReferenceEquals: {ReferenceEquals(strategy, clonedStrategy)}");

            // Клонирование заказа
            var clonedOrder = ((IMyCloneable<LimitOrder>)limitOrder).MyClone();
            Console.WriteLine($"\nOriginal order quantity: {limitOrder.Quantity}");
            Console.WriteLine($"Cloned order quantity: {clonedOrder.Quantity}");
            Console.WriteLine($"ReferenceEquals: {ReferenceEquals(limitOrder, clonedOrder)}");

            // Клонирование сделки
            var clonedTrade = ((IMyCloneable<Trade>)trade).MyClone();
            Console.WriteLine($"\nOriginal trade commission: {trade.Commission}");
            Console.WriteLine($"Cloned trade commission: {clonedTrade.Commission}");
            Console.WriteLine($"ReferenceEquals: {ReferenceEquals(trade, clonedTrade)}");

            // Демонстрация ICloneable
            Console.WriteLine("\n=== ICloneable Demo ===");

            // Клонирование через ICloneable
            var clonedViaICloneable = (MeanReversionStrategy)strategy.Clone();
            Console.WriteLine($"Original strategy ID: {strategy.Id}");
            Console.WriteLine($"Cloned strategy ID: {clonedViaICloneable.Id}");
            Console.WriteLine($"ReferenceEquals: {ReferenceEquals(strategy, clonedViaICloneable)}");

            // Проверка, что это разные объекты
            // Вместо изменения Name (который имеет protected set), создадим новый объект с другим именем
            var modifiedClone = new MeanReversionStrategy(
                name: "MR-1-Cloned",
                description: clonedViaICloneable.Description,
                symbols: clonedViaICloneable.Symbols,
                lookbackPeriod: clonedViaICloneable.LookbackPeriod,
                entryThreshold: clonedViaICloneable.EntryThreshold,
                exitThreshold: clonedViaICloneable.ExitThreshold);

            Console.WriteLine($"\nAfter creating modified clone:");
            Console.WriteLine($"Original strategy name: {strategy.Name}");
            Console.WriteLine($"Modified clone strategy name: {modifiedClone.Name}");

            // Демонстрация глубокого копирования с TradingContext
            Console.WriteLine("\n=== Deep Copy with TradingContext ===");

            var context = new TradingContext("Main Portfolio", strategy);
            context.AddOrder(limitOrder);
            context.AddTrade(trade);

            var clonedContext = ((IMyCloneable<TradingContext>)context).MyClone();

            Console.WriteLine($"Original context portfolio: {context.PortfolioName}");
            Console.WriteLine($"Cloned context portfolio: {clonedContext.PortfolioName}");
            Console.WriteLine($"ReferenceEquals for context: {ReferenceEquals(context, clonedContext)}");
            Console.WriteLine($"ReferenceEquals for strategy: {ReferenceEquals(context.Strategy, clonedContext.Strategy)}");
            Console.WriteLine($"ReferenceEquals for orders list: {ReferenceEquals(context.Orders, clonedContext.Orders)}");
            Console.WriteLine($"ReferenceEquals for first order: {ReferenceEquals(context.Orders[0], clonedContext.Orders[0])}");

            // Преимущества и недостатки
            Console.WriteLine("\n=== Advantages and Disadvantages ===");
            Console.WriteLine("\nIMyCloneable<T> advantages:");
            Console.WriteLine("1. Type-safe - returns specific type, no casting needed");
            Console.WriteLine("2. Generic - can work with any type");
            Console.WriteLine("3. Can have custom name (MyClone) to avoid conflicts");
            Console.WriteLine("\nIMyCloneable<T> disadvantages:");
            Console.WriteLine("1. Not standard - not part of .NET framework");
            Console.WriteLine("2. Requires explicit interface implementation for covariance");
            Console.WriteLine("3. More verbose syntax for calling");

            Console.WriteLine("\nICloneable advantages:");
            Console.WriteLine("1. Standard interface in .NET framework");
            Console.WriteLine("2. Widely recognized and used pattern");
            Console.WriteLine("3. Simple to use with 'Clone()' method");
            Console.WriteLine("\nICloneable disadvantages:");
            Console.WriteLine("1. Not type-safe - returns object, requires casting");
            Console.WriteLine("2. No distinction between shallow and deep copy");
            Console.WriteLine("3. Often considered obsolete in modern C#");

            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine("В нашей реализации:");
            Console.WriteLine("1. Все классы наследуются от TradingEntity (2 уровня наследования)");
            Console.WriteLine("2. Реализован обобщенный интерфейс IMyCloneable<T>");
            Console.WriteLine("3. Реализован стандартный интерфейс ICloneable через метод MyClone()");
            Console.WriteLine("4. Используются копирующие конструкторы для реализации клонирования");
            Console.WriteLine("5. Для глубокого копирования коллекций используется LINQ Select()");

            Console.WriteLine("\n=== End of demo ===");
            Console.ReadKey();
        }
    }
}
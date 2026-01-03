// Program.cs
// Program.cs
// Program.cs
using System;
using ProtoTypeApp;

namespace TradingPrototype
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Trading Prototype Demo ===");

            var strategy = new MeanReversionStrategy(
                name: "MR-1",
                description: "Mean Reversion on major FX pairs",
                symbols: new[] { "EURUSD", "USDJPY" },
                lookbackPeriod: 20,
                entryThreshold: 1.5m,
                exitThreshold: 0.5m);

            var context = new TradingContext(
                portfolioName: "Main FX Portfolio",
                strategy: strategy);

            var limitOrder = new LimitOrder(
                symbol: "EURUSD",
                quantity: 100000m,
                price: 1.1000m,
                side: OrderSide.Buy,
                exchange: "FXCM",
                isPostOnly: true);

            var marketOrder = new MarketOrder(
                symbol: "USDJPY",
                quantity: 200000m,
                side: OrderSide.Sell,
                exchange: "OANDA",
                tif: TimeInForce.Ioc,
                slippageTolerance: 0.0005m);

            context.AddOrder(limitOrder);
            context.AddOrder(marketOrder);

            var trade = new Trade(
                orderId: limitOrder.Id,
                symbol: "EURUSD",
                executedQuantity: 100000m,
                executedPrice: 1.1001m,
                commission: 5m);

            context.AddTrade(trade);

            Console.WriteLine("Original context orders count: " + context.Orders.Count);
            Console.WriteLine("Original context trades count: " + context.Trades.Count);

            // Клонируем контекст (типобезопасный метод)
            var clonedContext = context.MyClone();

            Console.WriteLine("Cloned context orders count: " + clonedContext.Orders.Count);
            Console.WriteLine("Cloned context trades count: " + clonedContext.Trades.Count);

            // Проверим, что это разные объекты
            Console.WriteLine("ReferenceEquals(context, clonedContext) = " +
                              ReferenceEquals(context, clonedContext));

            Console.WriteLine("ReferenceEquals(context.Strategy, clonedContext.Strategy) = " +
                              ReferenceEquals(context.Strategy, clonedContext.Strategy));

            // Модифицируем оригинал, проверим, что клон не меняется
            context.Orders[0] = new LimitOrder(
                symbol: "EURUSD",
                quantity: 500000m,
                price: 1.2000m,
                side: OrderSide.Buy,
                exchange: "FXCM",
                isPostOnly: false);

            Console.WriteLine("After modification:");
            Console.WriteLine("Original first order quantity: " +
                              context.Orders[0].Quantity);
            Console.WriteLine("Cloned first order quantity: " +
                              clonedContext.Orders[0].Quantity);

            // Демонстрация ICloneable
            var cloneableOrder = (ICloneable)limitOrder;
            var orderCloneAsObject = cloneableOrder.Clone();
            Console.WriteLine("ICloneable order clone type: " +
                              orderCloneAsObject?.GetType().Name);

            // Проверим, что ID клонированного объекта отличается от оригинала
            Console.WriteLine("Original order ID: " + limitOrder.Id);
            Console.WriteLine("Cloned order ID: " + ((OrderBase)orderCloneAsObject).Id);

            // Демонстрация IMyCloneable<T> для LimitOrder
            var typedCloneableOrder = (IMyCloneable<LimitOrder>)limitOrder;
            var typedOrderClone = typedCloneableOrder.MyClone();
            Console.WriteLine("IMyCloneable<LimitOrder> clone ID: " + typedOrderClone.Id);

            Console.WriteLine("=== End of demo ===");
            Console.ReadKey();
        }
    }
}
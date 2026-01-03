// TradingContext.cs

// TradingContext.cs
using ProtoTypeApp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingPrototype
{
    public class TradingContext : TradingEntity, IMyCloneable<TradingContext>
    {
        public string PortfolioName { get; private set; }
        public List<OrderBase> Orders { get; private set; }
        public List<Trade> Trades { get; private set; }
        public TradingStrategyBase Strategy { get; private set; }

        public TradingContext(
            string portfolioName,
            TradingStrategyBase strategy)
        {
            PortfolioName = portfolioName;
            Strategy = strategy;
            Orders = new List<OrderBase>();
            Trades = new List<Trade>();
        }

        protected TradingContext(TradingContext other) : base(other)
        {
            PortfolioName = other.PortfolioName;

            // Глубокое копирование коллекций
            Orders = other.Orders
                .Select(o => o.CloneOrder())
                .ToList();

            Trades = other.Trades
                .Select(t =>
                {
                    // Используем типобезопасный MyClone, если есть
                    if (t is IMyCloneable<Trade> tradeCloneable)
                        return tradeCloneable.MyClone();
                    return new Trade(t); // запасной вариант
                })
                .ToList();

            // Типобезопасное клонирование стратегии, если поддерживает IMyCloneable
            if (other.Strategy is IMyCloneable<TradingStrategyBase> strategyCloneable)
                Strategy = strategyCloneable.MyClone();
            else
                Strategy = other.Strategy;
        }

        public void AddOrder(OrderBase order)
        {
            Orders.Add(order);
        }

        public void AddTrade(Trade trade)
        {
            Trades.Add(trade);
        }

        // Реализация MyClone из TradingEntity
        public override TradingEntity MyClone()
        {
            return new TradingContext(this);
        }

        // Специализированный метод для TradingContext
        public TradingContext Clone()
        {
            return new TradingContext(this);
        }

        // Типобезопасное клонирование через IMyCloneable<TradingContext>
        public TradingContext MyClone()
        {
            return new TradingContext(this);
        }
    }
}
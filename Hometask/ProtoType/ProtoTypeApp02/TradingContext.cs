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
                .Select(o => ((IMyCloneable<OrderBase>)o).MyClone())
                .ToList();

            Trades = other.Trades
                .Select(t => ((IMyCloneable<Trade>)t).MyClone())
                .ToList();

            Strategy = other.Strategy != null
                ? ((IMyCloneable<TradingStrategyBase>)other.Strategy).MyClone()
                : null;
        }

        public void AddOrder(OrderBase order)
        {
            Orders.Add(order);
        }

        public void AddTrade(Trade trade)
        {
            Trades.Add(trade);
        }

        // Реализация абстрактного метода из TradingEntity
        public override TradingEntity MyClone()
        {
            return new TradingContext(this);
        }

        // Явная реализация IMyCloneable<TradingContext>
        TradingContext IMyCloneable<TradingContext>.MyClone()
        {
            return new TradingContext(this);
        }
    }
}
// Orders.cs

// Orders.cs
using ProtoTypeApp;
using System;

namespace TradingPrototype
{
    public enum OrderSide
    {
        Buy,
        Sell
    }

    public abstract class OrderBase : TradingEntity, IMyCloneable<OrderBase>
    {
        public string Symbol { get; protected set; }
        public decimal Quantity { get; protected set; }
        public decimal? Price { get; protected set; }
        public OrderSide Side { get; protected set; }
        public string Exchange { get; protected set; }

        protected OrderBase(
            string symbol,
            decimal quantity,
            decimal? price,
            OrderSide side,
            string exchange)
        {
            Symbol = symbol;
            Quantity = quantity;
            Price = price;
            Side = side;
            Exchange = exchange;
        }

        // Копирующий конструктор
        protected OrderBase(OrderBase other) : base(other)
        {
            Symbol = other.Symbol;
            Quantity = other.Quantity;
            Price = other.Price;
            Side = other.Side;
            Exchange = other.Exchange;
        }

        // Реализация из TradingEntity (скрывает базовый метод)
        public new abstract OrderBase MyClone();

        // Явная реализация IMyCloneable<OrderBase>
        OrderBase IMyCloneable<OrderBase>.MyClone() => MyClone();
    }

    public class LimitOrder : OrderBase, IMyCloneable<LimitOrder>
    {
        public bool IsPostOnly { get; private set; }

        public LimitOrder(
            string symbol,
            decimal quantity,
            decimal price,
            OrderSide side,
            string exchange,
            bool isPostOnly)
            : base(symbol, quantity, price, side, exchange)
        {
            IsPostOnly = isPostOnly;
        }

        protected LimitOrder(LimitOrder other) : base(other)
        {
            IsPostOnly = other.IsPostOnly;
        }

        // Реализация из OrderBase
        public override OrderBase MyClone()
        {
            return new LimitOrder(this);
        }

        // Специализированный метод для LimitOrder
        public LimitOrder Clone()
        {
            return new LimitOrder(this);
        }

        // Явная реализация IMyCloneable<LimitOrder>
        LimitOrder IMyCloneable<LimitOrder>.MyClone() => Clone();
    }

    public enum TimeInForce
    {
        Day,
        Gtc,  // Good Till Cancel
        Ioc,  // Immediate Or Cancel
        Fok   // Fill Or Kill
    }

    public class MarketOrder : OrderBase, IMyCloneable<MarketOrder>
    {
        public TimeInForce TimeInForce { get; private set; }
        public decimal SlippageTolerance { get; private set; }

        public MarketOrder(
            string symbol,
            decimal quantity,
            OrderSide side,
            string exchange,
            TimeInForce tif,
            decimal slippageTolerance)
            : base(symbol, quantity, price: null, side, exchange)
        {
            TimeInForce = tif;
            SlippageTolerance = slippageTolerance;
        }

        protected MarketOrder(MarketOrder other) : base(other)
        {
            TimeInForce = other.TimeInForce;
            SlippageTolerance = other.SlippageTolerance;
        }

        // Реализация из OrderBase
        public override OrderBase MyClone()
        {
            return new MarketOrder(this);
        }

        // Специализированный метод для MarketOrder
        public MarketOrder Clone()
        {
            return new MarketOrder(this);
        }

        // Явная реализация IMyCloneable<MarketOrder>
        MarketOrder IMyCloneable<MarketOrder>.MyClone() => Clone();
    }
}
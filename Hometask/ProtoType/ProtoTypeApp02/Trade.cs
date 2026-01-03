// Trade.cs

// Trade.cs
using ProtoTypeApp;
using System;

namespace TradingPrototype
{
    public class Trade : TradingEntity, IMyCloneable<Trade>
    {
        public Guid OrderId { get; private set; }
        public string Symbol { get; private set; }
        public decimal ExecutedQuantity { get; private set; }
        public decimal ExecutedPrice { get; private set; }
        public decimal Commission { get; private set; }

        public Trade(
            Guid orderId,
            string symbol,
            decimal executedQuantity,
            decimal executedPrice,
            decimal commission)
        {
            OrderId = orderId;
            Symbol = symbol;
            ExecutedQuantity = executedQuantity;
            ExecutedPrice = executedPrice;
            Commission = commission;
        }

        protected Trade(Trade other) : base(other)
        {
            OrderId = other.OrderId;
            Symbol = other.Symbol;
            ExecutedQuantity = other.ExecutedQuantity;
            ExecutedPrice = other.ExecutedPrice;
            Commission = other.Commission;
        }

        // Реализация абстрактного метода из TradingEntity
        public override TradingEntity MyClone()
        {
            return new Trade(this);
        }

        // Явная реализация IMyCloneable<Trade>
        Trade IMyCloneable<Trade>.MyClone()
        {
            return new Trade(this);
        }
    }
}
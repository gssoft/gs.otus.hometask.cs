// TradeStrategies.cs

// TradeStrategies.cs
using ProtoTypeApp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingPrototype
{
    public abstract class TradingStrategyBase : TradingEntity
    {
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public List<string> Symbols { get; protected set; }

        protected TradingStrategyBase(
            string name,
            string description,
            IEnumerable<string> symbols)
        {
            Name = name;
            Description = description;
            Symbols = symbols?.ToList() ?? new List<string>();
        }

        protected TradingStrategyBase(TradingStrategyBase other) : base(other)
        {
            Name = other.Name;
            Description = other.Description;
            Symbols = new List<string>(other.Symbols);
        }

        public abstract void GenerateSignal(
            string symbol,
            decimal lastPrice);
    }

    public class MeanReversionStrategy : TradingStrategyBase
    {
        public int LookbackPeriod { get; private set; }
        public decimal EntryThreshold { get; private set; }
        public decimal ExitThreshold { get; private set; }

        public MeanReversionStrategy(
            string name,
            string description,
            IEnumerable<string> symbols,
            int lookbackPeriod,
            decimal entryThreshold,
            decimal exitThreshold)
            : base(name, description, symbols)
        {
            LookbackPeriod = lookbackPeriod;
            EntryThreshold = entryThreshold;
            ExitThreshold = exitThreshold;
        }

        protected MeanReversionStrategy(MeanReversionStrategy other)
            : base(other)
        {
            LookbackPeriod = other.LookbackPeriod;
            EntryThreshold = other.EntryThreshold;
            ExitThreshold = other.ExitThreshold;
        }

        // Реализация MyClone из TradingEntity
        public override TradingEntity MyClone()
        {
            return new MeanReversionStrategy(this);
        }

        // Специализированный метод для MeanReversionStrategy
        public MeanReversionStrategy Clone()
        {
            return new MeanReversionStrategy(this);
        }

        public override void GenerateSignal(string symbol, decimal lastPrice)
        {
            Console.WriteLine(
                $"[MeanReversion] GenerateSignal for {symbol}, price={lastPrice}");
        }
    }
}
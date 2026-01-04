using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace AdvancedTradingSystem
{
    // ========== ОСНОВНЫЕ ТИПЫ ДАННЫХ ==========

    public enum OrderSide { Buy, Sell }
    public enum OrderType { Market, Limit, Stop }
    public enum OrderStatus { Pending, Filled, PartiallyFilled, Cancelled, Rejected }
    public enum SignalType { Buy, Sell, Hold, StrongBuy, StrongSell }
    public enum MarketState { PreMarket, Open, PostMarket, Closed, Halted }

    // ========== КАСТОМНЫЕ ДЕЛЕГАТЫ ==========

    public delegate void OrderEventHandler(Order order);
    public delegate void TradeEventHandler(Trade trade);
    public delegate void QuoteEventHandler(Quote quote);
    public delegate void SignalEventHandler(Signal signal);
    public delegate void PositionEventHandler(Position position);
    public delegate Task<bool> RiskCheckDelegate(Order order, TradingContext context);
    public delegate decimal CalculateSlippageDelegate(Order order, MarketData marketData);
    public delegate IEnumerable<Signal> StrategyDelegate(IEnumerable<Bar> bars, TradingContext context);
    public delegate void MarketStateChangedEventHandler(MarketState oldState, MarketState newState);

    // ========== КЛАССЫ ДАННЫХ ==========

    public record Quote(string Symbol, decimal Bid, decimal Ask, decimal Last, decimal Volume, DateTime Timestamp)
    {
        public decimal Spread => Ask - Bid;
        public decimal MidPrice => (Bid + Ask) / 2;
    }

    public record Bar(string Symbol, DateTime OpenTime, DateTime CloseTime,
        decimal Open, decimal High, decimal Low, decimal Close, decimal Volume,
        BarTimeFrame TimeFrame = BarTimeFrame.OneMinute)
    {
        public bool IsBullish => Close > Open;
        public bool IsBearish => Close < Open;
        public decimal BodySize => Math.Abs(Close - Open);
        public decimal TotalRange => High - Low;
        public decimal UpperShadow => High - Math.Max(Open, Close);
        public decimal LowerShadow => Math.Min(Open, Close) - Low;
    }

    public enum BarTimeFrame
    {
        OneSecond, FiveSeconds, FifteenSeconds, ThirtySeconds,
        OneMinute, FiveMinutes, FifteenMinutes, ThirtyMinutes,
        OneHour, FourHours, OneDay, OneWeek, OneMonth
    }

    public record Trade(string TradeId, string Symbol, decimal Price, decimal Quantity,
        OrderSide Side, DateTime Timestamp, string BuyerId, string SellerId)
    {
        public decimal Value => Price * Quantity;
        public bool IsBlockTrade => Quantity >= 10000;
    }

    public record Order(string OrderId, string Symbol, OrderType Type, OrderSide Side,
        decimal Quantity, decimal? Price, decimal? StopPrice, DateTime CreatedTime,
        string AccountId, string StrategyId = null)
    {
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public decimal FilledQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal RemainingQuantity => Quantity - FilledQuantity;
        public bool IsFullyFilled => Status == OrderStatus.Filled;
        public bool IsActive => Status == OrderStatus.Pending || Status == OrderStatus.PartiallyFilled;

        public decimal EstimatedValue => Price.HasValue ? Price.Value * Quantity : 0;
        public string FullId => $"{OrderId}_{Symbol}_{Side}_{CreatedTime:yyyyMMddHHmmss}";
    }

    public record Position(string PositionId, string Symbol, decimal Quantity,
        decimal AverageEntryPrice, decimal CurrentPrice, DateTime OpenedTime)
    {
        public decimal MarketValue => Quantity * CurrentPrice;
        public decimal CostBasis => Quantity * AverageEntryPrice;
        public decimal UnrealizedPnL => MarketValue - CostBasis;
        public decimal UnrealizedPnLPercentage => CostBasis != 0 ? (UnrealizedPnL / CostBasis) * 100 : 0;
        public bool IsLong => Quantity > 0;
        public bool IsShort => Quantity < 0;

        public Position WithUpdatedPrice(decimal newPrice) =>
            this with { CurrentPrice = newPrice };

        public Position WithIncreasedQuantity(decimal additionalQuantity, decimal fillPrice)
        {
            var totalQuantity = Quantity + additionalQuantity;
            var newAveragePrice = (CostBasis + additionalQuantity * fillPrice) / totalQuantity;

            return this with
            {
                Quantity = totalQuantity,
                AverageEntryPrice = Math.Abs(newAveragePrice)
            };
        }

        public Position WithDecreasedQuantity(decimal quantityToReduce) =>
            this with { Quantity = Quantity - quantityToReduce };
    }

    public record Signal(string SignalId, string Symbol, SignalType Type, decimal Strength,
        DateTime GeneratedTime, string StrategyId, string Reason = null)
    {
        public bool IsActionable => Type != SignalType.Hold;
        public decimal ConfidenceScore => Math.Clamp(Strength, 0, 1);
        public bool IsStrongSignal => Strength >= 0.7m;

        public string Description => $"{Symbol}: {Type} (Strength: {Strength:P0}) - {Reason}";
    }

    public record MarketData(string Symbol, Quote LatestQuote, Bar LatestBar,
        IEnumerable<Bar> RecentBars, decimal Volatility, decimal VolumeRatio);

    // ========== СИСТЕМНЫЕ КОМПОНЕНТЫ ==========

    public interface IExchange
    {
        string ExchangeId { get; }
        MarketState CurrentState { get; }
        event MarketStateChangedEventHandler MarketStateChanged;
        event QuoteEventHandler QuoteUpdated;
        event TradeEventHandler TradeExecuted;

        Task<Order> SubmitOrderAsync(Order order);
        Task<bool> CancelOrderAsync(string orderId);
        Task<MarketData> GetMarketDataAsync(string symbol);
        Task<IEnumerable<Bar>> GetHistoricalDataAsync(string symbol, BarTimeFrame timeframe, int periods);
    }

    public abstract class TradingStrategy
    {
        public string StrategyId { get; }
        public string Name { get; }
        public decimal CapitalAllocation { get; set; }
        public bool IsActive { get; set; } = true;

        public event SignalEventHandler SignalGenerated;
        public event EventHandler<Exception> StrategyError;

        protected TradingStrategy(string strategyId, string name)
        {
            StrategyId = strategyId;
            Name = name;
        }

        public abstract Task AnalyzeAsync(MarketData data, TradingContext context);

        protected virtual void OnSignalGenerated(Signal signal)
        {
            SignalGenerated?.Invoke(signal);
        }

        protected virtual void OnError(Exception ex)
        {
            StrategyError?.Invoke(this, ex);
        }
    }

    public class TradingContext
    {
        public string AccountId { get; }
        public decimal TotalCapital { get; private set; }
        public decimal AvailableCapital { get; private set; }
        public decimal UsedMargin { get; private set; }
        public decimal TotalPnL { get; private set; }
        public decimal DailyPnL { get; private set; }

        private readonly ConcurrentDictionary<string, Position> _positions = new();
        private readonly ConcurrentDictionary<string, Order> _activeOrders = new();
        private readonly ConcurrentBag<Trade> _todayTrades = new();

        public IReadOnlyDictionary<string, Position> Positions => _positions;
        public IReadOnlyDictionary<string, Order> ActiveOrders => _activeOrders;
        public IEnumerable<Trade> TodayTrades => _todayTrades;

        public TradingContext(string accountId, decimal initialCapital)
        {
            AccountId = accountId;
            TotalCapital = AvailableCapital = initialCapital;
        }

        public void AddPosition(Position position)
        {
            _positions[position.PositionId] = position;
            UpdateCapital();
        }

        public void RemovePosition(string positionId)
        {
            _positions.TryRemove(positionId, out _);
            UpdateCapital();
        }

        public void UpdatePosition(string symbol, decimal newPrice)
        {
            var positionsForSymbol = _positions.Values
                .Where(p => p.Symbol == symbol)
                .ToList();

            foreach (var position in positionsForSymbol)
            {
                var updatedPosition = position.WithUpdatedPrice(newPrice);
                _positions[updatedPosition.PositionId] = updatedPosition;
            }

            UpdateCapital();
        }

        public void AddOrder(Order order)
        {
            _activeOrders[order.OrderId] = order;
        }

        public void RemoveOrder(string orderId)
        {
            _activeOrders.TryRemove(orderId, out _);
        }

        public void AddTrade(Trade trade)
        {
            _todayTrades.Add(trade);
            UpdateCapital();
        }

        private void UpdateCapital()
        {
            var positionsValue = _positions.Values.Sum(p => p.MarketValue);
            var ordersMargin = _activeOrders.Values
                .Where(o => o.IsActive)
                .Sum(o => o.EstimatedValue * 0.1m); // 10% маржа

            UsedMargin = ordersMargin;
            TotalCapital = AvailableCapital + positionsValue;
            TotalPnL = _positions.Values.Sum(p => p.UnrealizedPnL);

            var today = DateTime.Today;
            DailyPnL = _todayTrades
                .Where(t => t.Timestamp.Date == today)
                .Sum(t => t.Value * (t.Side == OrderSide.Buy ? -1 : 1));
        }
    }

    // ========== ФУНКЦИИ РАСШИРЕНИЯ ==========

    public static class TradingExtensions
    {
        // 1. Расширения для коллекций баров
        public static IEnumerable<Bar> Smoothed(this IEnumerable<Bar> bars, int period)
        {
            var queue = new Queue<Bar>();

            foreach (var bar in bars)
            {
                queue.Enqueue(bar);
                if (queue.Count > period)
                    queue.Dequeue();

                if (queue.Count == period)
                {
                    var avgOpen = queue.Average(b => b.Open);
                    var avgHigh = queue.Average(b => b.High);
                    var avgLow = queue.Average(b => b.Low);
                    var avgClose = queue.Average(b => b.Close);

                    yield return bar with
                    {
                        Open = avgOpen,
                        High = avgHigh,
                        Low = avgLow,
                        Close = avgClose
                    };
                }
            }
        }

        public static decimal CalculateATR(this IEnumerable<Bar> bars, int period = 14)
        {
            var barList = bars.ToList();
            if (barList.Count < period) return 0;

            decimal sum = 0;
            for (int i = 1; i < period; i++)
            {
                var highLow = barList[i].High - barList[i].Low;
                var highClose = Math.Abs(barList[i].High - barList[i - 1].Close);
                var lowClose = Math.Abs(barList[i].Low - barList[i - 1].Close);
                sum += Math.Max(highLow, Math.Max(highClose, lowClose));
            }

            return sum / (period - 1);
        }

        public static (decimal upper, decimal middle, decimal lower)
            CalculateBollingerBands(this IEnumerable<Bar> bars, int period = 20, decimal deviations = 2)
        {
            var closes = bars.Select(b => b.Close).ToList();
            if (closes.Count < period) return (0, 0, 0);

            var sma = closes.TakeLast(period).Average();
            var stdDev = CalculateStandardDeviation(closes.TakeLast(period));

            return (
                upper: sma + deviations * stdDev,
                middle: sma,
                lower: sma - deviations * stdDev
            );
        }

        private static decimal CalculateStandardDeviation(IEnumerable<decimal> values)
        {
            var list = values.ToList();
            var avg = list.Average();
            var sum = list.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sum / list.Count));
        }

        // 2. Расширения для ордеров
        public static Order WithSlippage(this Order order, CalculateSlippageDelegate slippageCalculator, MarketData data)
        {
            if (order.Type != OrderType.Market || !order.Price.HasValue)
                return order;

            var slippage = slippageCalculator(order, data);
            var adjustedPrice = order.Side == OrderSide.Buy
                ? order.Price.Value + slippage
                : order.Price.Value - slippage;

            return order with { Price = adjustedPrice };
        }

        public static bool PassesRiskChecks(this Order order, TradingContext context,
            params RiskCheckDelegate[] riskChecks)
        {
            return riskChecks.All(check => check(order, context).Result);
        }

        // 3. Расширения для позиций
        public static decimal CalculateRiskRewardRatio(this Position position, decimal stopLoss, decimal takeProfit)
        {
            var risk = Math.Abs(position.AverageEntryPrice - stopLoss);
            var reward = Math.Abs(takeProfit - position.AverageEntryPrice);

            return risk > 0 ? reward / risk : 0;
        }

        public static bool IsProfitableAt(this Position position, decimal targetPrice, decimal minProfitPercentage = 0)
        {
            var pnl = (targetPrice - position.AverageEntryPrice) * position.Quantity;
            var percentage = position.AverageEntryPrice != 0
                ? (targetPrice - position.AverageEntryPrice) / position.AverageEntryPrice * 100
                : 0;

            return percentage >= minProfitPercentage;
        }

        // 4. Расширения для сигналов
        public static Signal CombineWith(this Signal signal, Signal other,
            Func<Signal, Signal, decimal> strengthCombiner)
        {
            if (signal.Symbol != other.Symbol)
                return signal;

            var combinedStrength = strengthCombiner(signal, other);
            var combinedType = combinedStrength > 0 ? SignalType.Buy : SignalType.Sell;

            return new Signal(
                Guid.NewGuid().ToString(),
                signal.Symbol,
                combinedType,
                Math.Abs(combinedStrength),
                DateTime.UtcNow,
                $"{signal.StrategyId}+{other.StrategyId}",
                $"Combined signal from {signal.StrategyId} and {other.StrategyId}"
            );
        }

        public static bool IsContradicting(this Signal signal, Signal other)
        {
            return signal.Symbol == other.Symbol &&
                   ((signal.Type == SignalType.Buy && other.Type == SignalType.Sell) ||
                    (signal.Type == SignalType.Sell && other.Type == SignalType.Buy));
        }
    }

    // ========== МЕТОДЫ РАСШИРЕНИЯ ДЕЛЕГАТОВ ==========

    public static class DelegateExtensions
    {
        // Композиция стратегий
        public static StrategyDelegate Compose(this StrategyDelegate first, StrategyDelegate second)
        {
            return (bars, context) =>
            {
                var firstSignals = first(bars, context);
                var secondSignals = second(bars, context);
                return firstSignals.Concat(secondSignals);
            };
        }

        // Цепочка проверок риска
        public static RiskCheckDelegate Chain(this RiskCheckDelegate first, RiskCheckDelegate second)
        {
            return async (order, context) =>
            {
                var firstResult = await first(order, context);
                return firstResult && await second(order, context);
            };
        }

        // Мемоизация для тяжелых вычислений
        public static Func<string, Task<MarketData>> Memoize(
            this Func<string, Task<MarketData>> dataFetcher, TimeSpan expiration)
        {
            var cache = new ConcurrentDictionary<string, (MarketData data, DateTime timestamp)>();

            return async symbol =>
            {
                if (cache.TryGetValue(symbol, out var cached) &&
                    DateTime.UtcNow - cached.timestamp < expiration)
                {
                    return cached.data;
                }

                var data = await dataFetcher(symbol);
                cache[symbol] = (data, DateTime.UtcNow);
                return data;
            };
        }

        // Троттлинг событий
        public static EventHandler<T> Throttle<T>(
            this EventHandler<T> handler, TimeSpan interval)
        {
            DateTime lastInvoke = DateTime.MinValue;

            return (sender, args) =>
            {
                var now = DateTime.UtcNow;
                if (now - lastInvoke >= interval)
                {
                    lastInvoke = now;
                    handler(sender, args);
                }
            };
        }

        // Фильтрация сигналов
        public static SignalEventHandler Filter(
            this SignalEventHandler handler, Predicate<Signal> filter)
        {
            return signal =>
            {
                if (filter(signal))
                    handler(signal);
            };
        }
    }

    // ========== КОНКРЕТНЫЕ РЕАЛИЗАЦИИ ==========

    public class MomentumStrategy : TradingStrategy
    {
        private readonly int _lookbackPeriod;
        private readonly decimal _threshold;

        public MomentumStrategy(string strategyId, int lookbackPeriod = 20, decimal threshold = 0.02m)
            : base(strategyId, $"Momentum Strategy ({lookbackPeriod} periods)")
        {
            _lookbackPeriod = lookbackPeriod;
            _threshold = threshold;
        }

        public override async Task AnalyzeAsync(MarketData data, TradingContext context)
        {
            try
            {
                var bars = data.RecentBars.ToList();
                if (bars.Count < _lookbackPeriod)
                    return;

                var recentBars = bars.TakeLast(_lookbackPeriod).ToList();
                var firstClose = recentBars.First().Close;
                var lastClose = recentBars.Last().Close;
                var momentum = (lastClose - firstClose) / firstClose;

                if (Math.Abs(momentum) >= _threshold)
                {
                    var signalType = momentum > 0 ? SignalType.Buy : SignalType.Sell;
                    var strength = Math.Min(Math.Abs(momentum) / _threshold, 1m);

                    var signal = new Signal(
                        Guid.NewGuid().ToString(),
                        data.Symbol,
                        signalType,
                        strength,
                        DateTime.UtcNow,
                        StrategyId,
                        $"Momentum: {momentum:P2} over {_lookbackPeriod} periods"
                    );

                    OnSignalGenerated(signal);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }
    }

    public class MeanReversionStrategy : TradingStrategy
    {
        private readonly int _period;
        private readonly decimal _deviationThreshold;

        public MeanReversionStrategy(string strategyId, int period = 20, decimal deviationThreshold = 2m)
            : base(strategyId, $"Mean Reversion Strategy ({period} periods)")
        {
            _period = period;
            _deviationThreshold = deviationThreshold;
        }

        public override async Task AnalyzeAsync(MarketData data, TradingContext context)
        {
            try
            {
                var bars = data.RecentBars.ToList();
                if (bars.Count < _period)
                    return;

                var (upper, middle, lower) = bars.CalculateBollingerBands(_period, _deviationThreshold);
                var currentPrice = data.LatestQuote.Last;

                if (currentPrice > upper)
                {
                    var deviation = (currentPrice - middle) / (upper - middle);
                    var signal = new Signal(
                        Guid.NewGuid().ToString(),
                        data.Symbol,
                        SignalType.Sell,
                        Math.Min(deviation, 1m),
                        DateTime.UtcNow,
                        StrategyId,
                        $"Price {currentPrice:C} above upper band {upper:C}"
                    );
                    OnSignalGenerated(signal);
                }
                else if (currentPrice < lower)
                {
                    var deviation = (middle - currentPrice) / (middle - lower);
                    var signal = new Signal(
                        Guid.NewGuid().ToString(),
                        data.Symbol,
                        SignalType.Buy,
                        Math.Min(deviation, 1m),
                        DateTime.UtcNow,
                        StrategyId,
                        $"Price {currentPrice:C} below lower band {lower:C}"
                    );
                    OnSignalGenerated(signal);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }
    }

    public class ExchangeSimulator : IExchange
    {
        public string ExchangeId { get; } = "SIM";
        public MarketState CurrentState { get; private set; } = MarketState.Closed;

        public event MarketStateChangedEventHandler MarketStateChanged;
        public event QuoteEventHandler QuoteUpdated;
        public event TradeEventHandler TradeExecuted;

        private readonly Random _random = new();
        private readonly Dictionary<string, decimal> _prices = new();
        private readonly ConcurrentDictionary<string, Order> _orders = new();

        public ExchangeSimulator()
        {
            // Инициализация цен для нескольких символов
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
            foreach (var symbol in symbols)
            {
                _prices[symbol] = 100 + (decimal)_random.NextDouble() * 900;
            }

            // Запуск симуляции
            Task.Run(SimulateMarket);
        }

        public async Task<Order> SubmitOrderAsync(Order order)
        {
            await Task.Delay(50); // Имитация задержки сети

            // Симуляция исполнения ордера
            var fillPrice = order.Type == OrderType.Market
                ? order.Side == OrderSide.Buy
                    ? _prices[order.Symbol] * 1.001m  // Проскальзывание при покупке
                    : _prices[order.Symbol] * 0.999m  // Проскальзывание при продаже
                : order.Price.Value;

            var filledOrder = order with
            {
                Status = OrderStatus.Filled,
                FilledQuantity = order.Quantity,
                AveragePrice = fillPrice
            };

            _orders[order.OrderId] = filledOrder;

            // Генерация сделки
            var trade = new Trade(
                Guid.NewGuid().ToString(),
                order.Symbol,
                fillPrice,
                order.Quantity,
                order.Side,
                DateTime.UtcNow,
                "SIM_BUYER",
                "SIM_SELLER"
            );

            TradeExecuted?.Invoke(trade);

            return filledOrder;
        }

        public Task<bool> CancelOrderAsync(string orderId)
        {
            if (_orders.TryGetValue(orderId, out var order) && order.IsActive)
            {
                _orders[orderId] = order with { Status = OrderStatus.Cancelled };
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<MarketData> GetMarketDataAsync(string symbol)
        {
            if (!_prices.ContainsKey(symbol))
                throw new ArgumentException($"Symbol {symbol} not found");

            var price = _prices[symbol];
            var quote = new Quote(
                symbol,
                price * 0.9995m,  // Bid
                price * 1.0005m,  // Ask
                price,            // Last
                _random.Next(1000, 10000),  // Volume
                DateTime.UtcNow
            );

            // Генерация бара
            var bar = new Bar(
                symbol,
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow,
                price * 0.995m,
                price * 1.005m,
                price * 0.99m,
                price,
                _random.Next(5000, 50000)
            );

            var bars = Enumerable.Range(0, 100)
                .Select(i => new Bar(
                    symbol,
                    DateTime.UtcNow.AddMinutes(-i - 1),
                    DateTime.UtcNow.AddMinutes(-i),
                    price * (0.95m + (decimal)_random.NextDouble() * 0.1m),
                    price * (0.96m + (decimal)_random.NextDouble() * 0.1m),
                    price * (0.94m + (decimal)_random.NextDouble() * 0.1m),
                    price * (0.95m + (decimal)_random.NextDouble() * 0.1m),
                    _random.Next(1000, 10000)
                ));

            var marketData = new MarketData(
                symbol,
                quote,
                bar,
                bars,
                (decimal)_random.NextDouble() * 0.5m,  // Volatility
                1.0m + (decimal)_random.NextDouble() * 0.5m  // Volume ratio
            );

            return Task.FromResult(marketData);
        }

        public Task<IEnumerable<Bar>> GetHistoricalDataAsync(string symbol, BarTimeFrame timeframe, int periods)
        {
            var bars = Enumerable.Range(0, periods)
                .Select(i =>
                {
                    var basePrice = 100 + (decimal)_random.NextDouble() * 900;
                    return new Bar(
                        symbol,
                        DateTime.UtcNow.AddMinutes(-i * GetMinutes(timeframe)),
                        DateTime.UtcNow.AddMinutes(-(i - 1) * GetMinutes(timeframe)),
                        basePrice * (0.95m + (decimal)_random.NextDouble() * 0.1m),
                        basePrice * (0.96m + (decimal)_random.NextDouble() * 0.1m),
                        basePrice * (0.94m + (decimal)_random.NextDouble() * 0.1m),
                        basePrice * (0.95m + (decimal)_random.NextDouble() * 0.1m),
                        _random.Next(1000, 10000),
                        timeframe
                    );
                });

            return Task.FromResult(bars);
        }

        private int GetMinutes(BarTimeFrame timeframe) => timeframe switch
        {
            BarTimeFrame.OneMinute => 1,
            BarTimeFrame.FiveMinutes => 5,
            BarTimeFrame.FifteenMinutes => 15,
            BarTimeFrame.ThirtyMinutes => 30,
            BarTimeFrame.OneHour => 60,
            BarTimeFrame.FourHours => 240,
            BarTimeFrame.OneDay => 1440,
            _ => 1
        };

        private async Task SimulateMarket()
        {
            while (true)
            {
                // Изменение состояния рынка
                if (DateTime.UtcNow.Hour == 9 && CurrentState != MarketState.Open)
                {
                    var oldState = CurrentState;
                    CurrentState = MarketState.Open;
                    MarketStateChanged?.Invoke(oldState, CurrentState);
                }
                else if (DateTime.UtcNow.Hour == 16 && CurrentState != MarketState.Closed)
                {
                    var oldState = CurrentState;
                    CurrentState = MarketState.Closed;
                    MarketStateChanged?.Invoke(oldState, CurrentState);
                }

                // Обновление цен
                if (CurrentState == MarketState.Open)
                {
                    foreach (var symbol in _prices.Keys.ToList())
                    {
                        var change = (_random.NextDouble() - 0.5) * 0.02; // ±2%
                        _prices[symbol] *= (decimal)(1 + change);

                        var quote = new Quote(
                            symbol,
                            _prices[symbol] * 0.9995m,
                            _prices[symbol] * 1.0005m,
                            _prices[symbol],
                            _random.Next(1000, 10000),
                            DateTime.UtcNow
                        );

                        QuoteUpdated?.Invoke(quote);
                    }
                }

                await Task.Delay(1000); // Обновление каждую секунду
            }
        }
    }

    public class TradingEngine
    {
        private readonly IExchange _exchange;
        private readonly TradingContext _context;
        private readonly List<TradingStrategy> _strategies = new();
        private readonly List<Order> _orderHistory = new();

        public event OrderEventHandler OrderSubmitted;
        public event OrderEventHandler OrderFilled;
        public event PositionEventHandler PositionOpened;
        public event PositionEventHandler PositionUpdated;
        public event PositionEventHandler PositionClosed;
        public event EventHandler<string> TradingError;

        public TradingEngine(IExchange exchange, TradingContext context)
        {
            _exchange = exchange;
            _context = context;

            // Подписка на события биржи
            _exchange.QuoteUpdated += OnQuoteUpdated;
            _exchange.TradeExecuted += OnTradeExecuted;
            _exchange.MarketStateChanged += OnMarketStateChanged;
        }

        public void AddStrategy(TradingStrategy strategy)
        {
            strategy.SignalGenerated += OnSignalGenerated;
            strategy.StrategyError += OnStrategyError;
            _strategies.Add(strategy);
        }

        public async Task RunAsync()
        {
            while (true)
            {
                if (_exchange.CurrentState == MarketState.Open)
                {
                    await ExecuteTradingCycle();
                }

                await Task.Delay(5000); // Цикл каждые 5 секунд
            }
        }

        private async Task ExecuteTradingCycle()
        {
            foreach (var symbol in new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" })
            {
                var marketData = await _exchange.GetMarketDataAsync(symbol);

                foreach (var strategy in _strategies.Where(s => s.IsActive))
                {
                    await strategy.AnalyzeAsync(marketData, _context);
                }

                // Обновление позиций
                if (_context.Positions.Values.Any(p => p.Symbol == symbol))
                {
                    _context.UpdatePosition(symbol, marketData.LatestQuote.Last);
                }
            }
        }

        public async Task<Order> SubmitOrderAsync(Order order)
        {
            try
            {
                OrderSubmitted?.Invoke(order);

                // Проверки риска
                var riskChecks = new RiskCheckDelegate[]
                {
                    CheckMarginRequirements,
                    CheckPositionLimits,
                    CheckDailyLossLimit
                };

                if (!order.PassesRiskChecks(_context, riskChecks))
                {
                    throw new InvalidOperationException("Order failed risk checks");
                }

                // Расчет проскальзывания
                var marketData = await _exchange.GetMarketDataAsync(order.Symbol);
                var orderWithSlippage = order.WithSlippage(CalculateDynamicSlippage, marketData);

                // Отправка ордера
                var filledOrder = await _exchange.SubmitOrderAsync(orderWithSlippage);
                _orderHistory.Add(filledOrder);
                _context.AddOrder(filledOrder);

                // Обновление позиции
                UpdatePositionFromOrder(filledOrder);

                OrderFilled?.Invoke(filledOrder);
                return filledOrder;
            }
            catch (Exception ex)
            {
                TradingError?.Invoke(this, ex.Message);
                throw;
            }
        }

        private void UpdatePositionFromOrder(Order filledOrder)
        {
            var existingPosition = _context.Positions.Values
                .FirstOrDefault(p => p.Symbol == filledOrder.Symbol);

            if (existingPosition == null)
            {
                // Новая позиция
                var position = new Position(
                    Guid.NewGuid().ToString(),
                    filledOrder.Symbol,
                    filledOrder.Side == OrderSide.Buy ? filledOrder.Quantity : -filledOrder.Quantity,
                    filledOrder.AveragePrice,
                    filledOrder.AveragePrice,
                    DateTime.UtcNow
                );

                _context.AddPosition(position);
                PositionOpened?.Invoke(position);
            }
            else
            {
                // Обновление существующей позиции
                var newQuantity = existingPosition.Quantity +
                    (filledOrder.Side == OrderSide.Buy ? filledOrder.Quantity : -filledOrder.Quantity);

                Position updatedPosition;

                if (newQuantity == 0)
                {
                    // Позиция закрыта
                    _context.RemovePosition(existingPosition.PositionId);
                    PositionClosed?.Invoke(existingPosition);
                    return;
                }
                else if (Math.Sign(newQuantity) == Math.Sign(existingPosition.Quantity))
                {
                    // Увеличение существующей позиции в том же направлении
                    updatedPosition = existingPosition.WithIncreasedQuantity(
                        filledOrder.Side == OrderSide.Buy ? filledOrder.Quantity : -filledOrder.Quantity,
                        filledOrder.AveragePrice
                    );
                }
                else
                {
                    // Частичное или полное закрытие позиции (с возможной инверсией)
                    if (Math.Abs(newQuantity) < Math.Abs(existingPosition.Quantity))
                    {
                        // Частичное закрытие
                        updatedPosition = existingPosition.WithDecreasedQuantity(
                            Math.Abs(filledOrder.Quantity) * Math.Sign(existingPosition.Quantity)
                        );
                    }
                    else
                    {
                        // Инверсия позиции (продажа больше, чем есть в лонге, или покупка больше, чем в шорте)
                        updatedPosition = new Position(
                            Guid.NewGuid().ToString(),
                            filledOrder.Symbol,
                            newQuantity,
                            filledOrder.AveragePrice,
                            filledOrder.AveragePrice,
                            DateTime.UtcNow
                        );
                    }
                }

                _context.AddPosition(updatedPosition);
                PositionUpdated?.Invoke(updatedPosition);
            }
        }

        private decimal CalculateDynamicSlippage(Order order, MarketData marketData)
        {
            var baseSlippage = 0.001m; // 0.1%
            var volumeImpact = order.Quantity / marketData.LatestQuote.Volume * 10;
            var volatilityImpact = marketData.Volatility * 0.5m;

            return baseSlippage + volumeImpact + volatilityImpact;
        }

        private async Task<bool> CheckMarginRequirements(Order order, TradingContext context)
        {
            var requiredMargin = order.EstimatedValue * 0.1m; // 10% маржа
            return context.AvailableCapital >= requiredMargin;
        }

        private async Task<bool> CheckPositionLimits(Order order, TradingContext context)
        {
            var symbolPositions = context.Positions.Values
                .Where(p => p.Symbol == order.Symbol)
                .Sum(p => p.Quantity);

            var newPosition = symbolPositions + (order.Side == OrderSide.Buy ? order.Quantity : -order.Quantity);
            return Math.Abs(newPosition) <= 1000; // Максимум 1000 акций на символ
        }

        private async Task<bool> CheckDailyLossLimit(Order order, TradingContext context)
        {
            return context.DailyPnL >= -context.TotalCapital * 0.02m; // Максимум 2% потерь в день
        }

        private void OnSignalGenerated(Signal signal)
        {
            Console.WriteLine($"Signal: {signal.Description}");

            if (signal.IsActionable && signal.IsStrongSignal)
            {
                var order = CreateOrderFromSignal(signal);
                _ = SubmitOrderAsync(order);
            }
        }

        private Order CreateOrderFromSignal(Signal signal)
        {
            var side = signal.Type switch
            {
                SignalType.Buy or SignalType.StrongBuy => OrderSide.Buy,
                SignalType.Sell or SignalType.StrongSell => OrderSide.Sell,
                _ => throw new InvalidOperationException("Invalid signal type for order")
            };

            return new Order(
                Guid.NewGuid().ToString(),
                signal.Symbol,
                OrderType.Market,
                side,
                CalculateOrderQuantity(signal),
                null,
                null,
                DateTime.UtcNow,
                _context.AccountId,
                signal.StrategyId
            );
        }

        private decimal CalculateOrderQuantity(Signal signal)
        {
            var baseQuantity = _context.TotalCapital * signal.Strength * 0.01m; // 1% капитала на сигнал
            var price = _exchange.GetMarketDataAsync(signal.Symbol).Result.LatestQuote.Last;

            return Math.Floor(baseQuantity / price);
        }

        private void OnQuoteUpdated(Quote quote)
        {
            // Обновление цен в позициях
            _context.UpdatePosition(quote.Symbol, quote.Last);
        }

        private void OnTradeExecuted(Trade trade)
        {
            _context.AddTrade(trade);
        }

        private void OnMarketStateChanged(MarketState oldState, MarketState newState)
        {
            Console.WriteLine($"Market state changed: {oldState} -> {newState}");
        }

        private void OnStrategyError(object sender, Exception ex)
        {
            TradingError?.Invoke(this, $"Strategy error: {ex.Message}");
        }
    }

    // ========== ДЕМОНСТРАЦИОННАЯ ПРОГРАММА ==========

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("=== ADVANCED TRADING SYSTEM ===");
            Console.WriteLine("Нажмите Enter для запуска симуляции...");
            Console.ReadLine();

            // Создание компонентов
            var exchange = new ExchangeSimulator();
            var context = new TradingContext("ACC001", 100000m); // 100,000 начального капитала
            var engine = new TradingEngine(exchange, context);

            // Настройка стратегий
            var momentumStrategy = new MomentumStrategy("MOM_01");
            var meanReversionStrategy = new MeanReversionStrategy("MR_01");

            engine.AddStrategy(momentumStrategy);
            engine.AddStrategy(meanReversionStrategy);

            // Подписка на события
            engine.OrderSubmitted += order =>
                Console.WriteLine($"Order submitted: {order.Symbol} {order.Side} {order.Quantity} @ {order.Price:C}");

            engine.OrderFilled += order =>
                Console.WriteLine($"Order filled: {order.Symbol} {order.Side} {order.FilledQuantity} @ {order.AveragePrice:C}");

            engine.PositionOpened += position =>
                Console.WriteLine($"Position opened: {position.Symbol} {position.Quantity} @ {position.AverageEntryPrice:C}");

            engine.PositionUpdated += position =>
                Console.WriteLine($"Position updated: {position.Symbol} {position.Quantity} PnL: {position.UnrealizedPnL:C} ({position.UnrealizedPnLPercentage:P2})");

            engine.PositionClosed += position =>
                Console.WriteLine($"Position closed: {position.Symbol} Final PnL: {position.UnrealizedPnL:C}");

            engine.TradingError += (sender, error) =>
                Console.WriteLine($"Trading error: {error}");

            // Запуск двигателя
            var engineTask = engine.RunAsync();

            // Демонстрация отправки ордера
            await Task.Delay(5000); // Ждем открытия рынка

            var sampleOrder = new Order(
                "TEST_001",
                "AAPL",
                OrderType.Market,
                OrderSide.Buy,
                10,
                null,
                null,
                DateTime.UtcNow,
                context.AccountId
            );

            try
            {
                await engine.SubmitOrderAsync(sampleOrder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Order failed: {ex.Message}");
            }

            // Мониторинг в реальном времени
            var monitorTask = Task.Run(async () =>
            {
                while (true)
                {
                    Console.WriteLine("\n=== ТОРГОВЫЙ МОНИТОР ===");
                    Console.WriteLine($"Капитал: {context.TotalCapital:C}");
                    Console.WriteLine($"Доступно: {context.AvailableCapital:C}");
                    Console.WriteLine($"Маржа: {context.UsedMargin:C}");
                    Console.WriteLine($"Общий PnL: {context.TotalPnL:C}");
                    Console.WriteLine($"Дневной PnL: {context.DailyPnL:C}");

                    Console.WriteLine("\nПозиции:");
                    foreach (var position in context.Positions.Values)
                    {
                        Console.WriteLine($"  {position.Symbol}: {position.Quantity} @ {position.AverageEntryPrice:C} " +
                                         $"(Текущая: {position.CurrentPrice:C}, PnL: {position.UnrealizedPnL:C})");
                    }

                    Console.WriteLine("\nАктивные ордера:");
                    foreach (var order in context.ActiveOrders.Values)
                    {
                        Console.WriteLine($"  {order.Symbol} {order.Side} {order.RemainingQuantity}/{order.Quantity} " +
                                         $"@{order.Price:C} ({order.Status})");
                    }

                    await Task.Delay(10000); // Обновление каждые 10 секунд
                }
            });

            // Демонстрация функций расширения
            await Task.Delay(10000);
            Console.WriteLine("\n=== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ ===");

            var marketData = await exchange.GetMarketDataAsync("AAPL");
            var bars = marketData.RecentBars.ToList();

            Console.WriteLine($"ATR (14 периодов): {bars.CalculateATR(14):C}");
            var (upper, middle, lower) = bars.CalculateBollingerBands(20, 2);
            Console.WriteLine($"Bollinger Bands: Upper={upper:C}, Middle={middle:C}, Lower={lower:C}");

            var smoothedBars = bars.Smoothed(5).Take(3);
            foreach (var bar in smoothedBars)
            {
                Console.WriteLine($"Smoothed Bar: O={bar.Open:C}, H={bar.High:C}, L={bar.Low:C}, C={bar.Close:C}");
            }

            // Комбинирование стратегий через делегаты
            Console.WriteLine("\n=== КОМБИНИРОВАНИЕ СТРАТЕГИЙ ===");

            StrategyDelegate momentumLogic = (bars, ctx) =>
            {
                var signals = new List<Signal>();
                // Логика импульса
                return signals;
            };

            StrategyDelegate meanReversionLogic = (bars, ctx) =>
            {
                var signals = new List<Signal>();
                // Логика возврата к среднему
                return signals;
            };

            var combinedStrategy = momentumLogic.Compose(meanReversionLogic);
            var combinedSignals = combinedStrategy(bars, context);

            Console.WriteLine($"Сгенерировано сигналов: {combinedSignals.Count()}");

            Console.WriteLine("\nСимуляция запущена. Нажмите Ctrl+C для остановки...");

            await Task.WhenAny(engineTask, monitorTask);
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Collections.Concurrent;

//namespace AdvancedTradingSystem
//{
//    // ========== ОСНОВНЫЕ ТИПЫ ДАННЫХ ==========

//    public enum OrderSide { Buy, Sell }
//    public enum OrderType { Market, Limit, Stop }
//    public enum OrderStatus { Pending, Filled, PartiallyFilled, Cancelled, Rejected }
//    public enum SignalType { Buy, Sell, Hold, StrongBuy, StrongSell }
//    public enum MarketState { PreMarket, Open, PostMarket, Closed, Halted }

//    // ========== КАСТОМНЫЕ ДЕЛЕГАТЫ ==========

//    public delegate void OrderEventHandler(Order order);
//    public delegate void TradeEventHandler(Trade trade);
//    public delegate void QuoteEventHandler(Quote quote);
//    public delegate void SignalEventHandler(Signal signal);
//    public delegate void PositionEventHandler(Position position);
//    public delegate Task<bool> RiskCheckDelegate(Order order, TradingContext context);
//    public delegate decimal CalculateSlippageDelegate(Order order, MarketData marketData);
//    public delegate IEnumerable<Signal> StrategyDelegate(IEnumerable<Bar> bars, TradingContext context);
//    public delegate void MarketStateChangedEventHandler(MarketState oldState, MarketState newState);

//    // ========== КЛАССЫ ДАННЫХ ==========

//    public record Quote(string Symbol, decimal Bid, decimal Ask, decimal Last, decimal Volume, DateTime Timestamp)
//    {
//        public decimal Spread => Ask - Bid;
//        public decimal MidPrice => (Bid + Ask) / 2;
//    }

//    public record Bar(string Symbol, DateTime OpenTime, DateTime CloseTime,
//        decimal Open, decimal High, decimal Low, decimal Close, decimal Volume,
//        BarTimeFrame TimeFrame = BarTimeFrame.OneMinute)
//    {
//        public bool IsBullish => Close > Open;
//        public bool IsBearish => Close < Open;
//        public decimal BodySize => Math.Abs(Close - Open);
//        public decimal TotalRange => High - Low;
//        public decimal UpperShadow => High - Math.Max(Open, Close);
//        public decimal LowerShadow => Math.Min(Open, Close) - Low;
//    }

//    public enum BarTimeFrame
//    {
//        OneSecond, FiveSeconds, FifteenSeconds, ThirtySeconds,
//        OneMinute, FiveMinutes, FifteenMinutes, ThirtyMinutes,
//        OneHour, FourHours, OneDay, OneWeek, OneMonth
//    }

//    public record Trade(string TradeId, string Symbol, decimal Price, decimal Quantity,
//        OrderSide Side, DateTime Timestamp, string BuyerId, string SellerId)
//    {
//        public decimal Value => Price * Quantity;
//        public bool IsBlockTrade => Quantity >= 10000;
//    }

//    public record Order(string OrderId, string Symbol, OrderType Type, OrderSide Side,
//        decimal Quantity, decimal? Price, decimal? StopPrice, DateTime CreatedTime,
//        string AccountId, string StrategyId = null)
//    {
//        public OrderStatus Status { get; set; } = OrderStatus.Pending;
//        public decimal FilledQuantity { get; set; }
//        public decimal AveragePrice { get; set; }
//        public decimal RemainingQuantity => Quantity - FilledQuantity;
//        public bool IsFullyFilled => Status == OrderStatus.Filled;
//        public bool IsActive => Status == OrderStatus.Pending || Status == OrderStatus.PartiallyFilled;

//        public decimal EstimatedValue => Price.HasValue ? Price.Value * Quantity : 0;
//        public string FullId => $"{OrderId}_{Symbol}_{Side}_{CreatedTime:yyyyMMddHHmmss}";
//    }

//    public record Position(string PositionId, string Symbol, decimal Quantity,
//        decimal AverageEntryPrice, decimal CurrentPrice, DateTime OpenedTime)
//    {
//        public decimal MarketValue => Quantity * CurrentPrice;
//        public decimal CostBasis => Quantity * AverageEntryPrice;
//        public decimal UnrealizedPnL => MarketValue - CostBasis;
//        public decimal UnrealizedPnLPercentage => CostBasis != 0 ? (UnrealizedPnL / CostBasis) * 100 : 0;
//        public bool IsLong => Quantity > 0;
//        public bool IsShort => Quantity < 0;

//        public void UpdatePrice(decimal newPrice) =>
//            this = this with { CurrentPrice = newPrice };
//    }

//    public record Signal(string SignalId, string Symbol, SignalType Type, decimal Strength,
//        DateTime GeneratedTime, string StrategyId, string Reason = null)
//    {
//        public bool IsActionable => Type != SignalType.Hold;
//        public decimal ConfidenceScore => Math.Clamp(Strength, 0, 1);
//        public bool IsStrongSignal => Strength >= 0.7m;

//        public string Description => $"{Symbol}: {Type} (Strength: {Strength:P0}) - {Reason}";
//    }

//    public record MarketData(string Symbol, Quote LatestQuote, Bar LatestBar,
//        IEnumerable<Bar> RecentBars, decimal Volatility, decimal VolumeRatio);

//    // ========== СИСТЕМНЫЕ КОМПОНЕНТЫ ==========

//    public interface IExchange
//    {
//        string ExchangeId { get; }
//        MarketState CurrentState { get; }
//        event MarketStateChangedEventHandler MarketStateChanged;
//        event QuoteEventHandler QuoteUpdated;
//        event TradeEventHandler TradeExecuted;

//        Task<Order> SubmitOrderAsync(Order order);
//        Task<bool> CancelOrderAsync(string orderId);
//        Task<MarketData> GetMarketDataAsync(string symbol);
//        Task<IEnumerable<Bar>> GetHistoricalDataAsync(string symbol, BarTimeFrame timeframe, int periods);
//    }

//    public abstract class TradingStrategy
//    {
//        public string StrategyId { get; }
//        public string Name { get; }
//        public decimal CapitalAllocation { get; set; }
//        public bool IsActive { get; set; } = true;

//        public event SignalEventHandler SignalGenerated;
//        public event EventHandler<Exception> StrategyError;

//        protected TradingStrategy(string strategyId, string name)
//        {
//            StrategyId = strategyId;
//            Name = name;
//        }

//        public abstract Task AnalyzeAsync(MarketData data, TradingContext context);

//        protected virtual void OnSignalGenerated(Signal signal)
//        {
//            SignalGenerated?.Invoke(signal);
//        }

//        protected virtual void OnError(Exception ex)
//        {
//            StrategyError?.Invoke(this, ex);
//        }
//    }

//    public class TradingContext
//    {
//        public string AccountId { get; }
//        public decimal TotalCapital { get; private set; }
//        public decimal AvailableCapital { get; private set; }
//        public decimal UsedMargin { get; private set; }
//        public decimal TotalPnL { get; private set; }
//        public decimal DailyPnL { get; private set; }

//        private readonly ConcurrentDictionary<string, Position> _positions = new();
//        private readonly ConcurrentDictionary<string, Order> _activeOrders = new();
//        private readonly ConcurrentBag<Trade> _todayTrades = new();

//        public IReadOnlyDictionary<string, Position> Positions => _positions;
//        public IReadOnlyDictionary<string, Order> ActiveOrders => _activeOrders;
//        public IEnumerable<Trade> TodayTrades => _todayTrades;

//        public TradingContext(string accountId, decimal initialCapital)
//        {
//            AccountId = accountId;
//            TotalCapital = AvailableCapital = initialCapital;
//        }

//        public void AddPosition(Position position)
//        {
//            _positions[position.PositionId] = position;
//            UpdateCapital();
//        }

//        public void UpdatePosition(string symbol, decimal newPrice)
//        {
//            var position = _positions.Values.FirstOrDefault(p => p.Symbol == symbol);
//            if (position != null)
//            {
//                position.UpdatePrice(newPrice);
//                UpdateCapital();
//            }
//        }

//        public void AddOrder(Order order)
//        {
//            _activeOrders[order.OrderId] = order;
//        }

//        public void RemoveOrder(string orderId)
//        {
//            _activeOrders.TryRemove(orderId, out _);
//        }

//        public void AddTrade(Trade trade)
//        {
//            _todayTrades.Add(trade);
//            UpdateCapital();
//        }

//        private void UpdateCapital()
//        {
//            var positionsValue = _positions.Values.Sum(p => p.MarketValue);
//            var ordersMargin = _activeOrders.Values
//                .Where(o => o.IsActive)
//                .Sum(o => o.EstimatedValue * 0.1m); // 10% маржа

//            UsedMargin = ordersMargin;
//            TotalCapital = AvailableCapital + positionsValue;
//            TotalPnL = _positions.Values.Sum(p => p.UnrealizedPnL);

//            var today = DateTime.Today;
//            DailyPnL = _todayTrades
//                .Where(t => t.Timestamp.Date == today)
//                .Sum(t => t.Value * (t.Side == OrderSide.Buy ? -1 : 1));
//        }
//    }

//    // ========== ФУНКЦИИ РАСШИРЕНИЯ ==========

//    public static class TradingExtensions
//    {
//        // 1. Расширения для коллекций баров
//        public static IEnumerable<Bar> Smoothed(this IEnumerable<Bar> bars, int period)
//        {
//            var queue = new Queue<Bar>();

//            foreach (var bar in bars)
//            {
//                queue.Enqueue(bar);
//                if (queue.Count > period)
//                    queue.Dequeue();

//                if (queue.Count == period)
//                {
//                    var avgOpen = queue.Average(b => b.Open);
//                    var avgHigh = queue.Average(b => b.High);
//                    var avgLow = queue.Average(b => b.Low);
//                    var avgClose = queue.Average(b => b.Close);

//                    yield return bar with
//                    {
//                        Open = avgOpen,
//                        High = avgHigh,
//                        Low = avgLow,
//                        Close = avgClose
//                    };
//                }
//            }
//        }

//        public static decimal CalculateATR(this IEnumerable<Bar> bars, int period = 14)
//        {
//            var barList = bars.ToList();
//            if (barList.Count < period) return 0;

//            decimal sum = 0;
//            for (int i = 1; i < period; i++)
//            {
//                var highLow = barList[i].High - barList[i].Low;
//                var highClose = Math.Abs(barList[i].High - barList[i - 1].Close);
//                var lowClose = Math.Abs(barList[i].Low - barList[i - 1].Close);
//                sum += Math.Max(highLow, Math.Max(highClose, lowClose));
//            }

//            return sum / (period - 1);
//        }

//        public static (decimal upper, decimal middle, decimal lower)
//            CalculateBollingerBands(this IEnumerable<Bar> bars, int period = 20, decimal deviations = 2)
//        {
//            var closes = bars.Select(b => b.Close).ToList();
//            if (closes.Count < period) return (0, 0, 0);

//            var sma = closes.TakeLast(period).Average();
//            var stdDev = CalculateStandardDeviation(closes.TakeLast(period));

//            return (
//                upper: sma + deviations * stdDev,
//                middle: sma,
//                lower: sma - deviations * stdDev
//            );
//        }

//        private static decimal CalculateStandardDeviation(IEnumerable<decimal> values)
//        {
//            var list = values.ToList();
//            var avg = list.Average();
//            var sum = list.Sum(v => (v - avg) * (v - avg));
//            return (decimal)Math.Sqrt((double)(sum / list.Count));
//        }

//        // 2. Расширения для ордеров
//        public static Order WithSlippage(this Order order, CalculateSlippageDelegate slippageCalculator, MarketData data)
//        {
//            if (order.Type != OrderType.Market || !order.Price.HasValue)
//                return order;

//            var slippage = slippageCalculator(order, data);
//            var adjustedPrice = order.Side == OrderSide.Buy
//                ? order.Price.Value + slippage
//                : order.Price.Value - slippage;

//            return order with { Price = adjustedPrice };
//        }

//        public static bool PassesRiskChecks(this Order order, TradingContext context,
//            params RiskCheckDelegate[] riskChecks)
//        {
//            return riskChecks.All(check => check(order, context).Result);
//        }

//        // 3. Расширения для позиций
//        public static decimal CalculateRiskRewardRatio(this Position position, decimal stopLoss, decimal takeProfit)
//        {
//            var risk = Math.Abs(position.AverageEntryPrice - stopLoss);
//            var reward = Math.Abs(takeProfit - position.AverageEntryPrice);

//            return risk > 0 ? reward / risk : 0;
//        }

//        public static bool IsProfitableAt(this Position position, decimal targetPrice, decimal minProfitPercentage = 0)
//        {
//            var pnl = (targetPrice - position.AverageEntryPrice) * position.Quantity;
//            var percentage = position.AverageEntryPrice != 0
//                ? (targetPrice - position.AverageEntryPrice) / position.AverageEntryPrice * 100
//                : 0;

//            return percentage >= minProfitPercentage;
//        }

//        // 4. Расширения для сигналов
//        public static Signal CombineWith(this Signal signal, Signal other,
//            Func<Signal, Signal, decimal> strengthCombiner)
//        {
//            if (signal.Symbol != other.Symbol)
//                return signal;

//            var combinedStrength = strengthCombiner(signal, other);
//            var combinedType = combinedStrength > 0 ? SignalType.Buy : SignalType.Sell;

//            return new Signal(
//                Guid.NewGuid().ToString(),
//                signal.Symbol,
//                combinedType,
//                Math.Abs(combinedStrength),
//                DateTime.UtcNow,
//                $"{signal.StrategyId}+{other.StrategyId}",
//                $"Combined signal from {signal.StrategyId} and {other.StrategyId}"
//            );
//        }

//        public static bool IsContradicting(this Signal signal, Signal other)
//        {
//            return signal.Symbol == other.Symbol &&
//                   ((signal.Type == SignalType.Buy && other.Type == SignalType.Sell) ||
//                    (signal.Type == SignalType.Sell && other.Type == SignalType.Buy));
//        }
//    }

//    // ========== МЕТОДЫ РАСШИРЕНИЯ ДЕЛЕГАТОВ ==========

//    public static class DelegateExtensions
//    {
//        // Композиция стратегий
//        public static StrategyDelegate Compose(this StrategyDelegate first, StrategyDelegate second)
//        {
//            return (bars, context) =>
//            {
//                var firstSignals = first(bars, context);
//                var secondSignals = second(bars, context);
//                return firstSignals.Concat(secondSignals);
//            };
//        }

//        // Цепочка проверок риска
//        public static RiskCheckDelegate Chain(this RiskCheckDelegate first, RiskCheckDelegate second)
//        {
//            return async (order, context) =>
//            {
//                var firstResult = await first(order, context);
//                return firstResult && await second(order, context);
//            };
//        }

//        // Мемоизация для тяжелых вычислений
//        public static Func<string, Task<MarketData>> Memoize(
//            this Func<string, Task<MarketData>> dataFetcher, TimeSpan expiration)
//        {
//            var cache = new ConcurrentDictionary<string, (MarketData data, DateTime timestamp)>();

//            return async symbol =>
//            {
//                if (cache.TryGetValue(symbol, out var cached) &&
//                    DateTime.UtcNow - cached.timestamp < expiration)
//                {
//                    return cached.data;
//                }

//                var data = await dataFetcher(symbol);
//                cache[symbol] = (data, DateTime.UtcNow);
//                return data;
//            };
//        }

//        // Троттлинг событий
//        public static EventHandler<T> Throttle<T>(
//            this EventHandler<T> handler, TimeSpan interval)
//        {
//            DateTime lastInvoke = DateTime.MinValue;

//            return (sender, args) =>
//            {
//                var now = DateTime.UtcNow;
//                if (now - lastInvoke >= interval)
//                {
//                    lastInvoke = now;
//                    handler(sender, args);
//                }
//            };
//        }

//        // Фильтрация сигналов
//        public static SignalEventHandler Filter(
//            this SignalEventHandler handler, Predicate<Signal> filter)
//        {
//            return signal =>
//            {
//                if (filter(signal))
//                    handler(signal);
//            };
//        }
//    }

//    // ========== КОНКРЕТНЫЕ РЕАЛИЗАЦИИ ==========

//    public class MomentumStrategy : TradingStrategy
//    {
//        private readonly int _lookbackPeriod;
//        private readonly decimal _threshold;

//        public MomentumStrategy(string strategyId, int lookbackPeriod = 20, decimal threshold = 0.02m)
//            : base(strategyId, $"Momentum Strategy ({lookbackPeriod} periods)")
//        {
//            _lookbackPeriod = lookbackPeriod;
//            _threshold = threshold;
//        }

//        public override async Task AnalyzeAsync(MarketData data, TradingContext context)
//        {
//            try
//            {
//                var bars = data.RecentBars.ToList();
//                if (bars.Count < _lookbackPeriod)
//                    return;

//                var recentBars = bars.TakeLast(_lookbackPeriod).ToList();
//                var firstClose = recentBars.First().Close;
//                var lastClose = recentBars.Last().Close;
//                var momentum = (lastClose - firstClose) / firstClose;

//                if (Math.Abs(momentum) >= _threshold)
//                {
//                    var signalType = momentum > 0 ? SignalType.Buy : SignalType.Sell;
//                    var strength = Math.Min(Math.Abs(momentum) / _threshold, 1m);

//                    var signal = new Signal(
//                        Guid.NewGuid().ToString(),
//                        data.Symbol,
//                        signalType,
//                        strength,
//                        DateTime.UtcNow,
//                        StrategyId,
//                        $"Momentum: {momentum:P2} over {_lookbackPeriod} periods"
//                    );

//                    OnSignalGenerated(signal);
//                }
//            }
//            catch (Exception ex)
//            {
//                OnError(ex);
//            }
//        }
//    }

//    public class MeanReversionStrategy : TradingStrategy
//    {
//        private readonly int _period;
//        private readonly decimal _deviationThreshold;

//        public MeanReversionStrategy(string strategyId, int period = 20, decimal deviationThreshold = 2m)
//            : base(strategyId, $"Mean Reversion Strategy ({period} periods)")
//        {
//            _period = period;
//            _deviationThreshold = deviationThreshold;
//        }

//        public override async Task AnalyzeAsync(MarketData data, TradingContext context)
//        {
//            try
//            {
//                var bars = data.RecentBars.ToList();
//                if (bars.Count < _period)
//                    return;

//                var (upper, middle, lower) = bars.CalculateBollingerBands(_period, _deviationThreshold);
//                var currentPrice = data.LatestQuote.Last;

//                if (currentPrice > upper)
//                {
//                    var deviation = (currentPrice - middle) / (upper - middle);
//                    var signal = new Signal(
//                        Guid.NewGuid().ToString(),
//                        data.Symbol,
//                        SignalType.Sell,
//                        Math.Min(deviation, 1m),
//                        DateTime.UtcNow,
//                        StrategyId,
//                        $"Price {currentPrice:C} above upper band {upper:C}"
//                    );
//                    OnSignalGenerated(signal);
//                }
//                else if (currentPrice < lower)
//                {
//                    var deviation = (middle - currentPrice) / (middle - lower);
//                    var signal = new Signal(
//                        Guid.NewGuid().ToString(),
//                        data.Symbol,
//                        SignalType.Buy,
//                        Math.Min(deviation, 1m),
//                        DateTime.UtcNow,
//                        StrategyId,
//                        $"Price {currentPrice:C} below lower band {lower:C}"
//                    );
//                    OnSignalGenerated(signal);
//                }
//            }
//            catch (Exception ex)
//            {
//                OnError(ex);
//            }
//        }
//    }

//    public class ExchangeSimulator : IExchange
//    {
//        public string ExchangeId { get; } = "SIM";
//        public MarketState CurrentState { get; private set; } = MarketState.Closed;

//        public event MarketStateChangedEventHandler MarketStateChanged;
//        public event QuoteEventHandler QuoteUpdated;
//        public event TradeEventHandler TradeExecuted;

//        private readonly Random _random = new();
//        private readonly Dictionary<string, decimal> _prices = new();
//        private readonly ConcurrentDictionary<string, Order> _orders = new();

//        public ExchangeSimulator()
//        {
//            // Инициализация цен для нескольких символов
//            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };
//            foreach (var symbol in symbols)
//            {
//                _prices[symbol] = (decimal)(100 + _random.NextDouble() * 900);
//            }

//            // Запуск симуляции
//            Task.Run(SimulateMarket);
//        }

//        public async Task<Order> SubmitOrderAsync(Order order)
//        {
//            await Task.Delay(50); // Имитация задержки сети

//            // Симуляция исполнения ордера
//            var fillPrice = order.Type == OrderType.Market
//                ? order.Side == OrderSide.Buy
//                    ? _prices[order.Symbol] * 1.001m  // Проскальзывание при покупке
//                    : _prices[order.Symbol] * 0.999m  // Проскальзывание при продаже
//                : order.Price.Value;

//            var filledOrder = order with
//            {
//                Status = OrderStatus.Filled,
//                FilledQuantity = order.Quantity,
//                AveragePrice = fillPrice
//            };

//            _orders[order.OrderId] = filledOrder;

//            // Генерация сделки
//            var trade = new Trade(
//                Guid.NewGuid().ToString(),
//                order.Symbol,
//                fillPrice,
//                order.Quantity,
//                order.Side,
//                DateTime.UtcNow,
//                "SIM_BUYER",
//                "SIM_SELLER"
//            );

//            TradeExecuted?.Invoke(trade);

//            return filledOrder;
//        }

//        public Task<bool> CancelOrderAsync(string orderId)
//        {
//            if (_orders.TryGetValue(orderId, out var order) && order.IsActive)
//            {
//                _orders[orderId] = order with { Status = OrderStatus.Cancelled };
//                return Task.FromResult(true);
//            }

//            return Task.FromResult(false);
//        }

//        public Task<MarketData> GetMarketDataAsync(string symbol)
//        {
//            if (!_prices.ContainsKey(symbol))
//                throw new ArgumentException($"Symbol {symbol} not found");

//            var price = _prices[symbol];
//            var quote = new Quote(
//                symbol,
//                price * 0.9995m,  // Bid
//                price * 1.0005m,  // Ask
//                price,            // Last
//                _random.Next(1000, 10000),  // Volume
//                DateTime.UtcNow
//            );

//            // Генерация бара
//            var bar = new Bar(
//                symbol,
//                DateTime.UtcNow.AddMinutes(-1),
//                DateTime.UtcNow,
//                price * 0.995m,
//                price * 1.005m,
//                price * 0.99m,
//                price,
//                _random.Next(5000, 50000)
//            );

//            var bars = Enumerable.Range(0, 100)
//                .Select(i => new Bar(
//                    symbol,
//                    DateTime.UtcNow.AddMinutes(-i - 1),
//                    DateTime.UtcNow.AddMinutes(-i),
//                    price * (0.95m + (decimal)_random.NextDouble() * 0.1m),
//                    price * (0.96m + (decimal)_random.NextDouble() * 0.1m),
//                    price * (0.94m + (decimal)_random.NextDouble() * 0.1m),
//                    price * (0.95m + (decimal)_random.NextDouble() * 0.1m),
//                    _random.Next(1000, 10000)
//                ));

//            var marketData = new MarketData(
//                symbol,
//                quote,
//                bar,
//                bars,
//                (decimal)_random.NextDouble() * 0.5m,  // Volatility
//                1.0m + (decimal)_random.NextDouble() * 0.5m  // Volume ratio
//            );

//            return Task.FromResult(marketData);
//        }

//        public Task<IEnumerable<Bar>> GetHistoricalDataAsync(string symbol, BarTimeFrame timeframe, int periods)
//        {
//            var bars = Enumerable.Range(0, periods)
//                .Select(i =>
//                {
//                    var basePrice = 100 + _random.NextDouble() * 900;
//                    return new Bar(
//                        symbol,
//                        DateTime.UtcNow.AddMinutes(-i * GetMinutes(timeframe)),
//                        DateTime.UtcNow.AddMinutes(-(i - 1) * GetMinutes(timeframe)),
//                        (decimal)(basePrice * (0.95 + _random.NextDouble() * 0.1)),
//                        (decimal)(basePrice * (0.96 + _random.NextDouble() * 0.1)),
//                        (decimal)(basePrice * (0.94 + _random.NextDouble() * 0.1)),
//                        (decimal)(basePrice * (0.95 + _random.NextDouble() * 0.1)),
//                        _random.Next(1000, 10000),
//                        timeframe
//                    );
//                });

//            return Task.FromResult(bars);
//        }

//        private int GetMinutes(BarTimeFrame timeframe) => timeframe switch
//        {
//            BarTimeFrame.OneMinute => 1,
//            BarTimeFrame.FiveMinutes => 5,
//            BarTimeFrame.FifteenMinutes => 15,
//            BarTimeFrame.ThirtyMinutes => 30,
//            BarTimeFrame.OneHour => 60,
//            BarTimeFrame.FourHours => 240,
//            BarTimeFrame.OneDay => 1440,
//            _ => 1
//        };

//        private async Task SimulateMarket()
//        {
//            while (true)
//            {
//                // Изменение состояния рынка
//                if (DateTime.UtcNow.Hour == 9 && CurrentState != MarketState.Open)
//                {
//                    var oldState = CurrentState;
//                    CurrentState = MarketState.Open;
//                    MarketStateChanged?.Invoke(oldState, CurrentState);
//                }
//                else if (DateTime.UtcNow.Hour == 16 && CurrentState != MarketState.Closed)
//                {
//                    var oldState = CurrentState;
//                    CurrentState = MarketState.Closed;
//                    MarketStateChanged?.Invoke(oldState, CurrentState);
//                }

//                // Обновление цен
//                if (CurrentState == MarketState.Open)
//                {
//                    foreach (var symbol in _prices.Keys.ToList())
//                    {
//                        var change = (_random.NextDouble() - 0.5) * 0.02; // ±2%
//                        _prices[symbol] *= (decimal)(1 + change);

//                        var quote = new Quote(
//                            symbol,
//                            _prices[symbol] * 0.9995m,
//                            _prices[symbol] * 1.0005m,
//                            _prices[symbol],
//                            _random.Next(1000, 10000),
//                            DateTime.UtcNow
//                        );

//                        QuoteUpdated?.Invoke(quote);
//                    }
//                }

//                await Task.Delay(1000); // Обновление каждую секунду
//            }
//        }
//    }

//    public class TradingEngine
//    {
//        private readonly IExchange _exchange;
//        private readonly TradingContext _context;
//        private readonly List<TradingStrategy> _strategies = new();
//        private readonly List<Order> _orderHistory = new();

//        public event OrderEventHandler OrderSubmitted;
//        public event OrderEventHandler OrderFilled;
//        public event PositionEventHandler PositionOpened;
//        public event PositionEventHandler PositionUpdated;
//        public event EventHandler<string> TradingError;

//        public TradingEngine(IExchange exchange, TradingContext context)
//        {
//            _exchange = exchange;
//            _context = context;

//            // Подписка на события биржи
//            _exchange.QuoteUpdated += OnQuoteUpdated;
//            _exchange.TradeExecuted += OnTradeExecuted;
//            _exchange.MarketStateChanged += OnMarketStateChanged;
//        }

//        public void AddStrategy(TradingStrategy strategy)
//        {
//            strategy.SignalGenerated += OnSignalGenerated;
//            strategy.StrategyError += OnStrategyError;
//            _strategies.Add(strategy);
//        }

//        public async Task RunAsync()
//        {
//            while (true)
//            {
//                if (_exchange.CurrentState == MarketState.Open)
//                {
//                    await ExecuteTradingCycle();
//                }

//                await Task.Delay(5000); // Цикл каждые 5 секунд
//            }
//        }

//        private async Task ExecuteTradingCycle()
//        {
//            foreach (var symbol in new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" })
//            {
//                var marketData = await _exchange.GetMarketDataAsync(symbol);

//                foreach (var strategy in _strategies.Where(s => s.IsActive))
//                {
//                    await strategy.AnalyzeAsync(marketData, _context);
//                }

//                // Обновление позиций
//                if (_context.Positions.Values.Any(p => p.Symbol == symbol))
//                {
//                    _context.UpdatePosition(symbol, marketData.LatestQuote.Last);
//                }
//            }
//        }

//        public async Task<Order> SubmitOrderAsync(Order order)
//        {
//            try
//            {
//                OrderSubmitted?.Invoke(order);

//                // Проверки риска
//                var riskChecks = new RiskCheckDelegate[]
//                {
//                    CheckMarginRequirements,
//                    CheckPositionLimits,
//                    CheckDailyLossLimit
//                };

//                if (!order.PassesRiskChecks(_context, riskChecks))
//                {
//                    throw new InvalidOperationException("Order failed risk checks");
//                }

//                // Расчет проскальзывания
//                var marketData = await _exchange.GetMarketDataAsync(order.Symbol);
//                var orderWithSlippage = order.WithSlippage(CalculateDynamicSlippage, marketData);

//                // Отправка ордера
//                var filledOrder = await _exchange.SubmitOrderAsync(orderWithSlippage);
//                _orderHistory.Add(filledOrder);
//                _context.AddOrder(filledOrder);

//                // Обновление позиции
//                UpdatePositionFromOrder(filledOrder);

//                OrderFilled?.Invoke(filledOrder);
//                return filledOrder;
//            }
//            catch (Exception ex)
//            {
//                TradingError?.Invoke(this, ex.Message);
//                throw;
//            }
//        }

//        private decimal CalculateDynamicSlippage(Order order, MarketData marketData)
//        {
//            var baseSlippage = 0.001m; // 0.1%
//            var volumeImpact = order.Quantity / marketData.LatestQuote.Volume * 10;
//            var volatilityImpact = marketData.Volatility * 0.5m;

//            return baseSlippage + volumeImpact + volatilityImpact;
//        }

//        private async Task<bool> CheckMarginRequirements(Order order, TradingContext context)
//        {
//            var requiredMargin = order.EstimatedValue * 0.1m; // 10% маржа
//            return context.AvailableCapital >= requiredMargin;
//        }

//        private async Task<bool> CheckPositionLimits(Order order, TradingContext context)
//        {
//            var symbolPositions = context.Positions.Values
//                .Where(p => p.Symbol == order.Symbol)
//                .Sum(p => p.Quantity);

//            var newPosition = symbolPositions + (order.Side == OrderSide.Buy ? order.Quantity : -order.Quantity);
//            return Math.Abs(newPosition) <= 1000; // Максимум 1000 акций на символ
//        }

//        private async Task<bool> CheckDailyLossLimit(Order order, TradingContext context)
//        {
//            return context.DailyPnL >= -context.TotalCapital * 0.02m; // Максимум 2% потерь в день
//        }

//        private void UpdatePositionFromOrder(Order order)
//        {
//            var existingPosition = _context.Positions.Values
//                .FirstOrDefault(p => p.Symbol == order.Symbol);

//            if (existingPosition == null)
//            {
//                // Новая позиция
//                var position = new Position(
//                    Guid.NewGuid().ToString(),
//                    order.Symbol,
//                    order.Side == OrderSide.Buy ? order.Quantity : -order.Quantity,
//                    order.AveragePrice,
//                    order.AveragePrice,
//                    DateTime.UtcNow
//                );

//                _context.AddPosition(position);
//                PositionOpened?.Invoke(position);
//            }
//            else
//            {
//                // Обновление существующей позиции
//                var newQuantity = existingPosition.Quantity +
//                    (order.Side == OrderSide.Buy ? order.Quantity : -order.Quantity);

//                var newAveragePrice = (existingPosition.CostBasis + order.EstimatedValue) /
//                    (Math.Abs(existingPosition.Quantity) + order.Quantity);

//                var updatedPosition = existingPosition with
//                {
//                    Quantity = newQuantity,
//                    AverageEntryPrice = Math.Abs(newAveragePrice)
//                };

//                _context.AddPosition(updatedPosition);
//                PositionUpdated?.Invoke(updatedPosition);
//            }
//        }

//        private void OnSignalGenerated(Signal signal)
//        {
//            Console.WriteLine($"Signal: {signal.Description}");

//            if (signal.IsActionable && signal.IsStrongSignal)
//            {
//                var order = CreateOrderFromSignal(signal);
//                _ = SubmitOrderAsync(order);
//            }
//        }

//        private Order CreateOrderFromSignal(Signal signal)
//        {
//            var side = signal.Type switch
//            {
//                SignalType.Buy or SignalType.StrongBuy => OrderSide.Buy,
//                SignalType.Sell or SignalType.StrongSell => OrderSide.Sell,
//                _ => throw new InvalidOperationException("Invalid signal type for order")
//            };

//            return new Order(
//                Guid.NewGuid().ToString(),
//                signal.Symbol,
//                OrderType.Market,
//                side,
//                CalculateOrderQuantity(signal),
//                null,
//                null,
//                DateTime.UtcNow,
//                _context.AccountId,
//                signal.StrategyId
//            );
//        }

//        private decimal CalculateOrderQuantity(Signal signal)
//        {
//            var baseQuantity = _context.TotalCapital * signal.Strength * 0.01m; // 1% капитала на сигнал
//            var price = _exchange.GetMarketDataAsync(signal.Symbol).Result.LatestQuote.Last;

//            return Math.Floor(baseQuantity / price);
//        }

//        private void OnQuoteUpdated(Quote quote)
//        {
//            // Обновление цен в позициях
//            _context.UpdatePosition(quote.Symbol, quote.Last);
//        }

//        private void OnTradeExecuted(Trade trade)
//        {
//            _context.AddTrade(trade);
//        }

//        private void OnMarketStateChanged(MarketState oldState, MarketState newState)
//        {
//            Console.WriteLine($"Market state changed: {oldState} -> {newState}");
//        }

//        private void OnStrategyError(object sender, Exception ex)
//        {
//            TradingError?.Invoke(this, $"Strategy error: {ex.Message}");
//        }
//    }

//    // ========== ДЕМОНСТРАЦИОННАЯ ПРОГРАММА ==========

//    class Program
//    {
//        static async Task Main(string[] args)
//        {
//            Console.WriteLine("=== ADVANCED TRADING SYSTEM ===");
//            Console.WriteLine("Нажмите Enter для запуска симуляции...");
//            Console.ReadLine();

//            // Создание компонентов
//            var exchange = new ExchangeSimulator();
//            var context = new TradingContext("ACC001", 100000m); // 100,000 начального капитала
//            var engine = new TradingEngine(exchange, context);

//            // Настройка стратегий
//            var momentumStrategy = new MomentumStrategy("MOM_01");
//            var meanReversionStrategy = new MeanReversionStrategy("MR_01");

//            engine.AddStrategy(momentumStrategy);
//            engine.AddStrategy(meanReversionStrategy);

//            // Подписка на события
//            engine.OrderSubmitted += order =>
//                Console.WriteLine($"Order submitted: {order.Symbol} {order.Side} {order.Quantity} @ {order.Price:C}");

//            engine.OrderFilled += order =>
//                Console.WriteLine($"Order filled: {order.Symbol} {order.Side} {order.FilledQuantity} @ {order.AveragePrice:C}");

//            engine.PositionOpened += position =>
//                Console.WriteLine($"Position opened: {position.Symbol} {position.Quantity} @ {position.AverageEntryPrice:C}");

//            engine.PositionUpdated += position =>
//                Console.WriteLine($"Position updated: {position.Symbol} PnL: {position.UnrealizedPnL:C} ({position.UnrealizedPnLPercentage:P2})");

//            engine.TradingError += (sender, error) =>
//                Console.WriteLine($"Trading error: {error}");

//            // Запуск двигателя
//            var engineTask = engine.RunAsync();

//            // Демонстрация отправки ордера
//            await Task.Delay(5000); // Ждем открытия рынка

//            var sampleOrder = new Order(
//                "TEST_001",
//                "AAPL",
//                OrderType.Market,
//                OrderSide.Buy,
//                10,
//                null,
//                null,
//                DateTime.UtcNow,
//                context.AccountId
//            );

//            try
//            {
//                await engine.SubmitOrderAsync(sampleOrder);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Order failed: {ex.Message}");
//            }

//            // Мониторинг в реальном времени
//            var monitorTask = Task.Run(async () =>
//            {
//                while (true)
//                {
//                    Console.WriteLine("\n=== ТОРГОВЫЙ МОНИТОР ===");
//                    Console.WriteLine($"Капитал: {context.TotalCapital:C}");
//                    Console.WriteLine($"Доступно: {context.AvailableCapital:C}");
//                    Console.WriteLine($"Маржа: {context.UsedMargin:C}");
//                    Console.WriteLine($"Общий PnL: {context.TotalPnL:C}");
//                    Console.WriteLine($"Дневной PnL: {context.DailyPnL:C}");

//                    Console.WriteLine("\nПозиции:");
//                    foreach (var position in context.Positions.Values)
//                    {
//                        Console.WriteLine($"  {position.Symbol}: {position.Quantity} @ {position.AverageEntryPrice:C} " +
//                                         $"(Текущая: {position.CurrentPrice:C}, PnL: {position.UnrealizedPnL:C})");
//                    }

//                    Console.WriteLine("\nАктивные ордера:");
//                    foreach (var order in context.ActiveOrders.Values)
//                    {
//                        Console.WriteLine($"  {order.Symbol} {order.Side} {order.RemainingQuantity}/{order.Quantity} " +
//                                         $"@{order.Price:C} ({order.Status})");
//                    }

//                    await Task.Delay(10000); // Обновление каждые 10 секунд
//                }
//            });

//            // Демонстрация функций расширения
//            await Task.Delay(10000);
//            Console.WriteLine("\n=== ДЕМОНСТРАЦИЯ ФУНКЦИЙ РАСШИРЕНИЯ ===");

//            var marketData = await exchange.GetMarketDataAsync("AAPL");
//            var bars = marketData.RecentBars.ToList();

//            Console.WriteLine($"ATR (14 периодов): {bars.CalculateATR(14):C}");
//            var (upper, middle, lower) = bars.CalculateBollingerBands(20, 2);
//            Console.WriteLine($"Bollinger Bands: Upper={upper:C}, Middle={middle:C}, Lower={lower:C}");

//            var smoothedBars = bars.Smoothed(5).Take(3);
//            foreach (var bar in smoothedBars)
//            {
//                Console.WriteLine($"Smoothed Bar: O={bar.Open:C}, H={bar.High:C}, L={bar.Low:C}, C={bar.Close:C}");
//            }

//            // Комбинирование стратегий через делегаты
//            Console.WriteLine("\n=== КОМБИНИРОВАНИЕ СТРАТЕГИЙ ===");

//            StrategyDelegate momentumLogic = (bars, ctx) =>
//            {
//                var signals = new List<Signal>();
//                // Логика импульса
//                return signals;
//            };

//            StrategyDelegate meanReversionLogic = (bars, ctx) =>
//            {
//                var signals = new List<Signal>();
//                // Логика возврата к среднему
//                return signals;
//            };

//            var combinedStrategy = momentumLogic.Compose(meanReversionLogic);
//            var combinedSignals = combinedStrategy(bars, context);

//            Console.WriteLine($"Сгенерировано сигналов: {combinedSignals.Count()}");

//            Console.WriteLine("\nСимуляция запущена. Нажмите Ctrl+C для остановки...");

//            await Task.WhenAny(engineTask, monitorTask);
//        }
//    }
//}

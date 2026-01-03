// TradingEntity.cs

// TradingEntity.cs
using System;

namespace ProtoTypeApp;

public abstract class TradingEntity : ICloneable
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }

    protected TradingEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    // Копирующий конструктор
    protected TradingEntity(TradingEntity other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        Id = other.Id;
        CreatedAt = other.CreatedAt;
    }

    // Базовый метод для клонирования
    public virtual TradingEntity MyClone()
    {
        throw new NotImplementedException(
            "MyClone should be implemented in derived types.");
    }

    // Реализация ICloneable
    public object Clone() => MyClone();
}
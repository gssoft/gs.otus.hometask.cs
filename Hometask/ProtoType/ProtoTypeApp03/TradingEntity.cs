// TradingEntity.cs

// TradingEntity.cs
using System;

namespace ProtoTypeApp
{
    public abstract class TradingEntity : IMyCloneable<TradingEntity>, ICloneable
    {
        public Guid Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }

        protected TradingEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        // Копирующий конструктор - сгенерирует новый ID и время создания
        protected TradingEntity(TradingEntity other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            Id = Guid.NewGuid();          // При клонировании создаем новый ID
            CreatedAt = DateTime.UtcNow;  // И новое время создания
        }

        // Базовый абстрактный метод для клонирования (тип TradingEntity)
        public abstract TradingEntity MyClone();

        // Реализация ICloneable — возвращает объект как object
        public object Clone() => MyClone();
    }
}
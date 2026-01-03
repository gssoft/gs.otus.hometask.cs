// Core/Interfaces/IGameSettings.cs - только настройки

namespace GuessTheNumber.Core.Interfaces;

public interface IGameSettings
{
    int MinNumber { get; }
    int MaxNumber { get; }
    int MaxAttempts { get; }
}



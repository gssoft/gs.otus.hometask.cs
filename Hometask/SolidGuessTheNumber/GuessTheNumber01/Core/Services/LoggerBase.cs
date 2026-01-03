// Core/Services/LoggerBase.cs

using GuessTheNumber.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Services;

public abstract class LoggerBase : IGameLogger
{
    public abstract void Log(string message);

    public virtual void LogGameStart(int targetNumber)
    {
        //  Log($"Игра началась. Загадано число: {targetNumber}");
        Log($"Игра началась...");
    }

    public virtual void LogAttempt(int attempt, int guess, string hint)
    {
        Log($"Попытка {attempt}: число {guess} - {hint}");
    }

    public virtual void LogGameEnd(bool isWin, int attemptsUsed)
    {
        var result = isWin ? "победа" : "проигрыш";
        Log($"Игра окончена: {result}, использовано попыток: {attemptsUsed}");
    }
}

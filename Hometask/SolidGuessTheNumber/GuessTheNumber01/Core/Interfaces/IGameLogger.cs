// Core/Interfaces/IGameLogger.cs

using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Interfaces;

// IGameLogger.cs - только логирование
public interface IGameLogger
{
    void Log(string message);
    void LogGameStart(int targetNumber);
    void LogAttempt(int attempt, int guess, string hint);
    void LogGameEnd(bool isWin, int attemptsUsed);
}

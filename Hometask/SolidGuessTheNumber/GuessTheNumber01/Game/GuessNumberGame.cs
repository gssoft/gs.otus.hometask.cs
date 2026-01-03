using GuessTheNumber.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using global::GuessTheNumber.Core.Models;

namespace GuessTheNumber.Game;

public class GuessNumberGame
{
    private readonly IGameSettings _settings;
    private readonly INumberGenerator _generator;
    private readonly IInputProvider _input;
    private readonly IOutputProvider _output;
    private readonly IGameLogger _logger;

    public GuessNumberGame(
        IGameSettings settings,
        INumberGenerator generator,
        IInputProvider input,
        IOutputProvider output,
        IGameLogger logger)
    {
        _settings = settings;
        _generator = generator;
        _input = input;
        _output = output;
        _logger = logger;
    }

    public GameResult Play()
    {
        var targetNumber = _generator.GenerateNumber(
            _settings.MinNumber,
            _settings.MaxNumber
        );

        _logger.LogGameStart(targetNumber);
        _output.DisplayMessage($"Угадайте число от {_settings.MinNumber} до {_settings.MaxNumber}");
        _output.DisplayMessage($"У вас {_settings.MaxAttempts} попыток");

        for (int attempt = 1; attempt <= _settings.MaxAttempts; attempt++)
        {
            _output.DisplayMessage($"Попытка {attempt}/{_settings.MaxAttempts}:");
            int guess = _input.GetNumberInput();

            if (guess == targetNumber)
            {
                _logger.LogAttempt(attempt, guess, "Угадали!");
                _output.DisplayMessage("Поздравляем! Вы угадали число!");
                _logger.LogGameEnd(true, attempt);
                return new GameResult
                {
                    IsWin = true,
                    AttemptsUsed = attempt,
                    TargetNumber = targetNumber
                };
            }

            string hint = guess < targetNumber ? "больше" : "меньше";
            _output.DisplayHint($"Загаданное число {hint}");
            _logger.LogAttempt(attempt, guess, hint);
        }

        _output.DisplayMessage($"Вы проиграли! Загаданное число было: {targetNumber}");
        _logger.LogGameEnd(false, _settings.MaxAttempts);
        return new GameResult
        {
            IsWin = false,
            AttemptsUsed = _settings.MaxAttempts,
            TargetNumber = targetNumber
        };
    }
}

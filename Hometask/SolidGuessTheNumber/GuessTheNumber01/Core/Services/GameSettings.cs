using GuessTheNumber.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

/*
Принцип OCP (Open-Closed Principle)
Классы открыты для расширения, но закрыты для модификации. 
*/

namespace GuessTheNumber.Core.Services
{
    public abstract class GameSettingsBase : IGameSettings
    {
        public abstract int MinNumber { get; }
        public abstract int MaxNumber { get; }
        public abstract int MaxAttempts { get; }
    }

    public class DefaultGameSettings : GameSettingsBase
    {
        public override int MinNumber => 1;
        public override int MaxNumber => 100;
        public override int MaxAttempts => 10;
    }

    public class HardGameSettings : GameSettingsBase
    {
        public override int MinNumber => 1;
        public override int MaxNumber => 1000;
        public override int MaxAttempts => 5;
    }

    public class CustomGameSettings : GameSettingsBase
    {
        private readonly int _minNumber;
        private readonly int _maxNumber;
        private readonly int _maxAttempts;

        public CustomGameSettings(int minNumber, int maxNumber, int maxAttempts)
        {
            _minNumber = minNumber;
            _maxNumber = maxNumber;
            _maxAttempts = maxAttempts;
        }

        public override int MinNumber => _minNumber;
        public override int MaxNumber => _maxNumber;
        public override int MaxAttempts => _maxAttempts;
    }
}
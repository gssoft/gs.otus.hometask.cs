// INumberGenerator.cs - только генерация чисел

using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Interfaces;

public interface INumberGenerator
{
    int GenerateNumber(int min, int max);
}
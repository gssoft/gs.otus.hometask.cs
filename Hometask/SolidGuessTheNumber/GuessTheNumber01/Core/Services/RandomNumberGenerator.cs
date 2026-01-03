using System;
using System.Collections.Generic;
using System.Text;

using GuessTheNumber.Core.Interfaces;

namespace GuessTheNumber.Core.Services;

public class RandomNumberGenerator : INumberGenerator
{
    private readonly Random _random = new Random();

    public int GenerateNumber(int min, int max)
    {
        return _random.Next(min, max + 1);
    }
}

// Core/Interfaces/IInputProvider.cs

using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Interfaces;

public interface IInputProvider
{
    int GetNumberInput();
    string GetStringInput();
}


// Core/Interfaces/IOutputProvider.cs

using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Interfaces;

public interface IOutputProvider
{
    void DisplayMessage(string message);
    void DisplayHint(string hint);
}


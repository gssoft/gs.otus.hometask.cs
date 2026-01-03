// IUserInput.cs - только ввод данных

using System;
using System.Collections.Generic;
using System.Text;

namespace GuessTheNumber.Core.Interfaces;

public interface IUserInput
{
    int GetNumberInput();
    string GetStringInput();
}

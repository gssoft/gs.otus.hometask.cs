
using GuessTheNumber.Core.Interfaces;
using System;
using System;
using System.Collections.Generic;
using System.Text;

namespace GuessNumberGame.Core.Services;

public class ConsoleInput : IInputProvider
{
    public int GetNumberInput()
    {
        while (true)
        {
            Console.Write("Введите число: ");
            if (int.TryParse(Console.ReadLine(), out int number))
                return number;
            Console.WriteLine("Ошибка ввода. Пожалуйста, введите целое число.");
        }
    }

    public string GetStringInput()
    {
        return Console.ReadLine();
    }
}

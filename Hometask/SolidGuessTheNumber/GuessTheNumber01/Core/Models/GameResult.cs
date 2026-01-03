using System;
using System.Collections.Generic;
using System.Text;

namespace GuessNumberGame.Core.Models;
public class GameResult
{
    public bool IsWin { get; set; }
    public int AttemptsUsed { get; set; }
    public int TargetNumber { get; set; }
}
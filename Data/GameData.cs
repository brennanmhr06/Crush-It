using System;
using System.Collections.Generic;

namespace CrushIt.Data
{
    public static class GameData
    {

        public static int TotalScore { get; private set; } = 0;


        private static readonly Dictionary<CandyType, int> CandyPointValues = new Dictionary<CandyType, int>
        {
            { CandyType.RedStrawberry, 50 },
            { CandyType.BlueGummy,      40 },
            { CandyType.GreenApple,     30 },
            { CandyType.YellowLemon,    20 },
            { CandyType.PurplePlum,     10 },
            { CandyType.RedStrawberryStriped, 100 },
            { CandyType.BlueGummyStriped, 80 },
            { CandyType.GreenAppleStriped, 60 },
            { CandyType.YellowLemonStriped, 40 },
            { CandyType.PurplePlumStriped, 20 },
            { CandyType.ColorBomb, 150 }
        };




        public static int GetCandyPoints(CandyType type)
        {
            if (CandyPointValues.TryGetValue(type, out int points))
            {
                return points;
            }
            return 10;
        }




        public static void AddPoints(CandyType type)
        {
            TotalScore += GetCandyPoints(type);
        }




        public static void ResetScore()
        {
            TotalScore = 0;
        }
    }
}


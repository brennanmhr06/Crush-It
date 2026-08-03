using System;
using System.Drawing;

namespace CrushIt.Data
{
    public enum CandyType
    {
        RedStrawberry,
        BlueGummy,
        GreenApple,
        YellowLemon,
        PurplePlum
    }

    public static class CandyGoldValues
    {
        public static int GetGoldValue(CandyType candy, int level)
        {

            int baseGold = candy switch
            {
                CandyType.RedStrawberry => 5,
                CandyType.BlueGummy => 10,
                CandyType.GreenApple => 7,
                CandyType.YellowLemon => 8,
                CandyType.PurplePlum => 6,
                _ => 5
            };



            int levelBonus = (int)(level * 0.5);


            levelBonus = Math.Min(levelBonus, 10);

            return baseGold + levelBonus;
        }


        public static int GetGoldValue(CandyType candy)
        {
            return GetGoldValue(candy, 1);
        }
    }

    public struct CandyParticle
    {
        public float X, Y;
        public float SpeedX, SpeedY;
        public float Size;
        public float Alpha;
        public Color ParticleColor;
    }
}


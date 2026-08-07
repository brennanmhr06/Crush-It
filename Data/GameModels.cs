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
        PurplePlum,
        RedStrawberryStriped,
        BlueGummyStriped,
        GreenAppleStriped,
        YellowLemonStriped,
        PurplePlumStriped,
        ColorBomb
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
                CandyType.RedStrawberryStriped => 10,
                CandyType.BlueGummyStriped => 20,
                CandyType.GreenAppleStriped => 14,
                CandyType.YellowLemonStriped => 16,
                CandyType.PurplePlumStriped => 12,
                CandyType.ColorBomb => 25,
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

    public static class CandyTypeExtensions
    {
        public static bool IsStriped(this CandyType type)
        {
            return type == CandyType.RedStrawberryStriped ||
                   type == CandyType.BlueGummyStriped ||
                   type == CandyType.GreenAppleStriped ||
                   type == CandyType.YellowLemonStriped ||
                   type == CandyType.PurplePlumStriped;
        }

        public static bool IsColorBomb(this CandyType type)
        {
            return type == CandyType.ColorBomb;
        }

        public static bool IsSpecial(this CandyType type)
        {
            return type.IsStriped() || type.IsColorBomb();
        }

        public static CandyType GetBaseType(this CandyType type)
        {
            return type switch
            {
                CandyType.RedStrawberryStriped => CandyType.RedStrawberry,
                CandyType.BlueGummyStriped => CandyType.BlueGummy,
                CandyType.GreenAppleStriped => CandyType.GreenApple,
                CandyType.YellowLemonStriped => CandyType.YellowLemon,
                CandyType.PurplePlumStriped => CandyType.PurplePlum,
                _ => type
            };
        }

        public static CandyType GetStripedVariant(this CandyType type)
        {
            return type switch
            {
                CandyType.RedStrawberry => CandyType.RedStrawberryStriped,
                CandyType.BlueGummy => CandyType.BlueGummyStriped,
                CandyType.GreenApple => CandyType.GreenAppleStriped,
                CandyType.YellowLemon => CandyType.YellowLemonStriped,
                CandyType.PurplePlum => CandyType.PurplePlumStriped,
                _ => type
            };
        }

        public static bool IsHorizontalStriped(this CandyType type)
        {
            // For simplicity, we alternate between horizontal and vertical based on candy type
            return type switch
            {
                CandyType.RedStrawberryStriped => true,
                CandyType.BlueGummyStriped => false,
                CandyType.GreenAppleStriped => true,
                CandyType.YellowLemonStriped => false,
                CandyType.PurplePlumStriped => true,
                _ => false
            };
        }
    }
}


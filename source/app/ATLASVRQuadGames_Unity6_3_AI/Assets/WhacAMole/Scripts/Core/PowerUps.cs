using UnityEngine;

namespace Whacamole.Core
{
    /// <summary>The acorn power-ups. Hit an acorn with a hammer to collect it; letting one sink back costs nothing.</summary>
    public enum PowerUpType
    {
        ExtraLife,  // +1 life (up to the max)
        BigHammer,  // both hammers grow for a while: bigger reach and a bigger head
        SlowTime    // nuts rise, hover and pop up more slowly for a while
    }

    /// <summary>Names, colors and messages for each power-up, shared by the nuts, effects and scoreboard.</summary>
    public static class PowerUpInfo
    {
        public static readonly PowerUpType[] All = { PowerUpType.ExtraLife, PowerUpType.BigHammer, PowerUpType.SlowTime };

        /// <summary>Acorn body color (and sparkle color) for a power-up.</summary>
        public static Color GetColor(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraLife: return new Color(0.25f, 0.9f, 0.35f);  // green
                case PowerUpType.BigHammer: return new Color(0.7f, 0.35f, 1f);     // purple
                case PowerUpType.SlowTime: return new Color(0.3f, 0.85f, 1f);      // icy blue
                default: return Color.white;
            }
        }

        /// <summary>Short name, e.g. for the active-timer line on the scoreboard.</summary>
        public static string GetName(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraLife: return "Extra life";
                case PowerUpType.BigHammer: return "Big hammer";
                case PowerUpType.SlowTime: return "Slow-mo";
                default: return type.ToString();
            }
        }

        /// <summary>What the scoreboard flashes when the acorn is collected.</summary>
        public static string GetCollectedMessage(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraLife: return "+1 LIFE!";
                case PowerUpType.BigHammer: return "BIG HAMMER!";
                case PowerUpType.SlowTime: return "SLOW-MO!";
                default: return type.ToString().ToUpper() + "!";
            }
        }

        /// <summary>One-line explanation shown when a power-up first appears at a level up.</summary>
        public static string GetIntro(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraLife: return "NEW: green acorn = +1 life";
                case PowerUpType.BigHammer: return "NEW: purple acorn = big hammer";
                case PowerUpType.SlowTime: return "NEW: blue acorn = slow-mo";
                default: return "NEW: " + GetName(type);
            }
        }
    }
}

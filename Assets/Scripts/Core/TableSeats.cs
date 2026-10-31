using System;

namespace CaSentLeSoufre.Core
{
    /// <summary>
    /// Bande de table réservée à un joueur, sur un écran de référence de 1920 x 1080. Les coordonnées partent
    /// du centre de l'écran (x vers la droite, y vers le haut), comme dans l'ancien projet.
    /// </summary>
    public readonly struct SeatArea
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        /// <summary>Colonne sur un côté de l'écran : ses cartes défilent verticalement, tournées d'un quart de tour.</summary>
        public bool IsVertical => Height > Width;

        public SeatArea(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Left => X - Width / 2;
        public float Right => X + Width / 2;
        public float Bottom => Y - Height / 2;
        public float Top => Y + Height / 2;
    }

    /// <summary>
    /// Disposition des joueurs autour de la table pour 3 à 10 joueurs : des bandes de 230 de large le long des
    /// bords de l'écran (en bas, à droite, en haut, à gauche), dans l'ordre de jeu. Le joueur humain, premier,
    /// est toujours en bas. Le centre de l'écran reste libre pour les piles et la main.
    /// </summary>
    public static class TableSeats
    {
        public const float ScreenWidth = 1920f;
        public const float ScreenHeight = 1080f;
        public const float Thickness = 230f;

        public static SeatArea[] For(int playerCount)
        {
            switch (playerCount)
            {
                case 4:
                    return new[]
                    {
                        new SeatArea(0, -430, 1430, 230), new SeatArea(830, 0, 230, 1080),
                        new SeatArea(0, 430, 1400, 230), new SeatArea(-830, 0, 230, 1080)
                    };
                case 5:
                    return new[]
                    {
                        new SeatArea(0, -430, 1400, 230), new SeatArea(830, 0, 230, 1080),
                        new SeatArea(357.5f, 430, 715, 230), new SeatArea(-357.5f, 430, 715, 230),
                        new SeatArea(-830, 0, 230, 1080)
                    };
                case 6:
                    return new[]
                    {
                        new SeatArea(357.5f, -430, 715, 230), new SeatArea(830, 0, 230, 1080),
                        new SeatArea(357.5f, 430, 715, 230), new SeatArea(-357.5f, 430, 715, 230),
                        new SeatArea(-830, 0, 230, 1080), new SeatArea(-357.5f, -430, 715, 230)
                    };
                case 7:
                    return new[]
                    {
                        new SeatArea(357.5f, -430, 715, 230), new SeatArea(830, -270, 230, 540),
                        new SeatArea(830, 270, 230, 540), new SeatArea(357.5f, 430, 715, 230),
                        new SeatArea(-357.5f, 430, 715, 230), new SeatArea(-830, 0, 230, 1080),
                        new SeatArea(-357.5f, -430, 715, 230)
                    };
                case 8:
                    return new[]
                    {
                        new SeatArea(357.5f, -430, 715, 230), new SeatArea(830, -270, 230, 540),
                        new SeatArea(830, 270, 230, 540), new SeatArea(357.5f, 430, 715, 230),
                        new SeatArea(-357.5f, 430, 715, 230), new SeatArea(-830, 270, 230, 540),
                        new SeatArea(-830, -270, 230, 540), new SeatArea(-357.5f, -430, 715, 230)
                    };
                case 9:
                    return new[]
                    {
                        new SeatArea(357.5f, -430, 715, 230), new SeatArea(830, -270, 230, 540),
                        new SeatArea(830, 270, 230, 540), new SeatArea(476.667f, 430, 476.667f, 230),
                        new SeatArea(0, 430, 476.667f, 230), new SeatArea(-476.667f, 430, 476.667f, 230),
                        new SeatArea(-830, 270, 230, 540), new SeatArea(-830, -270, 230, 540),
                        new SeatArea(-357.5f, -430, 715, 230)
                    };
                case 10:
                    return new[]
                    {
                        new SeatArea(0, -430, 476.667f, 230), new SeatArea(476.667f, -430, 476.667f, 230),
                        new SeatArea(830, -270, 230, 540), new SeatArea(830, 270, 230, 540),
                        new SeatArea(476.667f, 430, 476.667f, 230), new SeatArea(0, 430, 476.667f, 230),
                        new SeatArea(-476.667f, 430, 476.667f, 230), new SeatArea(-830, 270, 230, 540),
                        new SeatArea(-830, -270, 230, 540), new SeatArea(-476.667f, -430, 476.667f, 230)
                    };
                default: // 3 joueurs
                    return new[]
                    {
                        new SeatArea(0, -430, 1430, 230), new SeatArea(830, 0, 230, 1080),
                        new SeatArea(0, 430, 1430, 230)
                    };
            }
        }

        /// <summary>Zone centrale libre (piles, journal, main du joueur), entre les bandes de côté et du haut.</summary>
        public static SeatArea Center => new SeatArea(0, 0, 1430f - 2 * 15f, 630f - 2 * 10f);
    }
}

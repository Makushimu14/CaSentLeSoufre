namespace CaSentLeSoufre.Core
{
    /// <summary>Tous les pouvoirs de cartes connus. Le joueur en choisit jusqu'à 4 et leur associe une valeur.</summary>
    public enum PowerType
    {
        /// <summary>Remise à zéro : jouable sur tout, et on peut jouer n'importe quoi dessus (le 2).</summary>
        Reset,

        /// <summary>Brûle la défausse ; le joueur rejoue. Jouable sur tout (le 10).</summary>
        Burn,

        /// <summary>Transparent : jouable sur tout, le suivant doit battre la carte située dessous (le 3).</summary>
        Transparent,

        /// <summary>Le joueur suivant doit jouer une valeur inférieure ou égale (le 7).</summary>
        LowerOrEqual,

        /// <summary>Le joueur suivant passe son tour, un joueur par carte jouée (le 8).</summary>
        Skip,

        /// <summary>Le sens du jeu s'inverse (le 9).</summary>
        Reverse,

        /// <summary>Le joueur suivant ramasse la défausse et passe son tour (le Valet).</summary>
        ForcePickUp,

        /// <summary>Le joueur rejoue immédiatement (le 5).</summary>
        PlayAgain
    }

    public readonly struct PowerSlot
    {
        public PowerType Power { get; }

        /// <summary>Valeur de carte (2 à 14) qui porte ce pouvoir.</summary>
        public int Rank { get; }

        public PowerSlot(PowerType power, int rank)
        {
            Power = power;
            Rank = rank;
        }

        public PowerSlot(PowerType power) : this(power, PowerCatalog.DefaultRank(power)) { }
    }

    public static class PowerCatalog
    {
        public static readonly PowerType[] All =
        {
            PowerType.Reset, PowerType.Burn, PowerType.Transparent, PowerType.LowerOrEqual,
            PowerType.Skip, PowerType.Reverse, PowerType.ForcePickUp, PowerType.PlayAgain
        };

        /// <summary>Valeur traditionnellement associée au pouvoir.</summary>
        public static int DefaultRank(PowerType power)
        {
            switch (power)
            {
                case PowerType.Reset: return 2;
                case PowerType.Transparent: return 3;
                case PowerType.PlayAgain: return 5;
                case PowerType.LowerOrEqual: return 7;
                case PowerType.Skip: return 8;
                case PowerType.Reverse: return 9;
                case PowerType.Burn: return 10;
                default: return Card.Jack; // ForcePickUp
            }
        }

        /// <summary>Jouable sur n'importe quelle défausse.</summary>
        public static bool IsAlwaysPlayable(PowerType power) =>
            power == PowerType.Reset || power == PowerType.Burn || power == PowerType.Transparent;

        public static string Label(PowerType power)
        {
            switch (power)
            {
                case PowerType.Reset: return "Remise à zéro";
                case PowerType.Burn: return "Brûlure";
                case PowerType.Transparent: return "Transparent";
                case PowerType.LowerOrEqual: return "Plus bas";
                case PowerType.Skip: return "Saute un tour";
                case PowerType.Reverse: return "Inverse le sens";
                case PowerType.ForcePickUp: return "Ramassage forcé";
                default: return "Rejouer";
            }
        }

        public static string Description(PowerType power)
        {
            switch (power)
            {
                case PowerType.Reset: return "Jouable sur tout. Le suivant peut jouer n'importe quelle carte.";
                case PowerType.Burn: return "Jouable sur tout. Brûle la défausse et vous rejouez.";
                case PowerType.Transparent: return "Jouable sur tout. Le suivant doit battre la carte située dessous.";
                case PowerType.LowerOrEqual: return "Le suivant doit jouer une carte de valeur inférieure ou égale.";
                case PowerType.Skip: return "Le suivant passe son tour (un joueur de plus par carte jouée).";
                case PowerType.Reverse: return "Le sens du jeu s'inverse.";
                case PowerType.ForcePickUp: return "Le suivant ramasse la défausse et passe son tour.";
                default: return "Vous rejouez immédiatement.";
            }
        }
    }
}

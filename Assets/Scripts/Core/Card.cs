using System;

namespace CaSentLeSoufre.Core
{
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    /// <summary>Carte standard. Rang de 2 à 14 (11 = Valet, 12 = Dame, 13 = Roi, 14 = As).</summary>
    public readonly struct Card : IEquatable<Card>
    {
        public const int Jack = 11;
        public const int Queen = 12;
        public const int King = 13;
        public const int Ace = 14;

        public int Rank { get; }
        public Suit Suit { get; }

        public Card(int rank, Suit suit)
        {
            if (rank < 2 || rank > Ace)
                throw new ArgumentOutOfRangeException(nameof(rank), "Le rang doit être compris entre 2 et 14.");
            Rank = rank;
            Suit = suit;
        }

        /// <summary>
        /// Force d'une valeur pour comparer deux cartes : le 3 est la plus faible, puis le 4 ... le roi, l'as,
        /// et le 2 est la plus forte de toutes (sans pouvoir, il bat tout).
        /// </summary>
        public static int Strength(int rank) => rank == 2 ? Ace + 1 : rank;

        /// <summary>Les « têtes » : valet, dame et roi (l'as n'en fait pas partie).</summary>
        public static bool IsFace(int rank) => rank >= Jack && rank <= King;

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Rank * 4 + (int)Suit;
        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);

        /// <summary>Libellé technique invariant (J, Q, K, A). Pour l'affichage, utiliser <see cref="RankKey"/>.</summary>
        public string RankLabel
        {
            get
            {
                switch (Rank)
                {
                    case Jack: return "J";
                    case Queen: return "Q";
                    case King: return "K";
                    case Ace: return "A";
                    default: return Rank.ToString();
                }
            }
        }

        /// <summary>
        /// Clé de traduction de la valeur pour les figures et l'As (« card.rank.11 » à « card.rank.14 »),
        /// null pour les valeurs numériques 2 à 10 qui s'affichent telles quelles.
        /// </summary>
        public string RankKey => Rank >= Jack ? "card.rank." + Rank : null;

        public string SuitSymbol
        {
            get
            {
                switch (Suit)
                {
                    case Suit.Clubs: return "♣";
                    case Suit.Diamonds: return "♦";
                    case Suit.Hearts: return "♥";
                    default: return "♠";
                }
            }
        }

        public override string ToString() => RankLabel + SuitSymbol;
    }
}

using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    public sealed class Player
    {
        public string Name { get; }
        public bool IsHuman { get; }

        /// <summary>Niveau de l'IA ; sans effet pour un joueur humain.</summary>
        public AiDifficulty Difficulty { get; }

        /// <summary>Cartes cachées distribuées à chaque manche (varie en mode démon, minimum 1).</summary>
        public int FaceDownSlots { get; private set; } = RulesConfig.HandSize;

        /// <summary>Cartes visibles distribuées à chaque manche (varie en mode démon, minimum 1).</summary>
        public int FaceUpSlots { get; private set; } = RulesConfig.HandSize;

        public void SetSlots(int faceDown, int faceUp)
        {
            if (faceDown < 1 || faceUp < 1)
                throw new System.ArgumentOutOfRangeException(nameof(faceDown), "Au moins 1 carte cachée et 1 visible.");
            FaceDownSlots = faceDown;
            FaceUpSlots = faceUp;
        }

        public List<Card> Hand { get; } = new List<Card>();
        public List<Card> FaceUp { get; } = new List<Card>();
        public List<Card> FaceDown { get; } = new List<Card>();

        public Player(string name, bool isHuman = false, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            Name = name;
            IsHuman = isHuman;
            Difficulty = difficulty;
        }

        /// <summary>
        /// Cartes de la main que tous les joueurs connaissent : elles étaient visibles (cartes visibles échangées
        /// en début de manche), ou elles ont été ramassées sur la défausse que tout le monde a vue. Les cartes
        /// piochées ne sont pas connues. Sous-ensemble de <see cref="Hand"/> ; les IA difficiles s'en servent
        /// pour contrer le joueur suivant sans tricher.
        /// </summary>
        public List<Card> KnownInHand { get; } = new List<Card>();

        /// <summary>Le joueur ne cache plus rien dans sa main : toutes ses cartes sont connues.</summary>
        public bool HandFullyKnown => KnownInHand.Count >= Hand.Count;

        public bool HasCards => Hand.Count > 0 || FaceUp.Count > 0 || FaceDown.Count > 0;
        public int TotalCards => Hand.Count + FaceUp.Count + FaceDown.Count;

        public void ClearCards()
        {
            KnownInHand.Clear();
            Hand.Clear();
            FaceUp.Clear();
            FaceDown.Clear();
        }
    }
}

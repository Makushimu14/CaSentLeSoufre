using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    public static class Deck
    {
        public const int CardsPerDeck = 52;

        public static List<Card> CreateStandard52() => Create(1);

        /// <summary>Plusieurs paquets de 52 cartes mélangés ensemble ; les doublons sont identiques entre eux.</summary>
        public static List<Card> Create(int deckCount)
        {
            var cards = new List<Card>(CardsPerDeck * deckCount);
            for (int d = 0; d < deckCount; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 2; r <= Card.Ace; r++)
                        cards.Add(new Card(r, (Suit)s));
            return cards;
        }

        /// <summary>Mélange de Fisher-Yates, en place.</summary>
        public static void Shuffle(IList<Card> cards, Random rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}

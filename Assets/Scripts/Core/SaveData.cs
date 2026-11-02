using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    // Formats d'enregistrement d'une partie en cours. Uniquement des champs publics, des nombres, des booléens,
    // du texte et des listes : sérialisable tel quel en JSON par la couche Unity. Les énumérations sont
    // enregistrées en nombres ; ne jamais réordonner leurs valeurs existantes.

    [Serializable]
    public sealed class CardData
    {
        public int Rank;
        public int Suit;

        public static CardData From(Card card) => new CardData { Rank = card.Rank, Suit = (int)card.Suit };

        /// <summary>Lève une exception si la carte enregistrée n'a pas de sens (fichier modifié ou abîmé).</summary>
        public Card ToCard()
        {
            if (Suit < 0 || Suit > 3) throw new InvalidOperationException("Couleur de carte invalide.");
            return new Card(Rank, (Suit)Suit);
        }

        public static List<CardData> FromList(IEnumerable<Card> cards)
        {
            var list = new List<CardData>();
            foreach (var card in cards) list.Add(From(card));
            return list;
        }

        public static List<Card> ToCards(IEnumerable<CardData> data)
        {
            var list = new List<Card>();
            if (data == null) return list;
            foreach (var item in data)
            {
                if (item == null) throw new InvalidOperationException("Carte manquante.");
                list.Add(item.ToCard());
            }
            return list;
        }
    }

    [Serializable]
    public sealed class PlayerCardsData
    {
        public List<CardData> Hand = new List<CardData>();
        public List<CardData> FaceUp = new List<CardData>();
        public List<CardData> FaceDown = new List<CardData>();

        /// <summary>Cartes de la main connues de tous (voir <see cref="Player.KnownInHand"/>).</summary>
        public List<CardData> KnownInHand = new List<CardData>();
    }

    /// <summary>État complet d'une manche : pioche, défausse, cartes de chacun, tour, sens, ordre de sortie.</summary>
    [Serializable]
    public sealed class GameData
    {
        public int Phase;
        public int CurrentPlayerIndex;
        public int BurnedCount;
        public int Direction = 1;
        public int DeckCount = 1;
        public int LoserIndex = -1;
        public bool IsStalemate;
        public List<int> FinishOrder = new List<int>();
        public List<bool> Ready = new List<bool>();
        public List<CardData> DrawPile = new List<CardData>();
        public List<CardData> DiscardPile = new List<CardData>();
        public List<PlayerCardsData> Players = new List<PlayerCardsData>();
    }

    /// <summary>Réglages de la partie (copie figée : indépendante des préférences actuelles du joueur).</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public int Mode;
        public bool FourOfAKindBurns = true;
        public bool JumpIn;
        public bool AllowInitialSwap = true;
        public List<PowerPreference> Powers = new List<PowerPreference>();
        public int RoundCount = 1;
        public string HumanName = string.Empty;
        public List<AiPreference> Opponents = new List<AiPreference>();
    }

    /// <summary>
    /// Partie complète à reprendre plus tard : réglages, scores, cartes à distribuer par joueur (mode démon)
    /// et manche en cours. Le tirage des manches suivantes se rejoue à partir de la graine.
    /// </summary>
    [Serializable]
    public sealed class MatchData
    {
        public const int CurrentVersion = 2; // 2 : places des adversaires tirées au sort

        public int Version = CurrentVersion;
        public SettingsData Settings = new SettingsData();

        public int Seed;

        /// <summary>Nombre de manches distribuées : sert à rejouer la suite de tirages à partir de la graine.</summary>
        public int RoundsStarted;

        public int RoundNumber;
        public int RoundsCompleted;
        public bool RoundInProgress;

        public List<int> Points = new List<int>();
        public List<int> ShitheadCounts = new List<int>();
        public List<int> FaceDownSlots = new List<int>();
        public List<int> FaceUpSlots = new List<int>();

        /// <summary>
        /// Une manche est enregistrée dans <see cref="Game"/>. Indicateur explicite : le format JSON d'Unity relit
        /// un objet absent comme un objet vide, jamais comme null.
        /// </summary>
        public bool HasGame;

        /// <summary>Manche en cours ou terminée la dernière (vide si <see cref="HasGame"/> est faux).</summary>
        public GameData Game = new GameData();
    }
}

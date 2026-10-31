using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    public enum GameMode
    {
        /// <summary>Règles officielles.</summary>
        Classic,

        /// <summary>
        /// Mode démon (jusqu'à 10 joueurs) : à chaque manche, le dernier reçoit une carte cachée et une visible
        /// de plus, le premier en perd une de chaque (minimum 1).
        /// </summary>
        Demon
    }

    /// <summary>Paramètres de règles d'une partie.</summary>
    public sealed class RulesConfig
    {
        public const int MinPlayers = 3;

        /// <summary>Taille de la main, et nombre initial de cartes visibles / cachées par joueur.</summary>
        public const int HandSize = 3;

        /// <summary>Nombre maximal de pouvoirs de cartes actifs en même temps.</summary>
        public const int MaxPowers = 4;

        /// <summary>Cartes minimales à garder en pioche après la distribution (détermine le nombre de paquets).</summary>
        public const int MinDrawPileSize = 7;

        /// <summary>
        /// Sécurité anti-blocage n°1 : si exactement la même situation (cartes de chacun, défausse, joueur
        /// courant, sens) se reproduit ce nombre de fois sans aucun progrès, la manche s'arrête.
        /// Un « progrès » : un joueur sort, ou atteint un nouveau minimum de cartes pour lui.
        /// </summary>
        public const int StalemateRepeatLimit = 6;

        /// <summary>
        /// Sécurité anti-blocage n°2 (filet de sécurité contre les boucles non exactes) : sans progrès pendant
        /// ce nombre de coups par joueur encore en jeu, la manche s'arrête.
        /// </summary>
        public const int StalemateMovesPerPlayer = 300;

        public GameMode Mode = GameMode.Classic;

        /// <summary>Quatre cartes de même rang consécutives sur la défausse la brûlent (le joueur rejoue).</summary>
        public bool FourOfAKindBurns = true;

        /// <summary>
        /// Pouvoirs actifs (au plus <see cref="MaxPowers"/>), chacun associé à une valeur de carte.
        /// Par défaut : 2 = remise à zéro, 10 = brûlure.
        /// </summary>
        public List<PowerSlot> Powers = DefaultPowers();

        /// <summary>
        /// Jeu à la volée : n'importe quel joueur peut, hors de son tour, jouer une carte de même valeur
        /// que celle du dessus de la défausse. Le tour reprend alors après lui.
        /// </summary>
        public bool JumpIn = false;

        /// <summary>Autorise d'échanger des cartes de la main avec les cartes visibles en début de manche.</summary>
        public bool AllowInitialSwap = true;

        public static RulesConfig Classic => new RulesConfig();

        public static List<PowerSlot> DefaultPowers() => new List<PowerSlot>
        {
            new PowerSlot(PowerType.Reset),
            new PowerSlot(PowerType.Burn)
        };

        /// <summary>Règles dont les seuls pouvoirs sont ceux fournis.</summary>
        public static RulesConfig WithPowers(params PowerSlot[] powers)
        {
            var rules = new RulesConfig();
            rules.Powers = new List<PowerSlot>(powers);
            return rules;
        }

        public static int MaxPlayersFor(GameMode mode) => mode == GameMode.Demon ? 10 : 5;

        /// <summary>Plus petit nombre de paquets de 52 cartes qui couvre la distribution et laisse une pioche minimale.</summary>
        public static int DeckCountFor(int cardsToDeal)
        {
            int needed = cardsToDeal + MinDrawPileSize;
            return (needed + Deck.CardsPerDeck - 1) / Deck.CardsPerDeck;
        }

        /// <summary>Pouvoir porté par cette valeur de carte, ou null.</summary>
        public PowerType? PowerOf(int rank)
        {
            foreach (var slot in Powers)
                if (slot.Rank == rank) return slot.Power;
            return null;
        }

        public bool HasPower(PowerType power)
        {
            foreach (var slot in Powers)
                if (slot.Power == power) return true;
            return false;
        }

        /// <summary>Problèmes détectés, sous forme de clés de traduction ; liste vide si les règles sont valides.</summary>
        public List<LocalizedMessage> Validate()
        {
            var errors = new List<LocalizedMessage>();
            if (Powers == null)
            {
                errors.Add(new LocalizedMessage("validation.powers_missing"));
                return errors;
            }

            if (Powers.Count > MaxPowers)
                errors.Add(new LocalizedMessage("validation.powers_too_many", MaxPowers, Powers.Count));

            var powers = new HashSet<PowerType>();
            var ranks = new HashSet<int>();
            foreach (var slot in Powers)
            {
                if (slot.Rank < 2 || slot.Rank > Card.Ace)
                    errors.Add(new LocalizedMessage("validation.rank_invalid", slot.Rank));
                else if (!ranks.Add(slot.Rank))
                    errors.Add(new LocalizedMessage("validation.rank_duplicate", slot.Rank));

                if (!powers.Add(slot.Power))
                    errors.Add(new LocalizedMessage("validation.power_duplicate",
                        new KeyRef(PowerCatalog.LabelKey(slot.Power))));
            }
            return errors;
        }
    }
}

using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    /// <summary>
    /// État et règles d'une manche de Trois-Trois (Shithead).
    /// Aucune dépendance à Unity : l'UI et l'IA ne font qu'appeler cette API.
    /// </summary>
    public sealed class Game
    {
        private readonly List<Player> _players;
        private readonly List<Card> _drawPile;
        private readonly List<Card> _discardPile = new List<Card>();
        private readonly List<int> _finishOrder = new List<int>();
        private readonly bool[] _ready;

        public RulesConfig Rules { get; }
        public GamePhase Phase { get; private set; }
        public int CurrentPlayerIndex { get; private set; }
        public int BurnedCount { get; private set; }

        /// <summary>Sens du jeu : 1 = indices croissants, -1 = décroissants (inversé par le pouvoir Reverse).</summary>
        public int Direction { get; private set; } = 1;

        /// <summary>Nombre de paquets de 52 cartes utilisés, recalculé à chaque manche.</summary>
        public int DeckCount { get; private set; } = 1;

        public int TotalCardCount => Deck.CardsPerDeck * DeckCount;

        public IReadOnlyList<Player> Players => _players;
        public IReadOnlyList<Card> DrawPile => _drawPile;
        public IReadOnlyList<Card> DiscardPile => _discardPile;

        /// <summary>Ordre de sortie ; une fois la partie finie, le dernier est le « Shithead ».</summary>
        public IReadOnlyList<int> FinishOrder => _finishOrder;

        public int? LoserIndex { get; private set; }

        /// <summary>
        /// La manche a été arrêtée par la sécurité anti-blocage : les joueurs restants sont classés par
        /// nombre de cartes (le moins de cartes d'abord), et celui qui en a le plus est le Shithead.
        /// </summary>
        public bool IsStalemate { get; private set; }

        private int[] _minTotals;
        private int _movesWithoutProgress;
        private readonly Dictionary<ulong, int> _seenStates = new Dictionary<ulong, int>();

        public Player CurrentPlayer => _players[CurrentPlayerIndex];

        /// <summary>
        /// Nouvelle manche : mélange et distribution. La phase d'échange commence, sauf si
        /// <see cref="RulesConfig.AllowInitialSwap"/> est désactivé (la manche démarre alors directement).
        /// </summary>
        public Game(IList<Player> players, RulesConfig rules, int seed)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            Rules = rules ?? RulesConfig.Classic;

            var ruleErrors = Rules.Validate();
            if (ruleErrors.Count > 0) throw new ArgumentException(string.Join(" ", ruleErrors), nameof(rules));

            int max = RulesConfig.MaxPlayersFor(Rules.Mode);
            if (players.Count < RulesConfig.MinPlayers || players.Count > max)
                throw new ArgumentException(
                    $"Il faut entre {RulesConfig.MinPlayers} et {max} joueurs dans ce mode.", nameof(players));

            _players = new List<Player>(players);
            _ready = new bool[_players.Count];

            // Le nombre de paquets est recalculé à chaque manche d'après les cartes réellement distribuées.
            int cardsToDeal = 0;
            foreach (var p in _players)
                cardsToDeal += p.FaceDownSlots + p.FaceUpSlots + RulesConfig.HandSize;

            DeckCount = RulesConfig.DeckCountFor(cardsToDeal);
            _drawPile = Deck.Create(DeckCount);
            Deck.Shuffle(_drawPile, new Random(seed));

            foreach (var p in _players)
            {
                p.ClearCards();
                for (int i = 0; i < p.FaceDownSlots; i++) p.FaceDown.Add(DrawTop());
                for (int i = 0; i < p.FaceUpSlots; i++) p.FaceUp.Add(DrawTop());
                for (int i = 0; i < RulesConfig.HandSize; i++) p.Hand.Add(DrawTop());
            }
            InitMinTotals();

            if (Rules.AllowInitialSwap)
            {
                Phase = GamePhase.Swap;
            }
            else
            {
                for (int i = 0; i < _ready.Length; i++) _ready[i] = true;
                StartPlay();
            }
        }

        private Game(IList<Player> players, RulesConfig rules, List<Card> drawPile, List<Card> discard,
            int currentPlayer)
        {
            Rules = rules ?? RulesConfig.Classic;
            _players = new List<Player>(players);
            _ready = new bool[_players.Count];
            _drawPile = drawPile ?? new List<Card>();
            if (discard != null) _discardPile.AddRange(discard);
            CurrentPlayerIndex = currentPlayer;
            Phase = GamePhase.Play;
            InitMinTotals();
        }

        /// <summary>
        /// Crée une partie directement en phase de jeu à partir d'un état donné
        /// (tests, sauvegarde, scénarios). Le haut de la pioche est son dernier élément.
        /// </summary>
        public static Game FromSetup(IList<Player> players, RulesConfig rules, List<Card> drawPile,
            List<Card> discard, int currentPlayer)
            => new Game(players, rules, drawPile, discard, currentPlayer);

        // ---------------------------------------------------------------- Échange

        public bool IsReady(int playerIndex) => _ready[playerIndex];

        /// <summary>Échange une carte de la main avec une carte visible (phase d'échange uniquement).</summary>
        public bool SwapCards(int playerIndex, Card handCard, Card faceUpCard)
        {
            if (Phase != GamePhase.Swap || _ready[playerIndex]) return false;
            var p = _players[playerIndex];
            int h = p.Hand.IndexOf(handCard);
            int f = p.FaceUp.IndexOf(faceUpCard);
            if (h < 0 || f < 0) return false;
            p.Hand[h] = faceUpCard;
            p.FaceUp[f] = handCard;
            return true;
        }

        /// <summary>Le joueur a fini d'échanger. Quand tous sont prêts, la manche démarre.</summary>
        public bool SetReady(int playerIndex)
        {
            if (Phase != GamePhase.Swap) return false;
            _ready[playerIndex] = true;
            foreach (bool r in _ready)
                if (!r) return true;
            StartPlay();
            return true;
        }

        private void StartPlay()
        {
            Phase = GamePhase.Play;
            CurrentPlayerIndex = FindStartingPlayer();
        }

        /// <summary>Le joueur qui possède la plus petite carte sans pouvoir commence.</summary>
        private int FindStartingPlayer()
        {
            int best = 0;
            long bestKey = long.MaxValue;
            for (int i = 0; i < _players.Count; i++)
            {
                foreach (var c in _players[i].Hand)
                {
                    long key = (Rules.PowerOf(c.Rank) != null ? 1000 : 0) + c.Rank * 10 + (int)c.Suit;
                    if (key < bestKey)
                    {
                        bestKey = key;
                        best = i;
                    }
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- Interrogation

        /// <summary>Carte à battre (en ignorant les cartes transparentes), ou null si tout est jouable.</summary>
        public Card? EffectiveTop()
        {
            for (int i = _discardPile.Count - 1; i >= 0; i--)
            {
                if (Rules.PowerOf(_discardPile[i].Rank) == PowerType.Transparent) continue;
                return _discardPile[i];
            }
            return null;
        }

        /// <summary>Valeur à battre, ou null si tout est jouable.</summary>
        public int? RankToBeat() => EffectiveTop()?.Rank;

        public bool CanPlayRank(int rank)
        {
            var power = Rules.PowerOf(rank);
            if (power != null && PowerCatalog.IsAlwaysPlayable(power.Value)) return true;

            var top = EffectiveTop();
            if (top == null) return true;

            var topPower = Rules.PowerOf(top.Value.Rank);
            if (topPower == PowerType.Reset) return true;
            if (topPower == PowerType.LowerOrEqual) return rank <= top.Value.Rank;
            return rank >= top.Value.Rank;
        }

        /// <summary>Zone dans laquelle le joueur doit jouer actuellement.</summary>
        public Zone GetActiveZone(int playerIndex)
        {
            var p = _players[playerIndex];
            if (p.Hand.Count > 0) return Zone.Hand;
            if (p.FaceUp.Count > 0) return Zone.FaceUp;
            return Zone.FaceDown;
        }

        /// <summary>Cartes jouables de la zone active (vide en zone aveugle).</summary>
        public List<Card> GetPlayableCards(int playerIndex)
        {
            var result = new List<Card>();
            var p = _players[playerIndex];
            var zone = GetActiveZone(playerIndex);
            if (zone == Zone.FaceDown) return result;
            foreach (var c in zone == Zone.Hand ? p.Hand : p.FaceUp)
                if (CanPlayRank(c.Rank)) result.Add(c);
            return result;
        }

        public bool HasLegalPlay(int playerIndex) => GetPlayableCards(playerIndex).Count > 0;

        // ---------------------------------------------------------------- Actions

        /// <summary>Joue une ou plusieurs cartes de même rang depuis la main ou les cartes visibles.</summary>
        public MoveResult Play(int playerIndex, IReadOnlyList<Card> cards)
        {
            var error = CheckTurn(playerIndex);
            if (error != MoveError.None) return MoveResult.Fail(error);

            var zone = GetActiveZone(playerIndex);
            if (zone == Zone.FaceDown || !TryTakeFromZone(playerIndex, zone, cards, out var source, out int rank))
                return MoveResult.Fail(MoveError.InvalidCards);

            if (!CanPlayRank(rank)) return MoveResult.Fail(MoveError.NotPlayable);

            foreach (var c in cards) source.Remove(c);
            return Resolve(playerIndex, cards, rank, null);
        }

        /// <summary>
        /// Jeu à la volée : un joueur dont ce n'est pas le tour joue une ou plusieurs cartes de même valeur
        /// que celle du dessus de la défausse. Le tour reprend ensuite après lui.
        /// </summary>
        public MoveResult JumpIn(int playerIndex, IReadOnlyList<Card> cards)
        {
            if (Phase != GamePhase.Play) return MoveResult.Fail(MoveError.WrongPhase);
            if (!Rules.JumpIn || playerIndex == CurrentPlayerIndex || _discardPile.Count == 0)
                return MoveResult.Fail(MoveError.CannotJumpIn);

            var zone = GetActiveZone(playerIndex);
            if (!_players[playerIndex].HasCards || zone == Zone.FaceDown)
                return MoveResult.Fail(MoveError.CannotJumpIn);

            if (!TryTakeFromZone(playerIndex, zone, cards, out var source, out int rank))
                return MoveResult.Fail(MoveError.InvalidCards);
            if (rank != _discardPile[_discardPile.Count - 1].Rank)
                return MoveResult.Fail(MoveError.CannotJumpIn);

            foreach (var c in cards) source.Remove(c);
            CurrentPlayerIndex = playerIndex;
            return Resolve(playerIndex, cards, rank, null);
        }

        /// <summary>Joue à l'aveugle une carte face cachée (uniquement quand il ne reste plus que celles-là).</summary>
        public MoveResult PlayFaceDown(int playerIndex, int index)
        {
            var error = CheckTurn(playerIndex);
            if (error != MoveError.None) return MoveResult.Fail(error);

            var p = _players[playerIndex];
            if (GetActiveZone(playerIndex) != Zone.FaceDown || index < 0 || index >= p.FaceDown.Count)
                return MoveResult.Fail(MoveError.InvalidCards);

            var card = p.FaceDown[index];
            p.FaceDown.RemoveAt(index);

            if (CanPlayRank(card.Rank))
                return Resolve(playerIndex, new[] { card }, card.Rank, card);

            // Carte injouable : elle rejoint la défausse que le joueur ramasse en entier.
            _discardPile.Add(card);
            p.Hand.AddRange(_discardPile);
            _discardPile.Clear();
            AdvanceTurn();
            RegisterMove(playerIndex, false);
            return new MoveResult(MoveError.None, pickedUp: true, revealed: card);
        }

        /// <summary>Ramasse la défausse. Autorisé uniquement s'il n'existe aucun coup légal.</summary>
        public MoveResult PickUpPile(int playerIndex)
        {
            var error = CheckTurn(playerIndex);
            if (error != MoveError.None) return MoveResult.Fail(error);
            if (GetActiveZone(playerIndex) == Zone.FaceDown) return MoveResult.Fail(MoveError.InvalidCards);
            if (HasLegalPlay(playerIndex)) return MoveResult.Fail(MoveError.MustPlay);

            _players[playerIndex].Hand.AddRange(_discardPile);
            _discardPile.Clear();
            AdvanceTurn();
            RegisterMove(playerIndex, false);
            return new MoveResult(MoveError.None, pickedUp: true);
        }

        // ---------------------------------------------------------------- Résolution

        private bool TryTakeFromZone(int playerIndex, Zone zone, IReadOnlyList<Card> cards,
            out List<Card> source, out int rank)
        {
            source = zone == Zone.Hand ? _players[playerIndex].Hand : _players[playerIndex].FaceUp;
            rank = 0;
            if (cards == null || cards.Count == 0) return false;

            rank = cards[0].Rank;
            var check = new List<Card>(source);
            foreach (var c in cards)
                if (c.Rank != rank || !check.Remove(c)) return false;
            return true;
        }

        private MoveResult Resolve(int playerIndex, IReadOnlyList<Card> played, int rank, Card? revealed)
        {
            var p = _players[playerIndex];
            _discardPile.AddRange(played);

            while (_drawPile.Count > 0 && p.Hand.Count < RulesConfig.HandSize)
                p.Hand.Add(DrawTop());

            PowerType? power = Rules.PowerOf(rank);
            bool burned = power == PowerType.Burn || (Rules.FourOfAKindBurns && TopFourMatch());
            if (burned)
            {
                BurnedCount += _discardPile.Count;
                _discardPile.Clear();
            }

            bool finished = false;
            if (!p.HasCards)
            {
                finished = true;
                _finishOrder.Add(playerIndex);
                CheckGameOver();
            }

            int? forcedPickUp = null;
            if (Phase != GamePhase.Finished)
            {
                if (burned)
                {
                    // Une défausse brûlée donne la main au même joueur, sauf s'il vient de sortir.
                    if (finished) AdvanceTurn();
                }
                else if (power == PowerType.Skip)
                {
                    AdvanceTurn(1 + played.Count);
                }
                else if (power == PowerType.Reverse)
                {
                    if (played.Count % 2 == 1) Direction = -Direction;
                    AdvanceTurn();
                }
                else if (power == PowerType.ForcePickUp)
                {
                    AdvanceTurn();
                    var victim = _players[CurrentPlayerIndex];
                    victim.Hand.AddRange(_discardPile);
                    _discardPile.Clear();
                    forcedPickUp = CurrentPlayerIndex;
                    AdvanceTurn();
                }
                else if (power == PowerType.PlayAgain)
                {
                    if (finished) AdvanceTurn();
                }
                else
                {
                    AdvanceTurn();
                }
            }

            RegisterMove(playerIndex, finished);

            return new MoveResult(MoveError.None, burned: burned, playerFinished: finished, revealed: revealed,
                power: power, forcedPickUpPlayer: forcedPickUp);
        }

        // ---------------------------------------------------------------- Sécurité anti-blocage

        private void InitMinTotals()
        {
            _minTotals = new int[_players.Count];
            for (int i = 0; i < _players.Count; i++) _minTotals[i] = _players[i].TotalCards;
            _movesWithoutProgress = 0;
            _seenStates.Clear();
        }

        /// <summary>
        /// Un coup est un « progrès » si le joueur sort ou atteint un nouveau minimum de cartes pour lui.
        /// Une longue série de coups sans progrès (cycle de ramassages) met fin à la manche.
        /// </summary>
        private void RegisterMove(int playerIndex, bool finished)
        {
            if (Phase != GamePhase.Play) return;

            int total = _players[playerIndex].TotalCards;
            if (finished || total < _minTotals[playerIndex])
            {
                _minTotals[playerIndex] = Math.Min(total, _minTotals[playerIndex]);
                _movesWithoutProgress = 0;
                _seenStates.Clear();
                return;
            }

            ulong state = StateHash();
            _seenStates.TryGetValue(state, out int seen);
            _seenStates[state] = ++seen;
            if (seen >= RulesConfig.StalemateRepeatLimit)
            {
                EndByStalemate();
                return;
            }

            int active = 0;
            foreach (var p in _players)
                if (p.HasCards) active++;

            if (++_movesWithoutProgress >= RulesConfig.StalemateMovesPerPlayer * active)
                EndByStalemate();
        }

        /// <summary>
        /// Empreinte de la situation de jeu. Les cartes d'une main ou de la zone visible comptent sans ordre ;
        /// la défausse compte avec son ordre. Les cartes cachées ne bougent pas sans progrès : leur nombre suffit.
        /// </summary>
        private ulong StateHash()
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                h = Mix(h, (ulong)CurrentPlayerIndex);
                h = Mix(h, Direction == 1 ? 1UL : 2UL);
                h = Mix(h, (ulong)_drawPile.Count);
                foreach (var c in _discardPile) h = Mix(h, CardId(c));
                foreach (var p in _players)
                {
                    ulong hand = 0, up = 0;
                    foreach (var c in p.Hand) hand += Scramble(CardId(c));
                    foreach (var c in p.FaceUp) up += Scramble(CardId(c));
                    h = Mix(h, hand);
                    h = Mix(h, up);
                    h = Mix(h, (ulong)p.FaceDown.Count);
                }
                return h;
            }
        }

        private static ulong CardId(Card c) => (ulong)(c.Rank * 4 + (int)c.Suit + 1);

        private static ulong Mix(ulong h, ulong v) => unchecked((h ^ Scramble(v)) * 1099511628211UL);

        private static ulong Scramble(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        private void EndByStalemate()
        {
            var remaining = new List<int>();
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].HasCards) remaining.Add(i);

            // Le moins de cartes d'abord ; à égalité, l'ordre de la table.
            remaining.Sort((a, b) =>
            {
                int byCards = _players[a].TotalCards.CompareTo(_players[b].TotalCards);
                return byCards != 0 ? byCards : a.CompareTo(b);
            });

            _finishOrder.AddRange(remaining);
            LoserIndex = remaining[remaining.Count - 1];
            IsStalemate = true;
            Phase = GamePhase.Finished;
        }

        private bool TopFourMatch()
        {
            int n = _discardPile.Count;
            if (n < 4) return false;
            int rank = _discardPile[n - 1].Rank;
            for (int i = n - 2; i >= n - 4; i--)
                if (_discardPile[i].Rank != rank) return false;
            return true;
        }

        private void CheckGameOver()
        {
            int remaining = 0;
            int last = -1;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].HasCards)
                {
                    remaining++;
                    last = i;
                }
            }

            if (remaining > 1) return;
            Phase = GamePhase.Finished;
            if (remaining == 1)
            {
                LoserIndex = last;
                _finishOrder.Add(last);
            }
        }

        /// <summary>Passe la main au joueur suivant (dans le sens du jeu), <paramref name="steps"/> fois.</summary>
        private void AdvanceTurn(int steps = 1)
        {
            if (Phase == GamePhase.Finished) return;
            int n = _players.Count;
            for (int s = 0; s < steps; s++)
            {
                for (int k = 1; k <= n; k++)
                {
                    int next = (((CurrentPlayerIndex + Direction * k) % n) + n) % n;
                    if (_players[next].HasCards)
                    {
                        CurrentPlayerIndex = next;
                        break;
                    }
                }
            }
        }

        private MoveError CheckTurn(int playerIndex)
        {
            if (Phase != GamePhase.Play) return MoveError.WrongPhase;
            if (playerIndex != CurrentPlayerIndex) return MoveError.NotYourTurn;
            return MoveError.None;
        }

        private Card DrawTop()
        {
            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }
    }
}

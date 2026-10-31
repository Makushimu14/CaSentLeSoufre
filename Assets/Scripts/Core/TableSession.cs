using System;
using System.Collections.Generic;
using CaSentLeSoufre.Core.Ai;

namespace CaSentLeSoufre.Core
{
    /// <summary>Rythme de jeu des IA, en secondes : plus la difficulté est haute, plus elles sont rapides.</summary>
    public sealed class TableTimings
    {
        public float EasyPlayDelay = 3.0f;
        public float NormalPlayDelay = 2.0f;
        public float HardPlayDelay = 1.2f;

        /// <summary>Temps de réaction d'une IA pour jouer à la volée (les IA faciles ne le font jamais).</summary>
        public float NormalReaction = 1.2f;
        public float HardReaction = 0.7f;

        /// <summary>Variation aléatoire des délais (0,2 = plus ou moins 20 %).</summary>
        public float Jitter = 0.2f;

        public float PlayDelay(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return EasyPlayDelay;
                case AiDifficulty.Normal: return NormalPlayDelay;
                default: return HardPlayDelay;
            }
        }

        public float ReactionDelay(AiDifficulty difficulty) =>
            difficulty == AiDifficulty.Hard ? HardReaction : NormalReaction;

        /// <summary>Aucun délai ni variation : une action par appel de Tick (tests et simulations).</summary>
        public static TableTimings Instant() => new TableTimings
        {
            EasyPlayDelay = 0, NormalPlayDelay = 0, HardPlayDelay = 0,
            NormalReaction = 0, HardReaction = 0, Jitter = 0
        };
    }

    public enum TableEventKind
    {
        RoundStarted,
        PlayStarted,
        Played,
        JumpedIn,
        PickedUp,
        BlindPlayed,
        RoundFinished
    }

    /// <summary>Ce qui vient de se passer à la table, pour l'affichage, les animations et le journal.</summary>
    public readonly struct TableEvent
    {
        public TableEventKind Kind { get; }
        public int PlayerIndex { get; }
        public MoveResult Result { get; }

        /// <summary>Pour <see cref="TableEventKind.RoundFinished"/> : c'était la dernière manche de la partie.</summary>
        public bool MatchOver { get; }

        public TableEvent(TableEventKind kind, int playerIndex, MoveResult result = default, bool matchOver = false)
        {
            Kind = kind;
            PlayerIndex = playerIndex;
            Result = result;
            MatchOver = matchOver;
        }
    }

    /// <summary>
    /// Fait vivre une manche à l'écran : les IA jouent à leur rythme, le joueur humain (toujours le joueur 0)
    /// agit quand il veut, et le jeu à la volée est ouvert à tous tant que le joueur dont c'est le tour n'a pas
    /// posé. Le temps est fourni par <see cref="Tick"/>, ce qui rend le tout testable avec une horloge simulée.
    /// </summary>
    public sealed class TableSession
    {
        public const int HumanIndex = 0;

        private readonly Match _match;
        private readonly TableTimings _timings;
        private readonly Random _rng;
        private readonly List<TableEvent> _events = new List<TableEvent>();

        private Game _game;
        private float _turnTimer;
        private float _turnDelay;
        private float[] _reactionTimers = new float[0];
        private List<Card>[] _reactionCards = new List<Card>[0];

        public Match Match => _match;
        public Game Game => _game;

        /// <summary>Vrai tant qu'aucune manche n'est en cours (avant la première, ou une fois la manche enregistrée).</summary>
        public bool RoundOver { get; private set; } = true;

        public TableSession(Match match, TableTimings timings, int seed)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _timings = timings ?? new TableTimings();
            _rng = new Random(seed);
        }

        private TableSession(Game game, TableTimings timings, int seed)
        {
            _timings = timings ?? new TableTimings();
            _rng = new Random(seed);
            _game = game;
            RoundOver = false;
            ResetTimers();
        }

        /// <summary>
        /// Reprend une partie restaurée (<see cref="Match.FromData"/>) là où elle s'était arrêtée : au milieu d'une
        /// manche, pendant l'échange, ou entre deux manches (résultats affichés). Les délais des IA repartent à zéro.
        /// </summary>
        public static TableSession Resume(Match match, TableTimings timings, int seed)
        {
            var session = new TableSession(match, timings, seed);
            session._game = match.CurrentGame;
            session.RoundOver = session._game == null || session._game.Phase == GamePhase.Finished;
            session.ResetTimers();
            return session;
        }

        /// <summary>Session sur une manche déjà préparée, sans partie (tests et scénarios).</summary>
        public static TableSession ForGame(Game game, TableTimings timings, int seed) =>
            new TableSession(game, timings, seed);

        public bool IsHumanTurn => _game != null && _game.Phase == GamePhase.Play
                                   && _game.CurrentPlayerIndex == HumanIndex;

        // ---------------------------------------------------------------- Manche

        /// <summary>Distribue la manche suivante ; les IA font leur échange, le joueur doit valider le sien.</summary>
        public void StartRound()
        {
            if (_match == null) throw new InvalidOperationException("Session sans partie.");
            if (!RoundOver) throw new InvalidOperationException("La manche en cours n'est pas terminée.");

            _game = _match.StartNextRound();
            RoundOver = false;
            Emit(TableEventKind.RoundStarted, HumanIndex);

            for (int i = 0; i < _game.Players.Count; i++)
                if (!_game.Players[i].IsHuman) AiBrain.DoSwap(_game, i, _rng);

            if (_game.Phase == GamePhase.Play) BeginPlay();
        }

        public bool HumanSwap(Card handCard, Card faceUpCard) =>
            _game != null && _game.SwapCards(HumanIndex, handCard, faceUpCard);

        /// <summary>Le joueur a fini son échange ; la manche démarre si tout le monde est prêt.</summary>
        public bool HumanReady()
        {
            if (_game == null || _game.Phase != GamePhase.Swap) return false;
            bool ok = _game.SetReady(HumanIndex);
            if (_game.Phase == GamePhase.Play) BeginPlay();
            return ok;
        }

        private void BeginPlay()
        {
            Emit(TableEventKind.PlayStarted, _game.CurrentPlayerIndex);
            ResetTimers();
        }

        // ---------------------------------------------------------------- Actions du joueur

        public MoveResult HumanPlay(IReadOnlyList<Card> cards) =>
            AfterHumanMove(TableEventKind.Played, _game.Play(HumanIndex, cards));

        public MoveResult HumanPlayFaceDown(int index) =>
            AfterHumanMove(TableEventKind.BlindPlayed, _game.PlayFaceDown(HumanIndex, index));

        public MoveResult HumanPickUp() =>
            AfterHumanMove(TableEventKind.PickedUp, _game.PickUpPile(HumanIndex));

        public MoveResult HumanJumpIn(IReadOnlyList<Card> cards) =>
            AfterHumanMove(TableEventKind.JumpedIn, _game.JumpIn(HumanIndex, cards));

        /// <summary>
        /// Carte du joueur qui peut être posée à la volée en ce moment : la dernière d'un jeu complet de même
        /// valeur dont le reste est déjà sur la défausse, tant que le joueur dont c'est le tour n'a pas posé.
        /// Vide si ce n'est pas possible.
        /// </summary>
        public List<Card> HumanJumpInCards() =>
            _game == null ? new List<Card>() : _game.GetJumpInCards(HumanIndex);

        private MoveResult AfterHumanMove(TableEventKind kind, MoveResult result)
        {
            if (!result.Ok) return result;
            Emit(kind, HumanIndex, result);
            AfterMove();
            return result;
        }

        // ---------------------------------------------------------------- Temps réel

        /// <summary>Fait avancer le temps de <paramref name="dt"/> secondes : les IA réagissent et jouent.</summary>
        public void Tick(float dt)
        {
            if (_game == null || RoundOver || _game.Phase != GamePhase.Play) return;

            // Jeu à la volée des IA : chacune a décidé à l'avance, avec son temps de réaction.
            for (int i = 0; i < _reactionCards.Length; i++)
            {
                if (_reactionCards[i] == null) continue;

                _reactionTimers[i] -= dt;
                if (_reactionTimers[i] > 0) continue;

                var cards = _reactionCards[i];
                _reactionCards[i] = null;
                var result = _game.JumpIn(i, cards);
                if (result.Ok)
                {
                    Emit(TableEventKind.JumpedIn, i, result);
                    AfterMove();
                    return;
                }
            }

            // Tour d'une IA : elle prend son temps selon sa difficulté.
            if (_game.CurrentPlayer.IsHuman) return;

            _turnTimer += dt;
            if (_turnTimer < _turnDelay) return;

            int player = _game.CurrentPlayerIndex;
            var zone = _game.GetActiveZone(player);
            var move = AiBrain.TakeTurn(_game, _rng);
            if (!move.Ok) return;

            Emit(zone == Zone.FaceDown ? TableEventKind.BlindPlayed
                : move.PickedUp ? TableEventKind.PickedUp : TableEventKind.Played, player, move);
            AfterMove();
        }

        /// <summary>Après chaque coup : fin de manche, ou nouveaux délais et nouvelles envies de jouer à la volée.</summary>
        private void AfterMove()
        {
            if (_game.Phase == GamePhase.Finished)
            {
                FinishRound();
                return;
            }
            ResetTimers();
        }

        private void FinishRound()
        {
            RoundOver = true;
            ClearReactions();
            if (_match != null) _match.CompleteRound();
            _events.Add(new TableEvent(TableEventKind.RoundFinished, HumanIndex, default,
                _match == null || _match.IsOver));
        }

        private void ResetTimers()
        {
            _turnTimer = 0;
            _turnDelay = 0;
            ClearReactions();
            if (_game == null || _game.Phase != GamePhase.Play) return;

            var current = _game.CurrentPlayer;
            if (!current.IsHuman) _turnDelay = Jitter(_timings.PlayDelay(current.Difficulty));

            int count = _game.Players.Count;
            _reactionTimers = new float[count];
            _reactionCards = new List<Card>[count];
            for (int i = 0; i < count; i++)
            {
                if (_game.Players[i].IsHuman || i == _game.CurrentPlayerIndex) continue;

                var cards = AiBrain.ChooseJumpIn(_game, i, _rng);
                if (cards == null) continue;
                _reactionCards[i] = cards;
                _reactionTimers[i] = Jitter(_timings.ReactionDelay(_game.Players[i].Difficulty));
            }
        }

        private void ClearReactions()
        {
            _reactionTimers = new float[0];
            _reactionCards = new List<Card>[0];
        }

        private float Jitter(float seconds)
        {
            if (seconds <= 0 || _timings.Jitter <= 0) return seconds;
            double factor = 1.0 + (_rng.NextDouble() * 2 - 1) * _timings.Jitter;
            return (float)(seconds * factor);
        }

        // ---------------------------------------------------------------- Événements

        private void Emit(TableEventKind kind, int playerIndex, MoveResult result = default) =>
            _events.Add(new TableEvent(kind, playerIndex, result));

        /// <summary>Récupère (et vide) les événements survenus depuis le dernier appel, dans l'ordre.</summary>
        public List<TableEvent> DrainEvents()
        {
            var copy = new List<TableEvent>(_events);
            _events.Clear();
            return copy;
        }
    }
}

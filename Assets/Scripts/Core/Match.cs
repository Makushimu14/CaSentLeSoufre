using System;
using System.Collections.Generic;
using System.Linq;

namespace CaSentLeSoufre.Core
{
    public readonly struct StandingEntry
    {
        public int PlayerIndex { get; }
        public string Name { get; }
        public int Points { get; }
        public int ShitheadCount { get; }

        /// <summary>Classement à partir de 1 (les ex æquo parfaits se suivent dans l'ordre de la table).</summary>
        public int Place { get; }

        public StandingEntry(int playerIndex, string name, int points, int shitheadCount, int place)
        {
            PlayerIndex = playerIndex;
            Name = name;
            Points = points;
            ShitheadCount = shitheadCount;
            Place = place;
        }
    }

    /// <summary>
    /// Une partie complète : enchaîne les manches (<see cref="MatchSettings.RoundCount"/>) et cumule les scores.
    /// Barème d'une manche : 1er = N-1 points, 2e = N-2, ... , le Shithead (dernier) = 0.
    /// Au classement : plus de points d'abord, puis moins de fois Shithead.
    /// </summary>
    public sealed class Match
    {
        private readonly List<Player> _players = new List<Player>();
        private readonly Random _rng;
        private readonly int[] _points;
        private readonly int[] _shitheadCount;
        private bool _roundInProgress;

        public MatchSettings Settings { get; }
        public IReadOnlyList<Player> Players => _players;

        /// <summary>Manche en cours ou dernière manche jouée (null avant la première).</summary>
        public Game CurrentGame { get; private set; }

        /// <summary>Numéro de la manche en cours (0 avant la première).</summary>
        public int RoundNumber { get; private set; }

        public int RoundsCompleted { get; private set; }
        public bool IsOver => RoundsCompleted >= Settings.RoundCount;

        public Match(MatchSettings settings, int seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var errors = settings.Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(settings));

            Settings = settings;
            _rng = new Random(seed);
            _players.Add(new Player(settings.HumanName.Trim(), isHuman: true));
            foreach (var ai in settings.Opponents)
                _players.Add(new Player(ai.Name.Trim(), isHuman: false, difficulty: ai.Difficulty));

            _points = new int[_players.Count];
            _shitheadCount = new int[_players.Count];
        }

        /// <summary>Distribue la manche suivante. Impossible si la précédente n'est pas enregistrée.</summary>
        public Game StartNextRound()
        {
            if (IsOver) throw new InvalidOperationException("La partie est terminée.");
            if (_roundInProgress) throw new InvalidOperationException("La manche en cours n'est pas terminée.");

            RoundNumber++;
            _roundInProgress = true;
            CurrentGame = new Game(_players, Settings.Rules, _rng.Next());
            return CurrentGame;
        }

        /// <summary>Enregistre les points de la manche une fois celle-ci terminée.</summary>
        public void CompleteRound()
        {
            if (!_roundInProgress || CurrentGame == null || CurrentGame.Phase != GamePhase.Finished)
                throw new InvalidOperationException("Aucune manche terminée à enregistrer.");

            int n = _players.Count;
            var order = CurrentGame.FinishOrder;
            for (int place = 0; place < order.Count; place++)
                _points[order[place]] += n - 1 - place;

            if (CurrentGame.LoserIndex.HasValue)
                _shitheadCount[CurrentGame.LoserIndex.Value]++;

            if (Settings.Rules.Mode == GameMode.Demon)
                ApplyDemonPenalties(order[0], CurrentGame.LoserIndex);

            RoundsCompleted++;
            _roundInProgress = false;
        }

        /// <summary>
        /// Mode démon : le dernier reçoit une carte cachée et une visible de plus (sans limite) ;
        /// le premier en perd une de chaque (minimum 1 de chaque).
        /// </summary>
        private void ApplyDemonPenalties(int winnerIndex, int? loserIndex)
        {
            var winner = _players[winnerIndex];
            winner.SetSlots(Math.Max(1, winner.FaceDownSlots - 1), Math.Max(1, winner.FaceUpSlots - 1));

            if (loserIndex.HasValue)
            {
                var loser = _players[loserIndex.Value];
                loser.SetSlots(loser.FaceDownSlots + 1, loser.FaceUpSlots + 1);
            }
        }

        public IReadOnlyList<StandingEntry> GetStandings()
        {
            var sorted = Enumerable.Range(0, _players.Count)
                .OrderByDescending(i => _points[i])
                .ThenBy(i => _shitheadCount[i])
                .ThenBy(i => i)
                .ToList();

            var result = new List<StandingEntry>(sorted.Count);
            for (int place = 0; place < sorted.Count; place++)
            {
                int i = sorted[place];
                result.Add(new StandingEntry(i, _players[i].Name, _points[i], _shitheadCount[i], place + 1));
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    /// <summary>
    /// Compteurs d'un joueur dans un mode (ou cumulés). Une manche est gagnée par le premier à sortir et perdue
    /// par le Shithead ; une partie est gagnée par le premier du classement final et perdue par le dernier.
    /// Les autres places ne sont ni victoire ni défaite.
    /// </summary>
    [Serializable]
    public sealed class StatLine
    {
        public int RoundsPlayed;
        public int RoundWins;
        public int RoundLosses;
        public int MatchesPlayed;
        public int MatchWins;
        public int MatchLosses;

        /// <summary>Taux entre 0 et 1 (0 s'il n'y a encore rien de joué).</summary>
        public double RoundWinRate => Rate(RoundWins, RoundsPlayed);
        public double RoundLossRate => Rate(RoundLosses, RoundsPlayed);
        public double MatchWinRate => Rate(MatchWins, MatchesPlayed);
        public double MatchLossRate => Rate(MatchLosses, MatchesPlayed);

        public void Add(StatLine other)
        {
            RoundsPlayed += other.RoundsPlayed;
            RoundWins += other.RoundWins;
            RoundLosses += other.RoundLosses;
            MatchesPlayed += other.MatchesPlayed;
            MatchWins += other.MatchWins;
            MatchLosses += other.MatchLosses;
        }

        public StatLine Copy()
        {
            var copy = new StatLine();
            copy.Add(this);
            return copy;
        }

        private static double Rate(int count, int total) => total == 0 ? 0 : (double)count / total;
    }

    /// <summary>Statistiques d'un joueur, humain ou IA (une IA par nom et par difficulté).</summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public string Name;
        public bool IsHuman;

        /// <summary>Niveau de l'IA ; sans objet pour un humain.</summary>
        public AiDifficulty Difficulty = AiDifficulty.Normal;

        public StatLine Classic = new StatLine();
        public StatLine Demon = new StatLine();

        public StatLine Line(GameMode mode) => mode == GameMode.Demon ? Demon : Classic;

        /// <summary>Tous modes confondus (nouvelle instance à chaque appel).</summary>
        public StatLine Overall()
        {
            var total = new StatLine();
            total.Add(Classic);
            total.Add(Demon);
            return total;
        }
    }

    /// <summary>Forme enregistrable du carnet de statistiques (sérialisable en JSON par la couche Unity).</summary>
    [Serializable]
    public sealed class StatsSnapshot
    {
        public int Version = 1;
        public List<PlayerStats> Players = new List<PlayerStats>();
    }

    /// <summary>
    /// Carnet de statistiques de tous les joueurs, y compris les IA. Alimenté automatiquement par
    /// <see cref="Match"/> ; seules les manches terminées et les parties menées jusqu'au bout sont comptées.
    /// </summary>
    public sealed class StatsBook
    {
        private readonly List<PlayerStats> _players = new List<PlayerStats>();
        private readonly Dictionary<string, PlayerStats> _byKey = new Dictionary<string, PlayerStats>();

        public IReadOnlyList<PlayerStats> Players => _players;

        /// <summary>Identité d'un joueur : humain ou IA (avec sa difficulté) + nom sans tenir compte de la casse.</summary>
        public static string KeyOf(string name, bool isHuman, AiDifficulty difficulty)
        {
            string n = (name ?? string.Empty).Trim().ToLowerInvariant();
            return isHuman ? "H:" + n : "A:" + (int)difficulty + ":" + n;
        }

        public PlayerStats Find(string name, bool isHuman, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            _byKey.TryGetValue(KeyOf(name, isHuman, difficulty), out var stats);
            return stats;
        }

        public PlayerStats GetOrCreate(Player player)
        {
            string key = KeyOf(player.Name, player.IsHuman, player.Difficulty);
            if (!_byKey.TryGetValue(key, out var stats))
            {
                stats = new PlayerStats
                {
                    IsHuman = player.IsHuman,
                    Difficulty = player.IsHuman ? AiDifficulty.Normal : player.Difficulty
                };
                _byKey[key] = stats;
                _players.Add(stats);
            }
            stats.Name = player.Name.Trim();
            return stats;
        }

        /// <summary>Enregistre une manche terminée : tous les joueurs ont joué, le premier gagne, le Shithead perd.</summary>
        public void RecordRound(IReadOnlyList<Player> players, IReadOnlyList<int> finishOrder, int? loserIndex,
            GameMode mode)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var line = GetOrCreate(players[i]).Line(mode);
                line.RoundsPlayed++;
                if (finishOrder.Count > 0 && finishOrder[0] == i) line.RoundWins++;
                if (loserIndex == i) line.RoundLosses++;
            }
        }

        /// <summary>Enregistre une partie terminée : le premier du classement gagne, le dernier perd.</summary>
        public void RecordMatch(IReadOnlyList<Player> players, IReadOnlyList<StandingEntry> standings, GameMode mode)
        {
            foreach (var entry in standings)
            {
                var line = GetOrCreate(players[entry.PlayerIndex]).Line(mode);
                line.MatchesPlayed++;
                if (entry.Place == 1) line.MatchWins++;
                if (entry.Place == standings.Count) line.MatchLosses++;
            }
        }

        public void Clear()
        {
            _players.Clear();
            _byKey.Clear();
        }

        // ---------------------------------------------------------------- Sauvegarde

        public StatsSnapshot ToSnapshot()
        {
            var snapshot = new StatsSnapshot();
            foreach (var p in _players)
            {
                snapshot.Players.Add(new PlayerStats
                {
                    Name = p.Name,
                    IsHuman = p.IsHuman,
                    Difficulty = p.Difficulty,
                    Classic = p.Classic.Copy(),
                    Demon = p.Demon.Copy()
                });
            }
            return snapshot;
        }

        /// <summary>Reconstruit un carnet ; les entrées sans nom sont ignorées et les doublons additionnés.</summary>
        public static StatsBook FromSnapshot(StatsSnapshot snapshot)
        {
            var book = new StatsBook();
            if (snapshot?.Players == null) return book;

            foreach (var dto in snapshot.Players)
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) continue;

                var player = new Player(dto.Name, dto.IsHuman, dto.Difficulty);
                var stats = book.GetOrCreate(player);
                if (dto.Classic != null) stats.Classic.Add(dto.Classic);
                if (dto.Demon != null) stats.Demon.Add(dto.Demon);
            }
            return book;
        }
    }
}

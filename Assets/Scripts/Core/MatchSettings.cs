using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    public sealed class AiSetup
    {
        public string Name;
        public AiDifficulty Difficulty = AiDifficulty.Normal;

        public AiSetup(string name, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            Name = name;
            Difficulty = difficulty;
        }
    }

    /// <summary>Réglages choisis avant de lancer une partie (plusieurs manches).</summary>
    public sealed class MatchSettings
    {
        public const int MinRounds = 1;
        public const int MaxRounds = 999;
        public const int MaxNameLength = 20;

        /// <summary>Noms proposés par défaut aux IA (9 = le maximum d'adversaires en mode démon).</summary>
        public static readonly string[] SuggestedAiNames =
        {
            "Azazel", "Baal", "Lilith", "Mammon", "Asmodée", "Belphégor", "Léviathan", "Astaroth", "Bélial"
        };

        public RulesConfig Rules = RulesConfig.Classic;
        public int RoundCount = 1;
        public string HumanName = "Joueur";

        /// <summary>Adversaires IA, dans l'ordre de la table (le joueur humain est toujours le joueur 0).</summary>
        public List<AiSetup> Opponents = new List<AiSetup>();

        public int PlayerCount => 1 + Opponents.Count;
        public int MaxPlayers => RulesConfig.MaxPlayersFor(Rules.Mode);

        /// <summary>Réglages prêts à l'emploi : noms suggérés, difficulté normale.</summary>
        public static MatchSettings CreateDefault(GameMode mode, int playerCount, int roundCount = 3)
        {
            var s = new MatchSettings { Rules = new RulesConfig { Mode = mode }, RoundCount = roundCount };
            for (int i = 0; i < playerCount - 1; i++)
                s.Opponents.Add(new AiSetup(SuggestedAiNames[i % SuggestedAiNames.Length]));
            return s;
        }

        /// <summary>Liste des problèmes (en français, affichables) ; vide si les réglages sont valides.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Rules == null) errors.Add("Les règles sont manquantes.");
            else errors.AddRange(Rules.Validate());

            if (Rules != null && PlayerCount < RulesConfig.MinPlayers || PlayerCount > MaxPlayers)
                errors.Add($"Il faut entre {RulesConfig.MinPlayers} et {MaxPlayers} joueurs dans ce mode " +
                           $"(vous en avez {PlayerCount}).");

            if (RoundCount < MinRounds || RoundCount > MaxRounds)
                errors.Add($"Le nombre de manches doit être compris entre {MinRounds} et {MaxRounds}.");

            var names = new List<string> { HumanName };
            foreach (var o in Opponents) names.Add(o?.Name);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in names)
            {
                string name = raw?.Trim();
                if (string.IsNullOrEmpty(name)) errors.Add("Chaque joueur doit avoir un nom.");
                else if (name.Length > MaxNameLength)
                    errors.Add($"Le nom « {name} » dépasse {MaxNameLength} caractères.");
                else if (!seen.Add(name)) errors.Add($"Le nom « {name} » est utilisé plusieurs fois.");
            }

            return errors;
        }
    }
}

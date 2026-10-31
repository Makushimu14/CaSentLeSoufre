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

        /// <summary>Copie indépendante des réglages (règles et adversaires compris).</summary>
        public MatchSettings Clone()
        {
            var copy = new MatchSettings
            {
                Rules = Rules?.Clone(),
                RoundCount = RoundCount,
                HumanName = HumanName
            };
            foreach (var o in Opponents) copy.Opponents.Add(new AiSetup(o.Name, o.Difficulty));
            return copy;
        }

        /// <summary>Copie enregistrable des réglages.</summary>
        public SettingsData ToData()
        {
            var data = new SettingsData
            {
                Mode = (int)Rules.Mode,
                FourOfAKindBurns = Rules.FourOfAKindBurns,
                JumpIn = Rules.JumpIn,
                AllowInitialSwap = Rules.AllowInitialSwap,
                RoundCount = RoundCount,
                HumanName = HumanName
            };
            foreach (var slot in Rules.Powers)
                data.Powers.Add(new PowerPreference { Power = (int)slot.Power, Rank = slot.Rank });
            foreach (var ai in Opponents)
                data.Opponents.Add(new AiPreference { Name = ai.Name, Difficulty = (int)ai.Difficulty });
            return data;
        }

        /// <summary>Rétablit des réglages enregistrés ; lève une exception s'ils sont invalides.</summary>
        public static MatchSettings FromData(SettingsData data)
        {
            if (data == null) throw new InvalidOperationException("Réglages enregistrés manquants.");
            if (!Enum.IsDefined(typeof(GameMode), data.Mode)) throw new InvalidOperationException("Mode inconnu.");

            var rules = new RulesConfig
            {
                Mode = (GameMode)data.Mode,
                FourOfAKindBurns = data.FourOfAKindBurns,
                JumpIn = data.JumpIn,
                AllowInitialSwap = data.AllowInitialSwap,
                Powers = new List<PowerSlot>()
            };
            foreach (var saved in data.Powers ?? new List<PowerPreference>())
            {
                if (saved == null || !Enum.IsDefined(typeof(PowerType), saved.Power))
                    throw new InvalidOperationException("Pouvoir inconnu.");
                rules.Powers.Add(new PowerSlot((PowerType)saved.Power, saved.Rank));
            }

            var settings = new MatchSettings
            {
                Rules = rules,
                RoundCount = data.RoundCount,
                HumanName = data.HumanName ?? string.Empty
            };
            foreach (var saved in data.Opponents ?? new List<AiPreference>())
            {
                if (saved == null || !Enum.IsDefined(typeof(AiDifficulty), saved.Difficulty))
                    throw new InvalidOperationException("Adversaire invalide.");
                settings.Opponents.Add(new AiSetup(saved.Name ?? string.Empty, (AiDifficulty)saved.Difficulty));
            }

            var errors = settings.Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Réglages enregistrés invalides : " + string.Join(" ", errors));
            return settings;
        }

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

        /// <summary>Problèmes détectés, sous forme de clés de traduction ; liste vide si les réglages sont valides.</summary>
        public List<LocalizedMessage> Validate()
        {
            var errors = new List<LocalizedMessage>();

            if (Rules == null) errors.Add(new LocalizedMessage("validation.rules_missing"));
            else errors.AddRange(Rules.Validate());

            if (Rules != null && (PlayerCount < RulesConfig.MinPlayers || PlayerCount > MaxPlayers))
                errors.Add(new LocalizedMessage("validation.players_count",
                    RulesConfig.MinPlayers, MaxPlayers, PlayerCount));

            if (RoundCount < MinRounds || RoundCount > MaxRounds)
                errors.Add(new LocalizedMessage("validation.rounds_range", MinRounds, MaxRounds));

            var names = new List<string> { HumanName };
            foreach (var o in Opponents) names.Add(o?.Name);
            errors.AddRange(ValidateNames(names));

            return errors;
        }

        /// <summary>Noms non vides, d'au plus <see cref="MaxNameLength"/> caractères et uniques (casse ignorée).</summary>
        public static List<LocalizedMessage> ValidateNames(IEnumerable<string> names)
        {
            var errors = new List<LocalizedMessage>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in names)
            {
                string name = raw?.Trim();
                if (string.IsNullOrEmpty(name))
                    errors.Add(new LocalizedMessage("validation.name_required"));
                else if (name.Length > MaxNameLength)
                    errors.Add(new LocalizedMessage("validation.name_too_long", name, MaxNameLength));
                else if (!seen.Add(name))
                    errors.Add(new LocalizedMessage("validation.name_duplicate", name));
            }
            return errors;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace CaSentLeSoufre.Core
{
    /// <summary>
    /// Logique des écrans de configuration, indépendante de l'affichage : l'interface appelle ces méthodes puis
    /// relit l'état. Le joueur règle sur l'écran principal le mode, le nombre de joueurs et de manches ; tout le
    /// reste (profil, adversaires, pouvoirs, options) est conservé dans les préférences. Les adversaires forment
    /// une liste fixe de <see cref="RosterSize"/> IA : seuls les premiers jouent, selon le nombre de joueurs.
    /// </summary>
    public sealed class MatchSetupModel
    {
        /// <summary>Nombre d'IA configurables : 10 joueurs au maximum, moins le joueur humain.</summary>
        public const int RosterSize = 9;

        private readonly RulesConfig _rules = new RulesConfig();
        private readonly List<AiSetup> _roster = new List<AiSetup>();
        private int _playerCount;
        private int _roundCount;

        public MatchSetupModel(string defaultHumanName = "Joueur", GameMode mode = GameMode.Classic,
            int playerCount = 3, int roundCount = 3)
        {
            HumanName = defaultHumanName ?? string.Empty;
            _rules.Mode = mode;

            var names = MatchSettings.SuggestedAiNames;
            for (int i = 0; i < RosterSize; i++)
                _roster.Add(new AiSetup(names[i % names.Length], AiDifficulty.Normal));

            _playerCount = ClampPlayers(playerCount);
            _roundCount = ClampRounds(roundCount);
        }

        /// <summary>Code de langue mémorisé avec les préférences (vide tant que rien n'est choisi).</summary>
        public string Language { get; set; } = string.Empty;

        // ---------------------------------------------------------------- Son

        public const float DefaultMusicVolume = 0.5f;
        public const float DefaultEffectsVolume = 0.8f;

        public float MusicVolume { get; private set; } = DefaultMusicVolume;
        public float EffectsVolume { get; private set; } = DefaultEffectsVolume;

        public void SetMusicVolume(float volume) => MusicVolume = SanitizeVolume(volume, DefaultMusicVolume);
        public void SetEffectsVolume(float volume) => EffectsVolume = SanitizeVolume(volume, DefaultEffectsVolume);

        /// <summary>Ramène un volume entre 0 et 1 ; une valeur absurde (non numérique) donne la valeur par défaut.</summary>
        public static float SanitizeVolume(float volume, float fallback)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return fallback;
            return Math.Max(0f, Math.Min(1f, volume));
        }

        // ---------------------------------------------------------------- Mode et nombre de joueurs

        public GameMode Mode => _rules.Mode;
        public int MinPlayers => RulesConfig.MinPlayers;
        public int MaxPlayers => RulesConfig.MaxPlayersFor(_rules.Mode);
        public int PlayerCount => _playerCount;

        /// <summary>Change de mode ; le nombre de joueurs est ramené dans les limites du nouveau mode.</summary>
        public void SetMode(GameMode mode)
        {
            _rules.Mode = mode;
            _playerCount = ClampPlayers(_playerCount);
        }

        public void SetPlayerCount(int count) => _playerCount = ClampPlayers(count);
        public void AdjustPlayerCount(int delta) => SetPlayerCount(_playerCount + delta);

        private int ClampPlayers(int count) => Math.Max(MinPlayers, Math.Min(MaxPlayers, count));

        // ---------------------------------------------------------------- Profil et adversaires

        public string HumanName { get; private set; }

        public void SetHumanName(string name) => HumanName = name ?? string.Empty;

        /// <summary>Les 9 IA configurables, utilisées ou non.</summary>
        public IReadOnlyList<AiSetup> Roster => _roster;

        /// <summary>Les IA qui jouent réellement, selon le nombre de joueurs.</summary>
        public IReadOnlyList<AiSetup> Opponents => _roster.GetRange(0, _playerCount - 1);

        public bool IsRosterEntryUsed(int index) => index >= 0 && index < _playerCount - 1;

        public void SetRosterName(int index, string name) => _roster[index].Name = name ?? string.Empty;

        public void SetRosterDifficulty(int index, AiDifficulty difficulty) => _roster[index].Difficulty = difficulty;

        // ---------------------------------------------------------------- Manches

        public int RoundCount => _roundCount;

        public void SetRoundCount(int rounds) => _roundCount = ClampRounds(rounds);
        public void AdjustRoundCount(int delta) => SetRoundCount(_roundCount + delta);

        private static int ClampRounds(int rounds) =>
            Math.Max(MatchSettings.MinRounds, Math.Min(MatchSettings.MaxRounds, rounds));

        // ---------------------------------------------------------------- Pouvoirs

        public IReadOnlyList<PowerSlot> ActivePowers => _rules.Powers;
        public int ActivePowerCount => _rules.Powers.Count;

        /// <summary>Nombre de pouvoirs disponibles au catalogue (le maximum activable en même temps).</summary>
        public int AvailablePowerCount => PowerCatalog.All.Length;

        public bool IsPowerActive(PowerType power) => _rules.HasPower(power);

        public int RankOf(PowerType power)
        {
            foreach (var slot in _rules.Powers)
                if (slot.Power == power) return slot.Rank;
            return PowerCatalog.DefaultRank(power);
        }

        /// <summary>
        /// Active ou désactive un pouvoir (sans limite de nombre). À l'activation, il prend sa valeur
        /// traditionnelle, ou la plus proche libre.
        /// </summary>
        public void TogglePower(PowerType power)
        {
            var powers = _rules.Powers;
            int existing = powers.FindIndex(s => s.Power == power);
            if (existing >= 0)
                powers.RemoveAt(existing);
            else
                powers.Add(new PowerSlot(power, NearestFreeRank(PowerCatalog.DefaultRank(power))));
        }

        /// <summary>Déplace la valeur du pouvoir vers la valeur libre suivante (+1) ou précédente (-1), en bouclant.</summary>
        public bool StepPowerRank(PowerType power, int direction)
        {
            var powers = _rules.Powers;
            int index = powers.FindIndex(s => s.Power == power);
            if (index < 0 || direction == 0) return false;

            int step = direction > 0 ? 1 : -1;
            int span = Card.Ace - 1; // valeurs 2 à 14
            int rank = powers[index].Rank;
            for (int i = 0; i < span; i++)
            {
                rank = ((rank - 2 + step + span) % span) + 2;
                if (!powers.Any(s => s.Power != power && s.Rank == rank))
                {
                    powers[index] = new PowerSlot(power, rank);
                    return true;
                }
            }
            return false;
        }

        private int NearestFreeRank(int wanted)
        {
            var taken = new HashSet<int>(_rules.Powers.Select(s => s.Rank));
            for (int distance = 0; distance <= Card.Ace; distance++)
            {
                int up = wanted + distance;
                int down = wanted - distance;
                if (up <= Card.Ace && !taken.Contains(up)) return up;
                if (down >= 2 && !taken.Contains(down)) return down;
            }
            return wanted;
        }

        // ---------------------------------------------------------------- Options

        public bool FourOfAKindBurns
        {
            get => _rules.FourOfAKindBurns;
            set => _rules.FourOfAKindBurns = value;
        }

        public bool JumpIn
        {
            get => _rules.JumpIn;
            set => _rules.JumpIn = value;
        }

        public bool AllowInitialSwap
        {
            get => _rules.AllowInitialSwap;
            set => _rules.AllowInitialSwap = value;
        }

        // ---------------------------------------------------------------- Validation et lancement

        /// <summary>Réglages de la prochaine partie : copie indépendante du modèle.</summary>
        public MatchSettings BuildSettings()
        {
            var settings = new MatchSettings
            {
                Rules = _rules.Clone(),
                RoundCount = _roundCount,
                HumanName = HumanName
            };
            foreach (var ai in Opponents) settings.Opponents.Add(new AiSetup(ai.Name, ai.Difficulty));
            return settings;
        }

        /// <summary>
        /// Problèmes à corriger. Les noms sont contrôlés pour toute la liste d'IA, même celles qui ne jouent pas
        /// encore, afin qu'ajouter un joueur ne fasse jamais apparaître de doublon par surprise.
        /// </summary>
        public List<LocalizedMessage> Validate()
        {
            var errors = BuildSettings().Validate()
                .Where(e => !e.Key.StartsWith("validation.name_", StringComparison.Ordinal)).ToList();

            var names = new List<string> { HumanName };
            names.AddRange(_roster.Select(r => r.Name));
            errors.AddRange(MatchSettings.ValidateNames(names));
            return errors;
        }

        public bool IsValid => Validate().Count == 0;

        /// <summary>Crée la partie ; lève une exception si les réglages sont invalides.</summary>
        public Match CreateMatch(int seed, StatsBook stats = null)
        {
            var errors = Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            return new Match(BuildSettings(), seed, stats);
        }

        // ---------------------------------------------------------------- Préférences

        public SetupPreferences ToPreferences()
        {
            var prefs = new SetupPreferences
            {
                Language = Language ?? string.Empty,
                HumanName = HumanName,
                Mode = (int)Mode,
                PlayerCount = _playerCount,
                RoundCount = _roundCount,
                HasPowers = true,
                FourOfAKindBurns = FourOfAKindBurns,
                JumpIn = JumpIn,
                AllowInitialSwap = AllowInitialSwap,
                MusicVolume = MusicVolume,
                EffectsVolume = EffectsVolume
            };
            foreach (var ai in _roster)
                prefs.Roster.Add(new AiPreference { Name = ai.Name, Difficulty = (int)ai.Difficulty });
            foreach (var slot in _rules.Powers)
                prefs.Powers.Add(new PowerPreference { Power = (int)slot.Power, Rank = slot.Rank });
            return prefs;
        }

        /// <summary>
        /// Reconstruit le modèle depuis des préférences enregistrées. Tolérant : tout ce qui est absent ou
        /// invalide (fichier ancien, modifié à la main) retombe sur la valeur par défaut.
        /// </summary>
        public static MatchSetupModel FromPreferences(SetupPreferences prefs, string defaultHumanName = "Joueur")
        {
            var model = new MatchSetupModel(defaultHumanName);
            if (prefs == null) return model;

            model.Language = prefs.Language ?? string.Empty;

            string human = Clean(prefs.HumanName);
            if (human.Length > 0) model.HumanName = human;

            model._rules.Mode = Enum.IsDefined(typeof(GameMode), prefs.Mode) ? (GameMode)prefs.Mode : GameMode.Classic;
            model._playerCount = model.ClampPlayers(prefs.PlayerCount);
            model._roundCount = ClampRounds(prefs.RoundCount);

            if (prefs.Roster != null)
            {
                for (int i = 0; i < RosterSize && i < prefs.Roster.Count; i++)
                {
                    var saved = prefs.Roster[i];
                    if (saved == null) continue;

                    string name = Clean(saved.Name);
                    if (name.Length > 0) model._roster[i].Name = name;
                    if (Enum.IsDefined(typeof(AiDifficulty), saved.Difficulty))
                        model._roster[i].Difficulty = (AiDifficulty)saved.Difficulty;
                }
            }

            model._rules.FourOfAKindBurns = prefs.FourOfAKindBurns;
            model._rules.JumpIn = prefs.JumpIn;
            model._rules.AllowInitialSwap = prefs.AllowInitialSwap;
            model.SetMusicVolume(prefs.MusicVolume);
            model.SetEffectsVolume(prefs.EffectsVolume);

            if (prefs.HasPowers)
            {
                model._rules.Powers.Clear();
                var ranks = new HashSet<int>();
                foreach (var saved in prefs.Powers ?? new List<PowerPreference>())
                {
                    if (saved == null) continue;
                    if (!Enum.IsDefined(typeof(PowerType), saved.Power)) continue;
                    var power = (PowerType)saved.Power;
                    if (saved.Rank < 2 || saved.Rank > Card.Ace) continue;
                    if (model._rules.HasPower(power) || !ranks.Add(saved.Rank)) continue;
                    model._rules.Powers.Add(new PowerSlot(power, saved.Rank));
                }
            }

            return model;
        }

        private static string Clean(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            return trimmed.Length > MatchSettings.MaxNameLength
                ? trimmed.Substring(0, MatchSettings.MaxNameLength).TrimEnd()
                : trimmed;
        }
    }
}

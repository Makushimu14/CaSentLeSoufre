using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    /// <summary>
    /// Préférences du joueur à conserver d'une session à l'autre (nom, adversaires, règles, langue...).
    /// Champs publics et types simples pour être sérialisables en JSON par la couche Unity.
    /// Les énumérations sont enregistrées en nombres : ne jamais réordonner leurs valeurs existantes.
    /// </summary>
    [Serializable]
    public sealed class SetupPreferences
    {
        public int Version = 1;

        /// <summary>Code de langue (« fr », « en »), vide tant que le joueur n'a rien choisi.</summary>
        public string Language = string.Empty;

        public string HumanName = string.Empty;
        public int Mode;
        public int PlayerCount = 3;
        public int RoundCount = 3;

        /// <summary>Les 9 adversaires IA, dans l'ordre de la table.</summary>
        public List<AiPreference> Roster = new List<AiPreference>();

        /// <summary>Vrai quand la liste des pouvoirs a été enregistrée (une liste vide est alors un choix, pas un oubli).</summary>
        public bool HasPowers;

        public List<PowerPreference> Powers = new List<PowerPreference>();
        public bool FourOfAKindBurns = true;
        public bool JumpIn;
        public bool AllowInitialSwap = true;

        /// <summary>Volume de la musique d'ambiance (0 à 1).</summary>
        public float MusicVolume = 0.5f;

        /// <summary>Volume des effets sonores (0 à 1).</summary>
        public float EffectsVolume = 0.8f;
    }

    [Serializable]
    public sealed class AiPreference
    {
        public string Name = string.Empty;
        public int Difficulty = (int)AiDifficulty.Normal;
    }

    [Serializable]
    public sealed class PowerPreference
    {
        public int Power;
        public int Rank;
    }
}

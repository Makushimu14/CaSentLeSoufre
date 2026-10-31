using CaSentLeSoufre.Core;

namespace CaSentLeSoufre.App
{
    /// <summary>Données qui passent du menu à la table de jeu.</summary>
    public static class GameSession
    {
        /// <summary>Réglages choisis dans le menu pour la prochaine partie (copie indépendante du menu).</summary>
        public static MatchSettings Pending { get; private set; }

        public static void SetPending(MatchSettings settings) => Pending = settings?.Clone();

        public static void Clear() => Pending = null;
    }
}

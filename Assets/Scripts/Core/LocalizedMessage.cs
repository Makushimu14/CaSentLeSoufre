namespace CaSentLeSoufre.Core
{
    /// <summary>Paramètre de message qui est lui-même une clé de traduction (à traduire avant le formatage).</summary>
    public readonly struct KeyRef
    {
        public string Key { get; }

        public KeyRef(string key)
        {
            Key = key;
        }

        public override string ToString() => Key;
    }

    /// <summary>
    /// Message destiné au joueur, exprimé par une clé de traduction et ses paramètres ({0}, {1}...).
    /// Le moteur ne contient aucun texte : l'interface traduit la clé via les tables de Localization.
    /// </summary>
    public readonly struct LocalizedMessage
    {
        public string Key { get; }
        public object[] Args { get; }

        public LocalizedMessage(string key, params object[] args)
        {
            Key = key;
            Args = args ?? new object[0];
        }

        /// <summary>Forme technique (clé + paramètres), utile pour les journaux et les tests.</summary>
        public override string ToString() =>
            Args.Length == 0 ? Key : Key + "(" + string.Join(", ", Args) + ")";
    }
}

using CaSentLeSoufre.Core;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace CaSentLeSoufre.App
{
    /// <summary>
    /// Traduction des clés du moteur via la table « Game » du package Localization.
    /// Les paramètres de type <see cref="KeyRef"/> sont traduits avant le formatage du message.
    /// </summary>
    public static class Loc
    {
        public const string TableName = "Game";
        public const string French = "fr";
        public const string English = "en";

        public static string Get(string key, params object[] args)
        {
            var localized = new LocalizedString(TableName, key);
            return localized.GetLocalizedString(Resolve(args));
        }

        public static string Get(LocalizedMessage message) => Get(message.Key, message.Args);

        public static string Get(KeyRef key) => Get(key.Key);

        /// <summary>Change la langue du jeu (« fr » ou « en »). Retourne false si la langue n'existe pas.</summary>
        public static bool SetLanguage(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale == null) return false;
            LocalizationSettings.SelectedLocale = locale;
            return true;
        }

        private static object[] Resolve(object[] args)
        {
            if (args == null || args.Length == 0) return new object[0];

            var resolved = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
                resolved[i] = args[i] is KeyRef keyRef ? Get(keyRef.Key) : args[i];
            return resolved;
        }
    }
}

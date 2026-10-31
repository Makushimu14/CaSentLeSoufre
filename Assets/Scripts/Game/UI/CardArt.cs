using System.Collections.Generic;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// Images des cartes, dans Assets/Resources/Cards (générées par Tools/CardArt) : « hearts_7 », « spades_14 »...,
    /// et « back ». Chaque appel renvoie null si l'image est absente : <see cref="CardFactory"/> dessine alors
    /// la carte par code, le jeu reste donc jouable sans les images.
    /// Les images ne contiennent pas la valeur de la carte, qui est un texte localisé posé par-dessus.
    /// </summary>
    public static class CardArt
    {
        private const string Folder = "Cards/";

        private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        public static Texture2D Face(Card card) => Load(Folder + card.Suit.ToString().ToLowerInvariant() + "_" + card.Rank);

        public static Texture2D Back() => Load(Folder + "back");

        private static Texture2D Load(string path)
        {
            if (Missing.Contains(path)) return null;

            // Une texture détruite par Unity (changement de mode de jeu) se compare à null : on la recharge.
            if (Loaded.TryGetValue(path, out var cached) && cached != null) return cached;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Missing.Add(path);
                return null;
            }

            Loaded[path] = texture;
            return texture;
        }
    }
}

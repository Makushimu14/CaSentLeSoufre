using System;
using CaSentLeSoufre.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>Comment une carte est présentée et ce qu'on peut faire avec.</summary>
    public struct CardState
    {
        public bool Interactable;

        /// <summary>Carte sélectionnée : surlignée et légèrement relevée.</summary>
        public bool Selected;

        /// <summary>Carte jouable à la volée : cadre lumineux.</summary>
        public bool Highlight;

        /// <summary>Carte non jouable en ce moment : assombrie.</summary>
        public bool Dimmed;

        /// <summary>Carte visée par le curseur du clavier : cadre blanc épais.</summary>
        public bool Focused;
    }

    /// <summary>
    /// Dessine les cartes par code (rectangle crème, valeur et couleur), en attendant de vraies illustrations.
    /// Toute l'apparence est ici : remplacer ce fichier suffira à changer le visuel des cartes.
    /// </summary>
    public static class CardFactory
    {
        private static readonly Color Cream = new Color(0.95f, 0.91f, 0.85f);
        private static readonly Color RedInk = new Color(0.70f, 0.15f, 0.12f);
        private static readonly Color BlackInk = new Color(0.10f, 0.08f, 0.08f);
        private static readonly Color BackColor = new Color(0.35f, 0.10f, 0.08f);
        private static readonly Color BackInner = new Color(0.55f, 0.17f, 0.12f);

        /// <summary>Hauteur dont une carte sélectionnée se relève.</summary>
        public const float Raise = 20f;

        public static string RankText(int rank) =>
            rank <= 10 ? rank.ToString() : Loc.Get("card.rank." + rank);

        /// <summary>Texte court d'une carte, par exemple « 7♥ » ou « V♠ » selon la langue.</summary>
        public static string Label(Card card) => RankText(card.Rank) + card.SuitSymbol;

        private static bool IsRed(Suit suit) => suit == Suit.Hearts || suit == Suit.Diamonds;

        // ---------------------------------------------------------------- Cartes

        /// <summary>Carte face visible dans une mise en page ; l'emplacement réserve la place du relèvement.</summary>
        public static Button Face(Transform parent, Card card, Vector2 size, Action onClick, CardState state) =>
            FaceAt(NewSlot(parent, size), card, size, Vector2.zero, onClick, state);

        /// <summary>Carte face visible posée à une position précise d'un conteneur (pour empiler des cartes).</summary>
        public static Button FaceAt(RectTransform container, Card card, Vector2 size, Vector2 position,
            Action onClick, CardState state)
        {
            var art = CardArt.Face(card);
            var button = NewCard(container, size, position, Cream, art, onClick, state);
            var rect = (RectTransform)button.transform;
            var ink = IsRed(card.Suit) ? RedInk : BlackInk;

            if (art != null)
            {
                // L'image porte le fond, les symboles et les figures ; la valeur, elle, est un texte localisé
                // (V, D, R en français, J, Q, K en anglais), posé dans les deux coins à l'endroit laissé libre.
                int fontSize = Mathf.Max(10, Mathf.RoundToInt(size.y * 0.155f));
                Overlay(rect, RankText(card.Rank), fontSize, ink, FontStyle.Bold, TextAnchor.UpperLeft,
                    size.x * 0.045f, size.y * 0.03f, 0, 0);
                Overlay(rect, RankText(card.Rank), fontSize, ink, FontStyle.Bold, TextAnchor.LowerRight,
                    0, 0, size.x * 0.045f, size.y * 0.03f);
                return button;
            }

            // Repli sans image : la carte est dessinée par code.
            if (size.y >= 100)
            {
                Overlay(rect, RankText(card.Rank), Mathf.RoundToInt(size.y * 0.24f), ink, FontStyle.Bold,
                    TextAnchor.UpperLeft, size.x * 0.08f, size.y * 0.04f, 0, 0);
                Overlay(rect, card.SuitSymbol, Mathf.RoundToInt(size.y * 0.46f), ink, FontStyle.Normal,
                    TextAnchor.MiddleCenter, 0, size.y * 0.18f, 0, 0);
            }
            else
            {
                Overlay(rect, Label(card), Mathf.RoundToInt(size.y * 0.30f), ink, FontStyle.Bold,
                    TextAnchor.MiddleCenter, 0, 0, 0, 0);
            }
            return button;
        }

        /// <summary>Texte posé sur toute la surface d'une carte, avec des marges.</summary>
        private static void Overlay(RectTransform card, string text, int fontSize, Color color, FontStyle style,
            TextAnchor anchor, float left, float top, float right, float bottom)
        {
            var label = UiFactory.Label(card, text, fontSize, color, style, anchor);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch((RectTransform)label.transform, left, top, right, bottom);
        }

        /// <summary>Carte face cachée dans une mise en page.</summary>
        public static Button Back(Transform parent, Vector2 size, Action onClick, CardState state) =>
            BackAt(NewSlot(parent, size), size, Vector2.zero, onClick, state);

        /// <summary>Carte face cachée posée à une position précise d'un conteneur.</summary>
        public static Button BackAt(RectTransform container, Vector2 size, Vector2 position, Action onClick,
            CardState state)
        {
            var art = CardArt.Back();
            var button = NewCard(container, size, position, BackColor, art, onClick, state);
            if (art != null) return button;

            // Repli sans image : un motif simple dessiné par code.
            var rect = (RectTransform)button.transform;
            var inner = UiFactory.NewRect("Pattern", rect);
            UiFactory.Stretch(inner, size.x * 0.12f, size.y * 0.09f, size.x * 0.12f, size.y * 0.09f);
            var image = inner.gameObject.AddComponent<Image>();
            image.color = BackInner;
            image.raycastTarget = false;
            return button;
        }

        /// <summary>Emplacement vide (défausse sans carte).</summary>
        public static RectTransform EmptySlot(Transform parent, Vector2 size)
        {
            var slot = UiFactory.NewRect("EmptySlot", parent);
            UiFactory.Size(slot, minWidth: size.x, preferredWidth: size.x, minHeight: size.y + Raise,
                preferredHeight: size.y + Raise, flexibleWidth: 0);

            var inner = UiFactory.NewRect("Outline", slot);
            inner.anchorMin = inner.anchorMax = Vector2.zero;
            inner.pivot = Vector2.zero;
            inner.sizeDelta = size;
            var image = inner.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.07f);
            image.raycastTarget = false;
            return slot;
        }

        // ---------------------------------------------------------------- Fabrication commune

        /// <summary>Emplacement d'une carte dans une mise en page : réserve sa taille plus la place du relèvement.</summary>
        private static RectTransform NewSlot(Transform parent, Vector2 size)
        {
            var slot = UiFactory.NewRect("CardSlot", parent);
            UiFactory.Size(slot, minWidth: size.x, preferredWidth: size.x, minHeight: size.y + Raise,
                preferredHeight: size.y + Raise, flexibleWidth: 0);
            return slot;
        }

        /// <summary>
        /// Crée la carte. Avec une image, c'est un <see cref="RawImage"/> (texture quelle que soit la façon dont
        /// Unity l'a importée) ; sans image, un aplat de couleur.
        /// </summary>
        private static Button NewCard(RectTransform container, Vector2 size, Vector2 position, Color background,
            Texture2D art, Action onClick, CardState state)
        {
            var card = UiFactory.NewRect("Card", container);
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.pivot = Vector2.zero;
            card.sizeDelta = size;
            card.anchoredPosition = position + new Vector2(0, state.Selected ? Raise : 0);

            Graphic graphic;
            if (art != null)
            {
                var raw = card.gameObject.AddComponent<RawImage>();
                raw.texture = art;
                raw.color = Color.white;
                graphic = raw;
            }
            else
            {
                var image = card.gameObject.AddComponent<Image>();
                image.color = background;
                graphic = image;
            }

            if (state.Selected || state.Highlight || state.Focused)
            {
                var outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = state.Selected ? Theme.Accent
                    : state.Highlight ? new Color(1f, 0.82f, 0.25f) : Color.white;
                float distance = state.Focused ? 6f : 4f;
                outline.effectDistance = new Vector2(distance, -distance);
            }

            if (state.Dimmed) card.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.interactable = state.Interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; // l'assombrissement passe par le groupe, pas par le bouton
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(() =>
                {
                    AudioManager.Play(Sound.Select);
                    onClick();
                });
            return button;
        }
    }
}

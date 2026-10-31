using System;
using UnityEngine;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>Couleurs de l'interface : noir braise, rouge soufre.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("120C0C");
        public static readonly Color Felt = Hex("12201A");
        public static readonly Color Panel = Hex("201515");
        public static readonly Color PanelLight = Hex("2E1F1F");
        public static readonly Color Accent = Hex("C8452B");
        public static readonly Color Text = Hex("EDE3D8");
        public static readonly Color Muted = Hex("A09286");
        public static readonly Color Error = Hex("EE6B57");
        public static readonly Color Disabled = Hex("3A2E2E");
        public static readonly Color PanelCurrent = Hex("3A1812");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }

    /// <summary>Fabrique d'éléments uGUI construits par code (aucune ressource ni scène à préparer).</summary>
    public static class UiFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Contraintes de mise en page d'un élément dans un groupe horizontal ou vertical (0 ou moins = non fixé).</summary>
        public static LayoutElement Size(Component target, float minWidth = -1, float preferredWidth = -1,
            float minHeight = -1, float preferredHeight = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var layout = target.GetComponent<LayoutElement>();
            if (layout == null) layout = target.gameObject.AddComponent<LayoutElement>();
            if (minWidth >= 0) layout.minWidth = minWidth;
            if (preferredWidth >= 0) layout.preferredWidth = preferredWidth;
            if (minHeight >= 0) layout.minHeight = minHeight;
            if (preferredHeight >= 0) layout.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0) layout.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) layout.flexibleHeight = flexibleHeight;
            return layout;
        }

        // ---------------------------------------------------------------- Conteneurs

        public static RectTransform Row(Transform parent, float spacing = 12, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = NewRect("Row", parent);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform Column(Transform parent, float spacing = 10, int padding = 0,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var rect = NewRect("Column", parent);
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>Colonne sur fond de carte, pour une section de l'écran.</summary>
        public static RectTransform Card(Transform parent, Color? background = null, int padding = 26, float spacing = 14)
        {
            var rect = Column(parent, spacing, padding);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background ?? Theme.Panel;
            image.raycastTarget = false;
            return rect;
        }

        public static RectTransform FlexibleSpace(Transform parent)
        {
            var rect = NewRect("Space", parent);
            Size(rect, flexibleWidth: 1);
            return rect;
        }

        public static RectTransform FixedSpace(Transform parent, float width = 0, float height = 0)
        {
            var rect = NewRect("Space", parent);
            Size(rect, minWidth: width, preferredWidth: width, minHeight: height, preferredHeight: height,
                flexibleWidth: 0);
            return rect;
        }

        // ---------------------------------------------------------------- Texte

        public static Text Label(Transform parent, string text, int size, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var rect = NewRect("Label", parent);
            return ApplyText(rect.gameObject.AddComponent<Text>(), text, size, color, style, anchor);
        }

        private static Text ApplyText(Text text, string value, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // ---------------------------------------------------------------- Boutons

        public static Button MakeButton(Transform parent, string label, Action onClick, int fontSize = 26,
            float width = 0, float height = 56, Color? background = null, Color? foreground = null,
            bool interactable = true)
        {
            var rect = NewRect("Button", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background ?? Theme.PanelLight;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var textRect = NewRect("Text", rect);
            Stretch(textRect, 10, 4, 10, 4);
            ApplyText(textRect.gameObject.AddComponent<Text>(), label, fontSize, foreground ?? Theme.Text,
                FontStyle.Bold, TextAnchor.MiddleCenter).horizontalOverflow = HorizontalWrapMode.Overflow;

            Size(rect, minWidth: width, preferredWidth: width, minHeight: height, preferredHeight: height);
            return button;
        }

        /// <summary>Rangée de boutons exclusifs (le bouton choisi est en rouge). Chaque bouton partage la largeur.</summary>
        public static RectTransform Segmented(Transform parent, string[] labels, int selected, Action<int> onSelect,
            int fontSize = 24, float height = 56, float spacing = 6)
        {
            var row = Row(parent, spacing);
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = MakeButton(row, labels[i], () => onSelect(index), fontSize, 0, height,
                    i == selected ? Theme.Accent : Theme.PanelLight);
                Size(button, minWidth: 110, flexibleWidth: 1);
            }
            return row;
        }

        // ---------------------------------------------------------------- Champ de saisie

        public static InputField MakeInput(Transform parent, string value, string placeholder, int characterLimit,
            Action<string> onChanged, float height = 56)
        {
            var rect = NewRect("Input", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Theme.PanelLight;
            var input = rect.gameObject.AddComponent<InputField>();

            var textRect = NewRect("Text", rect);
            Stretch(textRect, 16, 6, 16, 6);
            var text = ApplyText(textRect.gameObject.AddComponent<Text>(), string.Empty, 26, Theme.Text,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var placeholderRect = NewRect("Placeholder", rect);
            Stretch(placeholderRect, 16, 6, 16, 6);
            var hint = ApplyText(placeholderRect.gameObject.AddComponent<Text>(), placeholder, 26, Theme.Muted,
                FontStyle.Italic, TextAnchor.MiddleLeft);
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;

            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;
            input.text = value ?? string.Empty;
            if (onChanged != null) input.onValueChanged.AddListener(v => onChanged(v));

            Size(rect, minWidth: 220, preferredWidth: 320, minHeight: height, preferredHeight: height, flexibleWidth: 1);
            return input;
        }
    }
}

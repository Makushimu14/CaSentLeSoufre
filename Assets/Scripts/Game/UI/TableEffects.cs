using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// Effets visuels éphémères par-dessus la table : bandeau de texte qui monte et s'efface, onde colorée qui
    /// s'élargit, anneau de points qui tourne. Aucune ressource : les formes sont générées par code.
    /// Les positions sont dans le repère de la couche d'animation (voir <see cref="CardFlights.ToLayer"/>).
    /// </summary>
    public sealed class TableEffects
    {
        private sealed class Effect
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public float Elapsed;
            public float Duration;
            public Action<Effect, float> Pose;
            public bool Started;
        }

        private static Texture2D _ring;
        private static Texture2D _dot;

        private readonly RectTransform _layer;
        private readonly List<Effect> _effects = new List<Effect>();

        public TableEffects(RectTransform layer)
        {
            _layer = layer;
        }

        public int Active => _effects.Count;

        // ---------------------------------------------------------------- Effets

        /// <summary>Texte qui apparaît en grossissant, monte un peu et s'efface.</summary>
        public void Banner(string text, Vector3 at, Color color, float delay = 0f)
        {
            var root = NewRoot("Banner", new Vector2(700, 70));
            var label = UiFactory.Label(root, text, 44, color, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch((RectTransform)label.transform);

            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            Add(root, delay, 1.15f, (effect, t) =>
            {
                float rise = 1f - (1f - t) * (1f - t);
                effect.Root.localPosition = at + new Vector3(0, 70f * rise, 0);
                float pop = Mathf.Clamp01(t / 0.2f);
                float scale = 0.7f + 0.4f * (1f - (1f - pop) * (1f - pop));
                effect.Root.localScale = new Vector3(scale, scale, 1f);
                effect.Group.alpha = t < 0.15f ? t / 0.15f : t > 0.7f ? (1f - t) / 0.3f : 1f;
            });
        }

        /// <summary>Anneau coloré qui s'élargit en s'effaçant.</summary>
        public void Pulse(Vector3 at, Color color, float startSize, float endSize, float seconds = 0.6f, float delay = 0f)
        {
            var root = NewRoot("Pulse", new Vector2(startSize, startSize));
            var image = root.gameObject.AddComponent<RawImage>();
            image.texture = RingTexture();
            image.color = color;
            image.raycastTarget = false;

            Add(root, delay, seconds, (effect, t) =>
            {
                float eased = 1f - (1f - t) * (1f - t);
                float size = Mathf.Lerp(startSize, endSize, eased);
                effect.Root.sizeDelta = new Vector2(size, size);
                effect.Root.localPosition = at;
                effect.Group.alpha = 1f - t;
            });
        }

        /// <summary>Anneau de points qui fait un tour autour d'un point, dans un sens ou dans l'autre.</summary>
        public void Orbit(Vector3 at, bool clockwise, Color color, float radius = 100f, float seconds = 1.0f, float delay = 0f)
        {
            var root = NewRoot("Orbit", new Vector2(radius * 2, radius * 2));
            const int dots = 8;
            for (int i = 0; i < dots; i++)
            {
                var dot = UiFactory.NewRect("Dot", root);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                float angle = i * Mathf.PI * 2f / dots;
                dot.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float size = 14f + 3f * i; // les points grossissent : on voit le sens de rotation
                dot.sizeDelta = new Vector2(size, size);
                var image = dot.gameObject.AddComponent<RawImage>();
                image.texture = DotTexture();
                image.color = color;
                image.raycastTarget = false;
            }

            Add(root, delay, seconds, (effect, t) =>
            {
                float eased = t * t * (3f - 2f * t);
                effect.Root.localPosition = at;
                effect.Root.localRotation = Quaternion.Euler(0, 0, (clockwise ? -1f : 1f) * 360f * eased);
                float scale = 0.8f + 0.3f * t;
                effect.Root.localScale = new Vector3(scale, scale, 1f);
                effect.Group.alpha = t > 0.6f ? (1f - t) / 0.4f : 1f;
            });
        }

        public void Update(float dt)
        {
            if (_effects.Count == 0) return;

            var finished = new List<Effect>();
            foreach (var effect in _effects)
            {
                effect.Elapsed += dt;
                if (effect.Elapsed < 0f) continue;

                if (!effect.Started)
                {
                    effect.Started = true;
                    effect.Root.gameObject.SetActive(true);
                }

                float t = Mathf.Clamp01(effect.Elapsed / Mathf.Max(0.01f, effect.Duration));
                effect.Pose(effect, t);
                if (t >= 1f) finished.Add(effect);
            }

            foreach (var effect in finished)
            {
                _effects.Remove(effect);
                UnityEngine.Object.Destroy(effect.Root.gameObject);
            }
        }

        // ---------------------------------------------------------------- Mise en place

        private RectTransform NewRoot(string name, Vector2 size)
        {
            var root = UiFactory.NewRect(name, _layer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;
            return root;
        }

        private void Add(RectTransform root, float delay, float duration, Action<Effect, float> pose)
        {
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var effect = new Effect { Root = root, Group = group, Elapsed = -delay, Duration = duration, Pose = pose };
            root.gameObject.SetActive(false); // invisible jusqu'au départ
            _effects.Add(effect);
        }

        // ---------------------------------------------------------------- Textures générées

        private static Texture2D RingTexture()
        {
            if (_ring == null) _ring = Generate(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.08f));
            return _ring;
        }

        private static Texture2D DotTexture()
        {
            if (_dot == null) _dot = Generate(64, d => 1f - Mathf.SmoothStep(0.65f, 1f, d));
            return _dot;
        }

        /// <summary>Image carrée blanche dont l'opacité dépend de la distance au centre (0 au centre, 1 au bord).</summary>
        private static Texture2D Generate(int size, Func<float, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = distance > 1f ? (byte)0 : (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alphaAt(distance)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

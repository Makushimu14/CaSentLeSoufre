using System;
using System.Collections.Generic;
using CaSentLeSoufre.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// Cartes qui volent d'un endroit de la table à un autre (par-dessus l'écran, sans toucher à la mise en page),
    /// avec retournement en vol et effet de brûlure. Chaque vol appelle <see cref="Spec.OnLand"/> à l'arrivée.
    /// </summary>
    public sealed class CardFlights
    {
        /// <summary>Description d'un vol. Les positions sont dans le repère de la couche d'animation.</summary>
        public struct Spec
        {
            /// <summary>Carte à montrer ; null = dos de carte.</summary>
            public Card? Face;

            /// <summary>Part du dos et se retourne à mi-chemin pour montrer <see cref="Face"/>.</summary>
            public bool FlipToFace;

            public Vector2 Size;
            public Vector3 From;
            public Vector3 To;
            public float Delay;
            public float Duration;
            public float StartScale;
            public float EndScale;

            /// <summary>Hauteur de l'arc de la trajectoire (négatif : arc vers le bas).</summary>
            public float Arc;

            public Action OnLand;
        }

        private sealed class Flight
        {
            public Spec Spec;
            public RectTransform Root;
            public GameObject Back;
            public GameObject Face;
            public Image Flame;
            public CanvasGroup Group;
            public bool IsBurn;
            public bool Started;
            public float Elapsed;
        }

        private readonly RectTransform _layer;
        private readonly List<Flight> _flights = new List<Flight>();

        public CardFlights(RectTransform layer)
        {
            _layer = layer;
        }

        /// <summary>Nombre de vols en cours ou en attente de départ.</summary>
        public int Active => _flights.Count;

        /// <summary>Centre d'un élément de l'interface, dans le repère de la couche d'animation.</summary>
        public Vector3 ToLayer(RectTransform rect) => _layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));

        public void Add(Spec spec)
        {
            var root = UiFactory.NewRect("Flight", _layer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = spec.Size;

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var flight = new Flight { Spec = spec, Root = root, Group = group, Elapsed = -spec.Delay };

            // Avec un retournement, on prépare le dos et la face ; sinon seulement ce qui doit se voir.
            bool wantsBack = !spec.Face.HasValue || spec.FlipToFace;
            bool wantsFace = spec.Face.HasValue;
            if (wantsBack) flight.Back = CardFactory.BackAt(root, spec.Size, Vector2.zero, null, new CardState()).gameObject;
            if (wantsFace)
                flight.Face = CardFactory.FaceAt(root, spec.Face.Value, spec.Size, Vector2.zero, null, new CardState()).gameObject;
            if (flight.Back != null && flight.Face != null) flight.Face.SetActive(false);

            root.gameObject.SetActive(false); // invisible jusqu'au départ
            root.localPosition = spec.From;
            _flights.Add(flight);
        }

        /// <summary>Effet de brûlure : la carte du dessus gonfle, rougeoie et disparaît.</summary>
        public void Burn(Card top, Vector3 at, Vector2 size, Action onDone)
        {
            var root = UiFactory.NewRect("Burn", _layer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var flight = new Flight
            {
                IsBurn = true,
                Root = root,
                Group = group,
                Spec = new Spec { From = at, To = at, Size = size, Duration = 0.5f, StartScale = 1f, EndScale = 1.6f, OnLand = onDone }
            };
            flight.Face = CardFactory.FaceAt(root, top, size, Vector2.zero, null, new CardState()).gameObject;

            var flame = UiFactory.NewRect("Flame", root);
            UiFactory.Stretch(flame);
            flight.Flame = flame.gameObject.AddComponent<Image>();
            flight.Flame.color = new Color(1f, 0.42f, 0.08f, 0.6f);
            flight.Flame.raycastTarget = false;

            root.localPosition = at;
            flight.Started = true;
            _flights.Add(flight);
        }

        public void Update(float dt)
        {
            if (_flights.Count == 0) return;

            var finished = new List<Flight>();
            foreach (var flight in _flights)
            {
                flight.Elapsed += dt;
                if (flight.Elapsed < 0f) continue;

                if (!flight.Started)
                {
                    flight.Started = true;
                    flight.Root.gameObject.SetActive(true);
                }

                float t = Mathf.Clamp01(flight.Elapsed / Mathf.Max(0.01f, flight.Spec.Duration));
                if (flight.IsBurn) PoseBurn(flight, t);
                else PoseFlight(flight, t);

                if (t >= 1f) finished.Add(flight);
            }

            // On retire d'abord les vols terminés : une arrivée peut en lancer d'autres (enchaînements).
            foreach (var flight in finished)
            {
                _flights.Remove(flight);
                UnityEngine.Object.Destroy(flight.Root.gameObject);
            }
            foreach (var flight in finished) flight.Spec.OnLand?.Invoke();
        }

        private static void PoseFlight(Flight flight, float t)
        {
            var spec = flight.Spec;
            float eased = t * t * (3f - 2f * t);

            var position = Vector3.Lerp(spec.From, spec.To, eased);
            position.y += Mathf.Sin(eased * Mathf.PI) * spec.Arc;
            flight.Root.localPosition = position;

            float scale = Mathf.Lerp(spec.StartScale, spec.EndScale, eased);
            float width = scale;
            if (flight.Back != null && flight.Face != null)
            {
                // Retournement : la carte s'amincit jusqu'à mi-chemin, puis se rouvre côté face.
                width = scale * Mathf.Abs(Mathf.Cos(eased * Mathf.PI));
                bool showFace = eased >= 0.5f;
                flight.Back.SetActive(!showFace);
                flight.Face.SetActive(showFace);
            }
            flight.Root.localScale = new Vector3(width, scale, 1f);
        }

        private static void PoseBurn(Flight flight, float t)
        {
            float scale = Mathf.Lerp(flight.Spec.StartScale, flight.Spec.EndScale, t);
            flight.Root.localScale = new Vector3(scale, scale, 1f);
            flight.Group.alpha = 1f - t * t;
            flight.Flame.color = new Color(1f, Mathf.Lerp(0.42f, 0.8f, t), 0.08f, 0.6f);
        }
    }
}

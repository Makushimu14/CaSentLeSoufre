using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// La table de jeu : chaque joueur occupe une bande le long des bords de l'écran (voir <see cref="TableSeats"/>),
    /// dans une zone défilante où ses cartes visibles sont posées sur ses cartes cachées. Le centre porte les piles,
    /// le journal et la main du joueur. L'écran est reconstruit à chaque événement.
    /// </summary>
    public sealed class TableController : MonoBehaviour
    {
        private static readonly Vector2 HandCard = new Vector2(104, 146);
        private static readonly Vector2 HumanZoneCard = new Vector2(92, 128);
        private static readonly Vector2 AiZoneCard = new Vector2(84, 118);
        private static readonly Vector2 PileCard = new Vector2(92, 128);

        // La carte visible couvre exactement la carte cachée (comme dans l'ancien projet) : aucun décalage.
        private const float HumanStackOffset = 0f;
        private const float AiStackOffset = 0f;
        private const float StackSpacing = 8f;
        private const float HandSpacing = 10f;

        /// <summary>Largeur minimale visible de chaque carte quand la main se chevauche.</summary>
        private const float MinVisibleSlice = 34f;

        /// <summary>Marge entre une bande et le bord de l'écran de référence.</summary>
        private const float ScreenInset = 4f;
        private const int LogLines = 5;

        private StatsBook _stats;
        private Match _match;
        private TableSession _session;
        private Action _onExit;

        private GameObject _canvasObject;
        private RectTransform _view;

        // Une partie abandonnée (ou finie) ne doit plus être enregistrée, même si le jeu se ferme ensuite.
        private bool _saveDisabled;
        private float _abandonConfirmUntil;

        // Menu pause : le temps de la partie et les animations sont gelés tant qu'il est ouvert.
        private bool _paused;
        private bool _wasHumanTurn;

        // Animations : cartes volantes par-dessus la table, et défausse affichée qui peut retarder sur la partie
        // le temps que les cartes arrivent (elle se recale toujours dès que plus rien ne vole).
        private const float FlightSeconds = 0.38f;
        private const float StaggerSeconds = 0.07f;
        private const int MaxFlyingCards = 6;

        private RectTransform _flightLayer;
        private CardFlights _flights;
        private TableEffects _effects;

        // Distribution en cours : les mains sont masquées et la partie attend que les cartes soient arrivées.
        private bool _dealing;
        private readonly List<Card> _visualDiscard = new List<Card>();
        private bool _needsRefresh;
        private int _lastDrawCount;
        private RectTransform _discardAnchor;
        private RectTransform _drawAnchor;
        private RectTransform _handArea;
        private readonly Dictionary<int, RectTransform> _seatAnchors = new Dictionary<int, RectTransform>();
        private readonly List<KeyValuePair<Card, RectTransform>> _humanCardAnchors =
            new List<KeyValuePair<Card, RectTransform>>();

        private readonly List<string> _log = new List<string>();
        private readonly List<int> _selected = new List<int>();
        private int _swapHandIndex = -1;

        // Les zones défilantes sont reconstruites à chaque rafraîchissement : on retient leur position.
        private readonly Dictionary<string, ScrollRect> _scrolls = new Dictionary<string, ScrollRect>();
        private readonly Dictionary<string, Vector2> _scrollMemory = new Dictionary<string, Vector2>();

        /// <summary>Crée la table pour la partie décrite par <paramref name="settings"/>.</summary>
        public static TableController Launch(Transform parent, MatchSettings settings, Action onExit)
        {
            var host = new GameObject("Table");
            host.transform.SetParent(parent, false);
            var controller = host.AddComponent<TableController>();
            try
            {
                controller.Begin(settings, onExit);
            }
            catch
            {
                Destroy(host);
                throw;
            }
            return controller;
        }

        /// <summary>Reprend la partie enregistrée là où elle s'était arrêtée (le journal des coups repart vide).</summary>
        public static TableController Resume(Transform parent, MatchData saved, Action onExit)
        {
            var host = new GameObject("Table");
            host.transform.SetParent(parent, false);
            var controller = host.AddComponent<TableController>();
            try
            {
                controller.BeginResume(saved, onExit);
            }
            catch
            {
                Destroy(host);
                throw;
            }
            return controller;
        }

        private void Begin(MatchSettings settings, Action onExit)
        {
            _onExit = onExit;
            _stats = StatsStorage.Load();
            _match = new Match(settings, new System.Random().Next(), _stats);
            _session = new TableSession(_match, new TableTimings(), new System.Random().Next());

            BuildCanvas();
            _session.StartRound();
            HandleEvents(_session.DrainEvents());
            StartDeal();
            Refresh();
            PlayDealAnimation();
            SaveProgress();
        }

        private void BeginResume(MatchData saved, Action onExit)
        {
            _onExit = onExit;
            _stats = StatsStorage.Load();
            _match = Match.FromData(saved, _stats);
            _session = TableSession.Resume(_match, new TableTimings(), new System.Random().Next());
            _lastDrawCount = _session.Game != null ? _session.Game.DrawPile.Count : 0;

            BuildCanvas();
            Refresh();
        }

        private void Update()
        {
            if (_session == null) return;

            // Échap ouvre ou ferme le menu pause (pas pendant l'écran des résultats, qui a ses propres boutons).
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_session.RoundOver)
                SetPaused(!_paused);

            // La confirmation d'abandon s'efface d'elle-même au bout de quelques secondes.
            if (_abandonConfirmUntil > 0f && Time.unscaledTime >= _abandonConfirmUntil)
            {
                _abandonConfirmUntil = 0f;
                Refresh();
            }

            // En pause, plus rien ne bouge : ni la partie, ni les cartes en vol, ni les effets.
            if (_paused) return;

            if (!_session.RoundOver) _session.Tick(Time.deltaTime);

            // Un petit « ping » quand la main passe au joueur (pas quand il rejoue après une brûlure).
            bool humanTurn = _session.IsHumanTurn && !_dealing;
            if (humanTurn && !_wasHumanTurn) AudioManager.Play(Sound.YourTurn, 0.7f);
            _wasHumanTurn = humanTurn;

            // Les événements sont traités avant tout rafraîchissement : les cartes volantes ont besoin de l'ancien
            // état de la table (positions des cartes, défausse affichée) pour partir du bon endroit.
            var events = _session.DrainEvents();
            if (events.Count > 0) HandleEvents(events);

            if (_flights != null) _flights.Update(Time.unscaledDeltaTime);
            if (_effects != null) _effects.Update(Time.unscaledDeltaTime);

            // Les dernières cartes distribuées sont arrivées : on montre les mains et la partie reprend.
            if (_dealing && _flights != null && _flights.Active == 0) FinishDeal();

            if (events.Count > 0 || _needsRefresh)
            {
                _needsRefresh = false;
                Refresh();
            }
            if (events.Count > 0) SaveProgress();
        }

        // ---------------------------------------------------------------- Sauvegarde et départ

        /// <summary>
        /// Enregistre la partie après chaque coup, pour qu'elle puisse être reprise même si le jeu est fermé
        /// brutalement. Une partie terminée n'est plus enregistrée.
        /// </summary>
        private void SaveProgress()
        {
            if (_match == null || _session == null || _saveDisabled) return;

            if (_match.IsOver && _session.RoundOver) GameSaveStorage.Delete();
            else GameSaveStorage.Save(_match.ToData());
        }

        private void OnApplicationQuit() => SaveProgress();

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveProgress();
        }

        /// <summary>Quitte vers le menu en gardant la partie : elle pourra être reprise.</summary>
        private void SaveAndQuit()
        {
            SaveProgress();
            _saveDisabled = true;
            AudioManager.Persist();
            Exit();
        }

        /// <summary>Enregistre la partie et ferme le jeu.</summary>
        private void SaveAndQuitGame()
        {
            SaveProgress();
            _saveDisabled = true;
            AudioManager.Persist();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            _abandonConfirmUntil = 0f;
            if (_session != null) _session.Paused = _paused || _dealing;

            // Les volumes changés dans le menu pause sont enregistrés quand on le referme.
            if (!paused) AudioManager.Persist();
            Refresh();
        }

        /// <summary>Demande une confirmation avant d'abandonner (un second clic dans les 4 secondes).</summary>
        private void OnAbandonClicked()
        {
            if (Time.unscaledTime < _abandonConfirmUntil)
            {
                Abandon();
                return;
            }

            _abandonConfirmUntil = Time.unscaledTime + 4f;
            Refresh();
        }

        private void Abandon()
        {
            _saveDisabled = true;
            GameSaveStorage.Delete();
            AudioManager.Persist();
            Exit();
        }

        // ---------------------------------------------------------------- Menu pause

        private void BuildPauseOverlay()
        {
            var overlay = UiFactory.NewRect("PauseOverlay", _view);
            UiFactory.Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.78f); // bloque aussi les clics

            var host = UiFactory.NewRect("PanelHost", overlay);
            UiFactory.Stretch(host);
            var centering = host.gameObject.AddComponent<VerticalLayoutGroup>();
            centering.childAlignment = TextAnchor.MiddleCenter;
            centering.childControlWidth = true;
            centering.childControlHeight = true;
            centering.childForceExpandWidth = false;
            centering.childForceExpandHeight = false;

            var panel = UiFactory.Card(host, Theme.Panel, 36, 14);
            UiFactory.Size(panel, preferredWidth: 760, flexibleWidth: 0);

            UiFactory.Label(panel, Loc.Get("pause.title"), 48, Theme.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.MakeButton(panel, Loc.Get("pause.resume"), () => SetPaused(false), 30, 0, 64, Theme.Accent);

            // Le son, réglable sans quitter la partie.
            AddPauseVolume(panel, "menu.audio.music", AudioManager.MusicVolume,
                v => AudioManager.SetVolumes(v, AudioManager.EffectsVolume));
            AddPauseVolume(panel, "menu.audio.effects", AudioManager.EffectsVolume,
                v => AudioManager.SetVolumes(AudioManager.MusicVolume, v));

            UiFactory.MakeButton(panel, Loc.Get("pause.to_menu"), SaveAndQuit, 26, 0, 58);
            UiFactory.MakeButton(panel, Loc.Get("pause.quit_game"), SaveAndQuitGame, 26, 0, 58);

            bool confirming = Time.unscaledTime < _abandonConfirmUntil;
            UiFactory.MakeButton(panel, Loc.Get(confirming ? "table.confirm_abandon" : "table.quit"), OnAbandonClicked,
                26, 0, 58, confirming ? Theme.Accent : Theme.PanelLight);

            UiFactory.Label(panel, Loc.Get("pause.hint"), 20, Theme.Muted, FontStyle.Italic, TextAnchor.MiddleCenter);
        }

        private static void AddPauseVolume(Transform parent, string labelKey, float value, Action<float> apply)
        {
            var row = UiFactory.Row(parent, 16);
            var label = UiFactory.Label(row, Loc.Get(labelKey), 24, Theme.Text);
            UiFactory.Size(label, minWidth: 150, preferredWidth: 150, flexibleWidth: 0);

            Text percent = null;
            UiFactory.MakeSlider(row, value, volume =>
            {
                apply(volume);
                if (percent != null) percent.text = Mathf.RoundToInt(volume * 100f) + "%";
            });

            percent = UiFactory.Label(row, Mathf.RoundToInt(value * 100f) + "%", 24, Theme.Accent, FontStyle.Bold,
                TextAnchor.MiddleRight);
            UiFactory.Size(percent, minWidth: 80, preferredWidth: 80, flexibleWidth: 0);
        }

        // ---------------------------------------------------------------- Mise en place

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("TableCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.layer = LayerMask.NameToLayer("UI");
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // « Expand » : la zone de référence 1920 x 1080 tient toujours en entier dans la fenêtre, quel que soit
            // son format ; l'espace en trop reste vide au lieu de couper le haut, le bas ou les côtés.
            scaler.referenceResolution = new Vector2(TableSeats.ScreenWidth, TableSeats.ScreenHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var root = (RectTransform)_canvasObject.transform;
            var background = UiFactory.NewRect("Felt", root);
            UiFactory.Stretch(background);
            background.gameObject.AddComponent<Image>().color = Theme.Felt;

            _view = UiFactory.NewRect("View", root);
            UiFactory.Stretch(_view);

            // Les cartes volantes vivent dans leur propre couche : la reconstruction de la table ne les détruit pas.
            _flightLayer = UiFactory.NewRect("Flights", root);
            UiFactory.Stretch(_flightLayer);
            _flights = new CardFlights(_flightLayer);
            _effects = new TableEffects(_flightLayer);
        }

        private void Exit()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
            var onExit = _onExit;
            Destroy(gameObject);
            onExit?.Invoke();
        }

        /// <summary>Rectangle ancré au centre de l'écran, positionné comme dans <see cref="SeatArea"/>.</summary>
        private RectTransform CenteredRect(string name, float x, float y, float width, float height)
        {
            var rect = UiFactory.NewRect(name, _view);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        // ---------------------------------------------------------------- Événements et journal

        private string NameOf(int playerIndex) => _session.Game.Players[playerIndex].Name;

        private static string CardsText(IEnumerable<Card> cards) => string.Join(" ", cards.Select(CardFactory.Label));

        private void AddLog(string line)
        {
            _log.Add(line);
            while (_log.Count > LogLines) _log.RemoveAt(0);
        }

        private void HandleEvents(List<TableEvent> events)
        {
            // Cartes piochées depuis le dernier lot d'événements : attribuées au dernier coup joué du lot.
            int drawNow = _session.Game.DrawPile.Count;
            bool newRound = events.Any(x => x.Kind == TableEventKind.RoundStarted);
            int drawn = newRound ? 0 : Mathf.Max(0, _lastDrawCount - drawNow);
            _lastDrawCount = drawNow;
            int lastPlay = events.FindLastIndex(x => x.Kind == TableEventKind.Played
                                                     || x.Kind == TableEventKind.JumpedIn
                                                     || x.Kind == TableEventKind.BlindPlayed);
            int position = -1;

            foreach (var e in events)
            {
                position++;
                switch (e.Kind)
                {
                    case TableEventKind.Played:
                    case TableEventKind.JumpedIn:
                        AddLog(Loc.Get(e.Kind == TableEventKind.JumpedIn ? "table.log.jumped_in" : "table.log.played",
                            NameOf(e.PlayerIndex), CardsText(e.Result.Played)));
                        LogConsequences(e);
                        break;

                    case TableEventKind.BlindPlayed:
                        string revealed = e.Result.Revealed.HasValue ? CardFactory.Label(e.Result.Revealed.Value) : "?";
                        AddLog(Loc.Get(e.Result.PickedUp ? "table.log.blind_fail" : "table.log.blind",
                            NameOf(e.PlayerIndex), revealed));
                        LogConsequences(e);
                        break;

                    case TableEventKind.PickedUp:
                        AddLog(Loc.Get("table.log.picked_up", NameOf(e.PlayerIndex), e.Result.PickedUpCount));
                        break;

                    case TableEventKind.RoundFinished:
                        PlayRoundEndSound();
                        if (_session.Game.IsStalemate) AddLog(Loc.Get("table.log.stalemate"));
                        StatsStorage.Save(_stats);
                        break;
                }

                _selected.Clear();
                _swapHandIndex = -1;

                AnimateEvent(e, position == lastPlay ? drawn : 0);
            }
        }

        private void LogConsequences(TableEvent e)
        {
            var result = e.Result;
            if (result.Burned) AddLog(Loc.Get("table.log.burned"));
            else if (result.Power.HasValue)
                AddLog(Loc.Get("table.log.power", Loc.Get(PowerCatalog.LabelKey(result.Power.Value))));
            if (result.ForcedPickUpPlayer.HasValue)
                AddLog(Loc.Get("table.log.forced_pickup", NameOf(result.ForcedPickUpPlayer.Value)));
            if (result.PlayerFinished) AddLog(Loc.Get("table.log.finished", NameOf(e.PlayerIndex)));
        }

        // ---------------------------------------------------------------- Animations

        /// <summary>
        /// La défausse telle qu'on la voit : en retard sur la partie tant que des cartes sont en vol (pour ne pas
        /// montrer une carte avant qu'elle n'arrive), identique à la partie dès que plus rien ne vole.
        /// </summary>
        private List<Card> ShownDiscard()
        {
            if (_flights == null || _flights.Active == 0)
            {
                _visualDiscard.Clear();
                _visualDiscard.AddRange(_session.Game.DiscardPile);
            }
            return _visualDiscard;
        }

        private Vector3? PointOf(RectTransform rect) => rect == null ? (Vector3?)null : _flights.ToLayer(rect);

        private Vector3? SeatPoint(int player) =>
            _seatAnchors.TryGetValue(player, out var seat) ? PointOf(seat) : null;

        /// <summary>D'où part une carte jouée : de sa place dans la main du joueur, sinon de sa bande.</summary>
        private Vector3? SourcePoint(int player, Card card, List<RectTransform> used)
        {
            if (player == TableSession.HumanIndex)
            {
                foreach (var pair in _humanCardAnchors)
                {
                    if (pair.Key != card || pair.Value == null || used.Contains(pair.Value)) continue;
                    used.Add(pair.Value);
                    return PointOf(pair.Value);
                }
            }
            return SeatPoint(player);
        }

        /// <summary>Où arrivent des cartes ramassées ou piochées : la main du joueur, ou la bande d'un adversaire.</summary>
        private Vector3? DestinationPoint(int player) =>
            player == TableSession.HumanIndex ? (PointOf(_handArea) ?? SeatPoint(player)) : SeatPoint(player);

        private void AnimateEvent(TableEvent e, int drawn)
        {
            if (_flights == null || _discardAnchor == null) return;

            var result = e.Result;
            int player = e.PlayerIndex;
            switch (e.Kind)
            {
                case TableEventKind.Played:
                case TableEventKind.JumpedIn:
                    if (e.Kind == TableEventKind.JumpedIn && _effects != null)
                    {
                        AudioManager.Play(Sound.JumpIn);
                        var seat = SeatPoint(player);
                        if (seat.HasValue)
                            _effects.Banner(Loc.Get("table.fx.jump_in"), seat.Value + new Vector3(0, 40, 0), new Color(1f, 0.82f, 0.25f));
                    }

                    // Les cartes des adversaires partent face cachée et se retournent en vol.
                    FlyToPile(player, result.Played, result, flip: player != TableSession.HumanIndex, pickUpAfter: false);
                    FlyDraws(player, drawn);
                    break;

                case TableEventKind.BlindPlayed:
                    var blind = result.Played.Count > 0 ? result.Played
                        : result.Revealed.HasValue ? new List<Card> { result.Revealed.Value }
                        : new List<Card>();
                    FlyToPile(player, blind, result, flip: true, pickUpAfter: result.PickedUp);
                    break;

                case TableEventKind.PickedUp:
                    FlyPileToPlayer(player);
                    break;
            }
        }

        /// <summary>Des cartes jouées volent vers la défausse ; à l'arrivée de la dernière, on enchaîne les conséquences.</summary>
        private void FlyToPile(int player, IReadOnlyList<Card> cards, MoveResult result, bool flip, bool pickUpAfter)
        {
            var to = PointOf(_discardAnchor);
            if (!to.HasValue || cards.Count == 0) return;

            var used = new List<RectTransform>();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var from = SourcePoint(player, card, used);
                if (!from.HasValue) continue;

                bool last = i == cards.Count - 1;
                _flights.Add(new CardFlights.Spec
                {
                    Face = card,
                    FlipToFace = flip,
                    Size = PileCard,
                    From = from.Value,
                    To = to.Value,
                    Delay = i * StaggerSeconds,
                    Duration = FlightSeconds,
                    StartScale = player == TableSession.HumanIndex ? 1f : 0.7f,
                    EndScale = 1f,
                    Arc = 26f,
                    OnStart = () => AudioManager.Play(Sound.CardSlide, 0.6f, RandomPitch()),
                    OnLand = () =>
                    {
                        AudioManager.Play(Sound.CardPlace, 0.9f, RandomPitch());
                        _visualDiscard.Add(card);
                        _needsRefresh = true;
                        if (last) AfterLanding(player, result, pickUpAfter);
                    }
                });
            }
        }

        private void AfterLanding(int player, MoveResult result, bool pickUpAfter)
        {
            if (pickUpAfter)
            {
                FlyPileToPlayer(player);
                return;
            }

            PlayPowerEffects(player, result);

            if (result.Burned) BurnPile();
            else if (result.ForcedPickUpPlayer.HasValue) FlyPileToPlayer(result.ForcedPickUpPlayer.Value);
        }

        // ---------------------------------------------------------------- Distribution

        /// <summary>Début de manche : les mains et les cartes des bandes restent masquées, le temps de la distribution.</summary>
        private void StartDeal()
        {
            _dealing = true;
            _session.Paused = true;
            _lastDrawCount = _session.Game.DrawPile.Count;
            _visualDiscard.Clear();
        }

        /// <summary>
        /// Des dos de cartes partent de la pioche vers chaque joueur, chacun son tour. Au plus quelques paquets
        /// par joueur volent, pour que la distribution reste courte même à dix joueurs.
        /// </summary>
        private void PlayDealAnimation()
        {
            var from = PointOf(_drawAnchor);
            int players = _session.Game.Players.Count;
            const int packetsPerPlayer = 3;

            if (!from.HasValue || _flights == null)
            {
                FinishDeal();
                return;
            }

            float stagger = Mathf.Min(0.07f, 1.4f / (players * packetsPerPlayer));
            int sent = 0;
            for (int round = 0; round < packetsPerPlayer; round++)
            {
                for (int player = 0; player < players; player++)
                {
                    var to = DestinationPoint(player);
                    if (!to.HasValue) continue;

                    _flights.Add(new CardFlights.Spec
                    {
                        Face = null,
                        Size = PileCard,
                        From = from.Value,
                        To = to.Value,
                        Delay = sent * stagger,
                        Duration = 0.35f,
                        StartScale = 1f,
                        EndScale = player == TableSession.HumanIndex ? 1f : 0.6f,
                        Arc = 14f,
                        OnStart = () => AudioManager.Play(Sound.CardDeal, 0.5f, RandomPitch())
                    });
                    sent++;
                }
            }

            if (sent == 0) FinishDeal();
        }

        private void FinishDeal()
        {
            _dealing = false;
            _session.Paused = _paused;
            _needsRefresh = true;
        }

        // ---------------------------------------------------------------- Effets des pouvoirs

        private static Color PowerColor(PowerType power)
        {
            switch (power)
            {
                case PowerType.Reset: return new Color(0.96f, 0.80f, 0.30f);
                case PowerType.Burn: return new Color(1.00f, 0.45f, 0.10f);
                case PowerType.Transparent: return new Color(0.65f, 0.92f, 1.00f);
                case PowerType.LowerOrEqual: return new Color(0.45f, 0.65f, 1.00f);
                case PowerType.SameOrFace: return new Color(0.78f, 0.55f, 1.00f);
                case PowerType.Skip: return new Color(1.00f, 0.38f, 0.32f);
                case PowerType.Reverse: return new Color(0.30f, 0.95f, 0.80f);
                case PowerType.ForcePickUp: return new Color(0.92f, 0.20f, 0.32f);
                default: return new Color(0.55f, 1.00f, 0.55f); // PlayAgain
            }
        }

        /// <summary>
        /// Un bandeau avec le nom du pouvoir au-dessus de la défausse, et un effet propre à chaque pouvoir :
        /// onde sur la pile, anneau qui tourne dans le sens du jeu, flash sur les joueurs visés.
        /// </summary>
        private void PlayPowerEffects(int player, MoveResult result)
        {
            if (_effects == null || _discardAnchor == null) return;

            var pileAt = PointOf(_discardAnchor);
            if (!pileAt.HasValue) return;
            var pile = pileAt.Value;
            var game = _session.Game;

            if (result.Power.HasValue)
            {
                var power = result.Power.Value;
                var color = PowerColor(power);

                // Une hauteur de note propre à chaque pouvoir : on les reconnaît à l'oreille.
                if (power != PowerType.Burn) AudioManager.Play(Sound.Power, 0.9f, 0.82f + 0.06f * (int)power);
                _effects.Banner(Loc.Get(PowerCatalog.LabelKey(power)), pile + new Vector3(0, 120, 0), color);

                switch (power)
                {
                    case PowerType.Skip:
                        foreach (int skipped in SkippedPlayers(player, result.Played.Count))
                            PulseAtSeat(skipped, color);
                        break;
                    case PowerType.Reverse:
                        // Les joueurs sont disposés dans le sens inverse des aiguilles d'une montre (sens +1) :
                        // l'anneau tourne dans le nouveau sens du jeu.
                        _effects.Orbit(pile, clockwise: game.Direction < 0, color: color);
                        break;
                    case PowerType.ForcePickUp:
                        if (result.ForcedPickUpPlayer.HasValue) PulseAtSeat(result.ForcedPickUpPlayer.Value, color);
                        _effects.Pulse(pile, color, 60f, 260f);
                        break;
                    case PowerType.PlayAgain:
                        PulseAtSeat(player, color);
                        break;
                    default:
                        _effects.Pulse(pile, color, 60f, 260f);
                        break;
                }
            }

            // Un jeu complet qui brûle, même sans pouvoir sur cette valeur.
            if (result.Burned && result.Power != PowerType.Burn)
            {
                _effects.Banner(Loc.Get("table.fx.full_set"), pile + new Vector3(0, 175, 0), PowerColor(PowerType.Burn), 0.1f);
                _effects.Pulse(pile, PowerColor(PowerType.Burn), 60f, 260f);
            }
        }

        private static float RandomPitch() => UnityEngine.Random.Range(0.94f, 1.06f);

        /// <summary>Fin de manche : fanfare si tu sors le premier, thème grave si tu es le Shithead, sinon deux notes neutres.</summary>
        private void PlayRoundEndSound()
        {
            var game = _session.Game;
            bool first = game.FinishOrder.Count > 0 && game.FinishOrder[0] == TableSession.HumanIndex;
            bool last = game.LoserIndex.HasValue && game.LoserIndex.Value == TableSession.HumanIndex;
            AudioManager.Play(first ? Sound.Win : last ? Sound.Lose : Sound.RoundEnd);
        }

        private void PulseAtSeat(int player, Color color)
        {
            var at = SeatPoint(player);
            if (at.HasValue) _effects.Pulse(at.Value, color, 80f, 320f, 0.7f);
        }

        /// <summary>Les joueurs que passe un « saute un tour » : les premiers joueurs encore en jeu après celui qui a posé.</summary>
        private List<int> SkippedPlayers(int actor, int count)
        {
            var game = _session.Game;
            var skipped = new List<int>();
            int n = game.Players.Count;
            for (int k = 1; k <= n && skipped.Count < count; k++)
            {
                int index = (((actor + game.Direction * k) % n) + n) % n;
                if (index == actor) break;
                if (game.Players[index].HasCards) skipped.Add(index);
            }
            return skipped;
        }

        private void BurnPile()
        {
            if (_visualDiscard.Count == 0 || _discardAnchor == null) return;

            AudioManager.Play(Sound.Burn);
            var top = _visualDiscard[_visualDiscard.Count - 1];
            _flights.Burn(top, _flights.ToLayer(_discardAnchor), PileCard, () =>
            {
                _visualDiscard.Clear();
                _needsRefresh = true;
            });
        }

        /// <summary>La défausse part vers celui qui la ramasse : au plus quelques cartes volent, la pile se vide aussitôt.</summary>
        private void FlyPileToPlayer(int player)
        {
            if (_visualDiscard.Count == 0 || _discardAnchor == null) return;

            var from = PointOf(_discardAnchor);
            var to = DestinationPoint(player);
            var cards = new List<Card>();
            for (int i = _visualDiscard.Count - 1; i >= 0 && cards.Count < MaxFlyingCards; i--)
                cards.Add(_visualDiscard[i]);

            _visualDiscard.Clear();
            _needsRefresh = true;
            if (!from.HasValue || !to.HasValue) return;

            for (int i = 0; i < cards.Count; i++)
            {
                _flights.Add(new CardFlights.Spec
                {
                    Face = cards[i],
                    Size = PileCard,
                    From = from.Value,
                    To = to.Value,
                    Delay = i * 0.06f,
                    Duration = 0.42f,
                    StartScale = 1f,
                    EndScale = player == TableSession.HumanIndex ? 1f : 0.6f,
                    Arc = -18f,
                    OnStart = i == 0 ? (Action)(() => AudioManager.Play(Sound.PickUp, 0.9f)) : null
                });
            }
        }

        /// <summary>Quelques dos de cartes partent de la pioche vers le joueur qui vient de piocher.</summary>
        private void FlyDraws(int player, int drawn)
        {
            int count = Mathf.Min(drawn, 3);
            if (count <= 0) return;

            var from = PointOf(_drawAnchor);
            var to = DestinationPoint(player);
            if (!from.HasValue || !to.HasValue) return;

            for (int i = 0; i < count; i++)
            {
                _flights.Add(new CardFlights.Spec
                {
                    Face = null,
                    Size = PileCard,
                    From = from.Value,
                    To = to.Value,
                    Delay = 0.22f + i * 0.08f,
                    Duration = 0.35f,
                    StartScale = 1f,
                    EndScale = player == TableSession.HumanIndex ? 1f : 0.6f,
                    Arc = 18f,
                    OnStart = () => AudioManager.Play(Sound.CardDeal, 0.6f, RandomPitch())
                });
            }
        }

        // ---------------------------------------------------------------- Reconstruction

        private void Refresh()
        {
            // On retient la position de chaque zone défilante avant de tout détruire.
            foreach (var entry in _scrolls)
                if (entry.Value != null) _scrollMemory[entry.Key] = entry.Value.normalizedPosition;
            _scrolls.Clear();

            // Les points de départ et d'arrivée des cartes volantes sont reposés à chaque reconstruction.
            _seatAnchors.Clear();
            _humanCardAnchors.Clear();
            _discardAnchor = _drawAnchor = _handArea = null;

            foreach (Transform child in _view)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var seats = TableSeats.For(_session.Game.Players.Count);
            for (int i = 0; i < seats.Length; i++) BuildSeat(i, seats[i]);
            BuildCenter();
            if (_session.RoundOver) BuildRoundOverlay();
            if (_paused) BuildPauseOverlay();

            // Une fois la mise en page calculée, on remet les zones défilantes là où elles étaient.
            Canvas.ForceUpdateCanvases();
            foreach (var entry in _scrolls)
                if (_scrollMemory.TryGetValue(entry.Key, out var position)) entry.Value.normalizedPosition = position;
        }

        // ---------------------------------------------------------------- Zone défilante

        /// <summary>
        /// Zone défilante (molette ou glisser). Retourne le contenu où poser les éléments, rangés en ligne
        /// (défilement horizontal) ou en colonne (défilement vertical).
        /// </summary>
        private RectTransform BuildScroll(Transform parent, string key, bool vertical, bool fitToContent = true)
        {
            var scrollRect = UiFactory.NewRect("Scroll_" + key, parent);
            UiFactory.Size(scrollRect, minWidth: 40, minHeight: 40, flexibleWidth: 1, flexibleHeight: 1);

            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = !vertical;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;

            var viewport = UiFactory.NewRect("Viewport", scrollRect);
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // reçoit les glissements

            var content = UiFactory.NewRect("Content", viewport);
            if (vertical)
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = StackSpacing;
                layout.childAlignment = TextAnchor.UpperCenter;
                ConfigureGroup(layout);
                if (fitToContent)
                    content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
                var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = StackSpacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                ConfigureGroup(layout);
                if (fitToContent)
                    content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            _scrolls[key] = scroll;
            return content;
        }

        private static void ConfigureGroup(HorizontalOrVerticalLayoutGroup group)
        {
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        }

        // ---------------------------------------------------------------- Bande d'un joueur

        private void BuildSeat(int index, SeatArea area)
        {
            var game = _session.Game;
            var player = game.Players[index];
            bool human = index == TableSession.HumanIndex;
            bool current = game.Phase == GamePhase.Play && game.CurrentPlayerIndex == index;

            // La bande garde une petite marge avec ses voisines, et ne déborde jamais de l'écran de référence
            // (les bandes du haut et du bas de l'ancienne disposition dépassent de quelques pixels).
            float halfWidth = TableSeats.ScreenWidth / 2 - ScreenInset;
            float halfHeight = TableSeats.ScreenHeight / 2 - ScreenInset;
            float left = Mathf.Max(area.Left + ScreenInset, -halfWidth);
            float right = Mathf.Min(area.Right - ScreenInset, halfWidth);
            float bottom = Mathf.Max(area.Bottom + ScreenInset, -halfHeight);
            float top = Mathf.Min(area.Top - ScreenInset, halfHeight);
            var seat = CenteredRect("Seat" + index, (left + right) / 2, (bottom + top) / 2, right - left, top - bottom);
            _seatAnchors[index] = seat;
            var background = seat.gameObject.AddComponent<Image>();
            background.color = current ? Theme.PanelCurrent : Theme.Panel;
            background.raycastTarget = false;
            if (current)
            {
                var outline = seat.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Accent;
                outline.effectDistance = new Vector2(3, -3);
            }
            if (!player.HasCards) seat.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;

            var column = UiFactory.Column(seat, 4, 8);
            UiFactory.Stretch(column);

            BuildSeatHeader(column, player, human, area.IsVertical);

            var content = BuildScroll(column, "seat" + index, area.IsVertical);
            if (!_dealing) BuildStacks(content, player, human, area.IsVertical); // masqué pendant la distribution
        }

        /// <summary>
        /// En-tête d'une bande : le nom, la difficulté de l'IA (sur sa propre ligne, pour tenir même dans une bande
        /// étroite) et le nombre de cartes en main. Chaque ligne garde sa hauteur : le texte ne passe jamais à la ligne.
        /// </summary>
        private void BuildSeatHeader(Transform parent, Player player, bool human, bool vertical)
        {
            string difficulty = human ? string.Empty : Loc.Get("difficulty." + player.Difficulty + ".label");
            string info = !player.HasCards ? Loc.Get("table.out")
                : human ? string.Empty : Loc.Get("table.hand_count", player.Hand.Count);

            if (vertical)
            {
                HeaderLine(parent, player.Name, 22, Theme.Text, FontStyle.Bold, 30);
                if (!human) HeaderLine(parent, difficulty, 20, Theme.Accent, FontStyle.Normal, 26);
                HeaderLine(parent, info, 20, Theme.Muted, FontStyle.Normal, 26);
                return;
            }

            var row = UiFactory.Row(parent, 12);
            UiFactory.Size(row, minHeight: 32, preferredHeight: 32);
            UiFactory.Label(row, player.Name, 22, Theme.Text, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.FlexibleSpace(row);
            UiFactory.Label(row, info, 20, Theme.Muted, FontStyle.Normal, TextAnchor.MiddleRight)
                .horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!human) HeaderLine(parent, difficulty, 20, Theme.Accent, FontStyle.Normal, 24);
        }

        private static void HeaderLine(Transform parent, string text, int size, Color color, FontStyle style, float height)
        {
            var label = UiFactory.Label(parent, text, size, color, style);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Size(label, minHeight: height, preferredHeight: height);
        }

        /// <summary>
        /// Une pile par emplacement : la carte cachée dessous, la carte visible posée dessus avec un léger décalage.
        /// Les bandes latérales sont tournées d'un quart de tour.
        /// </summary>
        private void BuildStacks(RectTransform content, Player player, bool human, bool vertical)
        {
            var cardSize = human ? HumanZoneCard : AiZoneCard;
            float offset = human ? HumanStackOffset : AiStackOffset;
            float raise = human ? CardFactory.Raise : 0f;
            var stackSize = new Vector2(cardSize.x + offset, cardSize.y + offset + raise);

            var game = _session.Game;
            bool myTurn = human && _session.IsHumanTurn;
            var zone = human ? game.GetActiveZone(TableSession.HumanIndex) : Zone.Hand;
            bool swapPhase = human && game.Phase == GamePhase.Swap;
            var jumpCards = human ? _session.HumanJumpInCards() : new List<Card>();

            int count = Math.Max(player.FaceDown.Count, player.FaceUp.Count);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                RectTransform stack;
                if (vertical)
                {
                    var slot = UiFactory.NewRect("StackSlot", content);
                    UiFactory.Size(slot, minWidth: stackSize.y, preferredWidth: stackSize.y, minHeight: stackSize.x,
                        preferredHeight: stackSize.x);
                    stack = UiFactory.NewRect("Stack", slot);
                    stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
                    stack.pivot = new Vector2(0.5f, 0.5f);
                    stack.anchoredPosition = Vector2.zero;
                    stack.sizeDelta = stackSize;
                    stack.localRotation = Quaternion.Euler(0, 0, 90);
                }
                else
                {
                    stack = UiFactory.NewRect("Stack", content);
                    UiFactory.Size(stack, minWidth: stackSize.x, preferredWidth: stackSize.x, minHeight: stackSize.y,
                        preferredHeight: stackSize.y);
                }

                if (index < player.FaceDown.Count)
                {
                    bool blind = myTurn && zone == Zone.FaceDown;
                    CardFactory.BackAt(stack, cardSize, Vector2.zero, human ? (Action)(() => OnFaceDownClicked(index)) : null,
                        new CardState { Interactable = blind, Highlight = blind });
                }

                if (index < player.FaceUp.Count)
                {
                    var card = player.FaceUp[index];
                    var state = human
                        ? StateFor(card, index, swapPhase, myTurn && zone == Zone.FaceUp, jumpCards,
                            zone == Zone.FaceUp, swapTarget: true)
                        : new CardState();
                    var faceUp = CardFactory.FaceAt(stack, card, cardSize, new Vector2(offset, offset),
                        human ? (Action)(() => OnFaceUpClicked(index)) : null, state);
                    if (human)
                        _humanCardAnchors.Add(new KeyValuePair<Card, RectTransform>(card, (RectTransform)faceUp.transform));
                }
            }
        }

        // ---------------------------------------------------------------- Centre

        private void BuildCenter()
        {
            var game = _session.Game;
            var area = TableSeats.Center;
            var center = CenteredRect("Center", area.X, area.Y, area.Width, area.Height);
            var column = UiFactory.Column(center, 10, 0, TextAnchor.UpperCenter);
            UiFactory.Stretch(column);

            BuildInfoRow(column);

            bool swapPhase = game.Phase == GamePhase.Swap;
            bool myTurn = _session.IsHumanTurn;
            var zone = game.GetActiveZone(TableSession.HumanIndex);
            var jumpCards = _session.HumanJumpInCards();

            // Indication du moment (une ligne réservée, jamais superposée aux autres éléments).
            string hint = string.Empty;
            Color hintColor = Theme.Muted;
            if (_dealing) hint = string.Empty;
            else if (swapPhase) hint = Loc.Get("table.swap_hint");
            else if (jumpCards.Count > 0)
            {
                hint = Loc.Get("table.jump_in_hint");
                hintColor = new Color(1f, 0.82f, 0.25f);
            }
            else if (myTurn && zone == Zone.FaceDown) hint = Loc.Get("table.play_blind");
            else if (myTurn && !game.HasLegalPlay(TableSession.HumanIndex)) hint = Loc.Get("table.must_pick_up");
            else if (myTurn && _selected.Count > 0) hint = Loc.Get("table.click_again");

            var hintLabel = UiFactory.Label(column, hint, 24, hintColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Size(hintLabel, minHeight: 34, preferredHeight: 34);

            // Boutons d'action.
            var buttons = UiFactory.Row(column, 20, TextAnchor.MiddleCenter);
            UiFactory.Size(buttons, minHeight: 56, preferredHeight: 56);
            if (_dealing)
            {
                // Pas de bouton tant que les cartes sont en train d'être distribuées.
            }
            else if (swapPhase)
            {
                UiFactory.MakeButton(buttons, Loc.Get("table.ready"), OnReady, 30, 300, 56, Theme.Accent);
            }
            else if (myTurn && zone != Zone.FaceDown)
            {
                if (_selected.Count > 0)
                    UiFactory.MakeButton(buttons, Loc.Get("table.play"), PlaySelection, 30, 260, 56, Theme.Accent);
                if (!game.HasLegalPlay(TableSession.HumanIndex))
                    UiFactory.MakeButton(buttons, Loc.Get("table.pick_up"), OnPickUp, 30, 420, 56, Theme.Accent);
            }

            BuildHand(column, game.Players[TableSession.HumanIndex], swapPhase, myTurn && zone == Zone.Hand, jumpCards,
                zone == Zone.Hand, area.Width);
        }

        private void BuildInfoRow(Transform parent)
        {
            var game = _session.Game;
            var row = UiFactory.Row(parent, 30, TextAnchor.MiddleCenter);
            UiFactory.Size(row, minHeight: 190, preferredHeight: 190);

            // Colonne de gauche : manche, tour en cours, cartes brûlées, abandon.
            var info = UiFactory.Column(row, 4, 0, TextAnchor.MiddleLeft);
            UiFactory.Size(info, minWidth: 480, preferredWidth: 480, flexibleWidth: 0);
            UiFactory.Label(info, Loc.Get("table.round", Mathf.Max(1, _match.RoundNumber), _match.Settings.RoundCount),
                26, Theme.Muted, FontStyle.Bold);

            string turn = string.Empty;
            if (game.Phase == GamePhase.Play)
                turn = _session.IsHumanTurn ? Loc.Get("table.your_turn") : Loc.Get("table.turn_of", NameOf(game.CurrentPlayerIndex));
            UiFactory.Label(info, turn, 30, _session.IsHumanTurn ? Theme.Accent : Theme.Text, FontStyle.Bold);

            UiFactory.Label(info, Loc.Get("table.burned", game.BurnedCount), 22, Theme.Muted);
            if (game.Direction < 0)
                UiFactory.Label(info, Loc.Get("table.direction_reversed"), 22, Theme.Accent, FontStyle.Bold);
            // Le menu pause regroupe les sorties (menu, quitter le jeu, abandonner) et le son.
            var pauseRow = UiFactory.Row(info, 10);
            UiFactory.MakeButton(pauseRow, Loc.Get("table.pause"), () => SetPaused(true), 22, 240, 46);

            // Pioche.
            var draw = UiFactory.Column(row, 4, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(draw, minWidth: 130, preferredWidth: 130, flexibleWidth: 0);
            _drawAnchor = game.DrawPile.Count > 0
                ? (RectTransform)CardFactory.Back(draw, PileCard, null, new CardState()).transform
                : CardFactory.EmptySlot(draw, PileCard);
            UiFactory.Label(draw, $"{Loc.Get("table.draw_pile")} ({game.DrawPile.Count})", 20, Theme.Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);

            // Défausse : celle qui est affichée peut retarder sur la partie le temps que les cartes arrivent.
            var shown = ShownDiscard();
            var discard = UiFactory.Column(row, 4, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(discard, minWidth: 130, preferredWidth: 130, flexibleWidth: 0);
            _discardAnchor = shown.Count > 0
                ? (RectTransform)CardFactory.Face(discard, shown[shown.Count - 1], PileCard, null, new CardState()).transform
                : CardFactory.EmptySlot(discard, PileCard);
            UiFactory.Label(discard, $"{Loc.Get("table.discard_pile")} ({shown.Count})", 20, Theme.Muted,
                FontStyle.Normal, TextAnchor.MiddleCenter);

            // Journal des derniers coups.
            var log = UiFactory.Column(row, 2, 0, TextAnchor.MiddleLeft);
            UiFactory.Size(log, minWidth: 500, preferredWidth: 500, flexibleWidth: 0);
            for (int i = 0; i < _log.Count; i++)
            {
                bool latest = i == _log.Count - 1;
                UiFactory.Label(log, _log[i], latest ? 24 : 20, latest ? Theme.Text : Theme.Muted,
                    latest ? FontStyle.Bold : FontStyle.Normal);
            }
        }

        /// <summary>La main du joueur : une rangée défilante, centrée tant que toutes les cartes tiennent.</summary>
        private void BuildHand(Transform parent, Player me, bool swapPhase, bool myZone, List<Card> jumpCards,
            bool jumpZone, float viewportWidth)
        {
            var content = BuildScroll(parent, "hand", vertical: false, fitToContent: false);

            // Les cartes se chevauchent quand elles sont nombreuses (chacune garde une part visible) ; si elles ne
            // tiennent toujours pas, la zone défile. Le contenu est aussi large que la zone visible quand tout tient,
            // ce qui garde les cartes centrées.
            const float padding = 10f;
            float spacing = HandOverlapSpacing(me.Hand.Count, HandCard.x, viewportWidth - 2 * padding);

            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset((int)padding, (int)padding, 0, 0);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 0.5f);

            int count = me.Hand.Count;
            float needed = count == 0 ? 0f : count * HandCard.x + (count - 1) * spacing + 2 * padding;
            content.offsetMin = Vector2.zero;
            content.offsetMax = new Vector2(Mathf.Max(0f, needed - viewportWidth), 0f);

            // Affichage trié par puissance (le 2, le plus fort, à droite), couleur par couleur à égalité.
            // Les clics gardent l'indice réel de la carte dans la main : seul l'ordre d'affichage change.
            var order = Enumerable.Range(0, me.Hand.Count)
                .OrderBy(i => Card.Strength(me.Hand[i].Rank))
                .ThenBy(i => (int)me.Hand[i].Suit)
                .ToList();

            _handArea = content;
            if (_dealing) return; // la main apparaît quand les cartes sont arrivées

            foreach (int index in order)
            {
                var card = me.Hand[index];
                var button = CardFactory.Face(content, card, HandCard, () => OnHandClicked(index),
                    StateFor(card, index, swapPhase, myZone, jumpCards, jumpZone, swapTarget: false));
                _humanCardAnchors.Add(new KeyValuePair<Card, RectTransform>(card, (RectTransform)button.transform));
            }
        }

        /// <summary>
        /// Espace entre deux cartes de la main : normal tant que tout tient, négatif (cartes qui se chevauchent)
        /// sinon, sans jamais cacher plus que <see cref="MinVisibleSlice"/> de chaque carte.
        /// </summary>
        private static float HandOverlapSpacing(int count, float cardWidth, float available)
        {
            if (count <= 1) return HandSpacing;

            float full = count * cardWidth + (count - 1) * HandSpacing;
            if (full <= available) return HandSpacing;

            float tight = (available - count * cardWidth) / (count - 1);
            return Mathf.Max(tight, -(cardWidth - MinVisibleSlice));
        }

        /// <summary>Apparence et possibilités d'une carte du joueur, selon la phase et la zone.</summary>
        private CardState StateFor(Card card, int index, bool swapPhase, bool myZone, List<Card> jumpCards,
            bool jumpZone, bool swapTarget)
        {
            var state = new CardState();

            if (swapPhase)
            {
                state.Interactable = true;
                state.Selected = !swapTarget && index == _swapHandIndex;
                return state;
            }

            if (myZone)
            {
                bool playable = _session.Game.CanPlayRank(card.Rank);
                state.Interactable = playable;
                state.Dimmed = !playable;
                state.Selected = playable && _selected.Contains(index);
                return state;
            }

            if (jumpZone && jumpCards.Contains(card))
            {
                state.Interactable = true;
                state.Highlight = true;
            }
            return state;
        }

        // ---------------------------------------------------------------- Actions du joueur

        private void AfterAction()
        {
            HandleEvents(_session.DrainEvents());
            Refresh();
            SaveProgress();
        }

        private void OnHandClicked(int index) => OnOwnCardClicked(Zone.Hand, index);
        private void OnFaceUpClicked(int index) => OnOwnCardClicked(Zone.FaceUp, index);

        private void OnOwnCardClicked(Zone zone, int index)
        {
            var game = _session.Game;
            var me = game.Players[TableSession.HumanIndex];

            // Phase d'échange : une carte de la main, puis une carte visible.
            if (game.Phase == GamePhase.Swap)
            {
                if (zone == Zone.Hand)
                {
                    _swapHandIndex = _swapHandIndex == index ? -1 : index;
                }
                else if (_swapHandIndex >= 0 && _swapHandIndex < me.Hand.Count && index < me.FaceUp.Count)
                {
                    _session.HumanSwap(me.Hand[_swapHandIndex], me.FaceUp[index]);
                    _swapHandIndex = -1;
                    SaveProgress(); // l'échange change les cartes : on l'enregistre aussi
                }
                Refresh();
                return;
            }

            // Jeu à la volée : la dernière carte d'un jeu complet de même valeur.
            if (!_session.IsHumanTurn)
            {
                var jump = _session.HumanJumpInCards();
                if (jump.Count > 0) _session.HumanJumpIn(jump.Take(1).ToList());
                AfterAction();
                return;
            }

            if (game.GetActiveZone(TableSession.HumanIndex) != zone) return;
            var cards = zone == Zone.Hand ? me.Hand : me.FaceUp;
            if (index >= cards.Count || !game.CanPlayRank(cards[index].Rank)) return;

            // Un clic sélectionne (une seule valeur à la fois) ; un clic sur une carte déjà sélectionnée la joue.
            if (_selected.Contains(index))
            {
                PlaySelection();
                return;
            }

            if (_selected.Count > 0 && cards[_selected[0]].Rank != cards[index].Rank) _selected.Clear();
            _selected.Add(index);
            Refresh();
        }

        private void PlaySelection()
        {
            var game = _session.Game;
            var me = game.Players[TableSession.HumanIndex];
            var zone = game.GetActiveZone(TableSession.HumanIndex);
            var source = zone == Zone.Hand ? me.Hand : me.FaceUp;

            var cards = _selected.Where(i => i < source.Count).Select(i => source[i]).ToList();
            _selected.Clear();
            if (cards.Count > 0 && zone != Zone.FaceDown) _session.HumanPlay(cards);
            AfterAction();
        }

        private void OnFaceDownClicked(int index)
        {
            if (_session.IsHumanTurn) _session.HumanPlayFaceDown(index);
            AfterAction();
        }

        private void OnPickUp()
        {
            _session.HumanPickUp();
            AfterAction();
        }

        private void OnReady()
        {
            _swapHandIndex = -1;
            _session.HumanReady();
            AfterAction();
        }

        // ---------------------------------------------------------------- Fin de manche

        private void BuildRoundOverlay()
        {
            var game = _session.Game;
            bool matchOver = _match.IsOver;

            var overlay = UiFactory.NewRect("Overlay", _view);
            UiFactory.Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

            var panelHost = UiFactory.NewRect("PanelHost", overlay);
            UiFactory.Stretch(panelHost);
            var centerLayout = panelHost.gameObject.AddComponent<VerticalLayoutGroup>();
            centerLayout.childAlignment = TextAnchor.MiddleCenter;
            centerLayout.childControlWidth = true;
            centerLayout.childControlHeight = true;
            centerLayout.childForceExpandWidth = false;
            centerLayout.childForceExpandHeight = false;

            var panel = UiFactory.Card(panelHost, Theme.Panel, 36, 12);
            UiFactory.Size(panel, preferredWidth: 760, flexibleWidth: 0);

            string title = matchOver
                ? Loc.Get("table.match_over")
                : Loc.Get("table.round_over", _match.RoundNumber);
            UiFactory.Label(panel, title, 44, Theme.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);

            if (game.IsStalemate)
                UiFactory.Label(panel, Loc.Get("table.log.stalemate"), 22, Theme.Muted, FontStyle.Italic,
                    TextAnchor.MiddleCenter);

            UiFactory.Label(panel, Loc.Get("table.finish_order"), 28, Theme.Text, FontStyle.Bold);
            for (int place = 0; place < game.FinishOrder.Count; place++)
            {
                int player = game.FinishOrder[place];
                bool last = game.LoserIndex.HasValue && game.LoserIndex.Value == player;
                string line = $"{place + 1}. {NameOf(player)}" + (last ? $"  ({Loc.Get("table.last")})" : string.Empty);
                UiFactory.Label(panel, line, 24, last ? Theme.Error : Theme.Text);
            }

            UiFactory.Label(panel, Loc.Get("table.standings"), 28, Theme.Text, FontStyle.Bold);
            var standings = _match.GetStandings();
            foreach (var entry in standings)
                UiFactory.Label(panel, Loc.Get("table.standing_row", entry.Place, entry.Name, entry.Points), 24,
                    Theme.Muted);

            if (matchOver)
                UiFactory.Label(panel, Loc.Get("table.winner", standings[0].Name), 32, Theme.Accent, FontStyle.Bold,
                    TextAnchor.MiddleCenter);

            var buttons = UiFactory.Row(panel, 20, TextAnchor.MiddleCenter);
            if (matchOver)
                UiFactory.MakeButton(buttons, Loc.Get("table.back_to_menu"), Abandon, 30, 360, 64, Theme.Accent);
            else
                UiFactory.MakeButton(buttons, Loc.Get("table.next_round"), OnNextRound, 30, 360, 64, Theme.Accent);
        }

        private void OnNextRound()
        {
            _log.Clear();
            _scrollMemory.Clear();
            _session.StartRound();
            HandleEvents(_session.DrainEvents());
            StartDeal();
            Refresh();
            PlayDealAnimation();
            SaveProgress();
        }
    }
}

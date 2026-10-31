using System;
using System.Collections;
using System.Linq;
using CaSentLeSoufre.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// Menu du jeu en deux pages : l'écran principal (mode, nombre de joueurs et de manches, Jouer) et la page
    /// Paramètres (profil, adversaires, pouvoirs, options, langue). Toute la logique est dans
    /// <see cref="MatchSetupModel"/> ; les préférences sont enregistrées à chaque changement.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Page
        {
            Main,
            Settings
        }

        private const float ColumnWidth = 1100f;

        private MatchSetupModel _model;
        private Page _page = Page.Main;

        private ScrollRect _scroll;
        private RectTransform _column;
        private RectTransform _errorsHost;
        private Button _startButton;
        private Text _noticeLabel;

        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;

            var preferences = SetupStorage.Load();
            string language = preferences?.Language;
            if (string.IsNullOrEmpty(language))
                language = Application.systemLanguage == SystemLanguage.French ? Loc.French : Loc.English;

            Loc.SetLanguage(language);
            yield return LocalizationSettings.SelectedLocaleAsync;

            _model = MatchSetupModel.FromPreferences(preferences, Loc.Get("menu.you"));
            _model.Language = language;

            BuildCanvas();
            Rebuild();
        }

        private void OnDisable() => SavePreferences();
        private void OnApplicationQuit() => SavePreferences();

        private void OnApplicationPause(bool paused)
        {
            if (paused) SavePreferences();
        }

        private void SavePreferences()
        {
            if (_model != null) SetupStorage.Save(_model.ToPreferences());
        }

        // ---------------------------------------------------------------- Mise en place

        private void BuildCanvas()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasObject.transform;
            var background = UiFactory.NewRect("Background", root);
            UiFactory.Stretch(background);
            background.gameObject.AddComponent<Image>().color = Theme.Background;

            var scrollRect = UiFactory.NewRect("Scroll", root);
            UiFactory.Stretch(scrollRect);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 45f;

            var viewport = UiFactory.NewRect("Viewport", scrollRect);
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0); // reçoit les glissements même sur les zones vides

            var content = UiFactory.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 40, 60);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = content;

            _column = UiFactory.Column(content, 22);
            UiFactory.Size(_column, preferredWidth: ColumnWidth, flexibleWidth: 0);
        }

        // ---------------------------------------------------------------- Reconstruction

        private void ShowPage(Page page)
        {
            _page = page;
            _scroll.verticalNormalizedPosition = 1f;
            Rebuild();
        }

        private void Rebuild()
        {
            float scrollPosition = _scroll.verticalNormalizedPosition;

            // Désactiver avant de détruire : la mise en page ne compte plus les anciens éléments.
            foreach (Transform child in _column)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            _errorsHost = null;
            _startButton = null;
            _noticeLabel = null;

            if (_page == Page.Main) BuildMainPage();
            else BuildSettingsPage();

            RefreshStatus();
            SavePreferences();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_scroll.content);
            _scroll.verticalNormalizedPosition = scrollPosition;
        }

        /// <summary>Met à jour les erreurs et le bouton Jouer sans reconstruire (la saisie garde le focus).</summary>
        private void RefreshStatus()
        {
            if (_errorsHost == null) return;

            foreach (Transform child in _errorsHost)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var errors = _model.Validate();
            if (_startButton != null) _startButton.interactable = errors.Count == 0;
            if (errors.Count == 0) return;

            var card = UiFactory.Card(_errorsHost, Theme.Panel, 22, 8);
            UiFactory.Label(card, Loc.Get("menu.errors.title"), 26, Theme.Error, FontStyle.Bold);
            foreach (var error in errors)
                UiFactory.Label(card, "- " + Loc.Get(error), 24, Theme.Error);
            if (_page == Page.Main)
                UiFactory.Label(card, Loc.Get("menu.fix_settings"), 22, Theme.Muted);

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_scroll.content);
        }

        // ---------------------------------------------------------------- Page principale

        private void BuildMainPage()
        {
            var header = UiFactory.Row(_column, 16);
            UiFactory.Label(header, Loc.Get("game.title"), 72, Theme.Accent, FontStyle.Bold);
            UiFactory.FlexibleSpace(header);
            UiFactory.MakeButton(header, Loc.Get("menu.settings"), () => ShowPage(Page.Settings), 26, 260, 60);

            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.quick.title"), 36, Theme.Text, FontStyle.Bold);

            // Mode de jeu.
            UiFactory.Label(card, Loc.Get("menu.mode.label"), 22, Theme.Muted);
            var modes = new[] { GameMode.Classic, GameMode.Demon };
            var modeLabels = new[] { Loc.Get("mode.Classic.label"), Loc.Get("mode.Demon.label") };
            UiFactory.Segmented(card, modeLabels, Array.IndexOf(modes, _model.Mode), index =>
            {
                _model.SetMode(modes[index]);
                Rebuild();
            }, 26, 60);
            UiFactory.Label(card, Loc.Get($"mode.{_model.Mode}.description"), 22, Theme.Muted);

            // Nombre de joueurs.
            var playersRow = UiFactory.Row(card, 10);
            UiFactory.Label(playersRow, Loc.Get("menu.players.count"), 26, Theme.Text);
            UiFactory.FlexibleSpace(playersRow);
            AddStepButton(playersRow, "-", () => _model.AdjustPlayerCount(-1), _model.PlayerCount > _model.MinPlayers);
            AddValue(playersRow, $"{_model.PlayerCount}");
            AddStepButton(playersRow, "+", () => _model.AdjustPlayerCount(1), _model.PlayerCount < _model.MaxPlayers);

            var versus = _model.Opponents.Select(o => o.Name.Trim());
            UiFactory.Label(card, Loc.Get("menu.versus", string.Join(", ", versus)), 22, Theme.Muted);

            // Nombre de manches.
            var roundsRow = UiFactory.Row(card, 10);
            UiFactory.Label(roundsRow, Loc.Get("menu.rounds.label"), 26, Theme.Text);
            UiFactory.FlexibleSpace(roundsRow);
            AddStepButton(roundsRow, "-10", () => _model.AdjustRoundCount(-10));
            AddStepButton(roundsRow, "-1", () => _model.AdjustRoundCount(-1));
            AddValue(roundsRow, _model.RoundCount.ToString());
            AddStepButton(roundsRow, "+1", () => _model.AdjustRoundCount(1));
            AddStepButton(roundsRow, "+10", () => _model.AdjustRoundCount(10));

            _errorsHost = UiFactory.Column(_column, 0);

            _startButton = UiFactory.MakeButton(_column, Loc.Get("menu.start"), OnStart, 36, 0, 84, Theme.Accent);
            _noticeLabel = UiFactory.Label(_column, string.Empty, 24, Theme.Muted, FontStyle.Italic,
                TextAnchor.MiddleCenter);
        }

        private void AddStepButton(Transform parent, string text, Action change, bool interactable = true)
        {
            UiFactory.MakeButton(parent, text, () =>
            {
                change();
                Rebuild();
            }, 26, text.Length > 2 ? 96 : 76, 56, Theme.PanelLight, Theme.Text, interactable);
        }

        private static void AddValue(Transform parent, string text)
        {
            var value = UiFactory.Label(parent, text, 40, Theme.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiFactory.Size(value, minWidth: 130, preferredWidth: 130, flexibleWidth: 0);
        }

        private void OnStart()
        {
            if (!_model.IsValid)
            {
                RefreshStatus();
                return;
            }

            SavePreferences();
            GameSession.SetPending(_model.BuildSettings());
            _noticeLabel.text = Loc.Get("menu.start_pending");
            Debug.Log($"[Menu] Partie prête : {GameSession.Pending.PlayerCount} joueurs, " +
                      $"{GameSession.Pending.RoundCount} manche(s), mode {GameSession.Pending.Rules.Mode}.");
        }

        // ---------------------------------------------------------------- Page Paramètres

        private void BuildSettingsPage()
        {
            var header = UiFactory.Row(_column, 16);
            UiFactory.MakeButton(header, Loc.Get("menu.back"), () => ShowPage(Page.Main), 26, 220, 60);
            UiFactory.Label(header, Loc.Get("menu.settings"), 56, Theme.Accent, FontStyle.Bold);
            UiFactory.FlexibleSpace(header);

            _errorsHost = UiFactory.Column(_column, 0);

            BuildProfileSection();
            BuildOpponentsSection();
            BuildPowersSection();
            BuildOptionsSection();
            BuildLanguageSection();

            UiFactory.Label(_column, Loc.Get("menu.autosave"), 22, Theme.Muted, FontStyle.Italic,
                TextAnchor.MiddleCenter);
            UiFactory.MakeButton(_column, Loc.Get("menu.back"), () => ShowPage(Page.Main), 32, 0, 72, Theme.Accent);
        }

        private void BuildProfileSection()
        {
            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.section.profile"), 36, Theme.Text, FontStyle.Bold);

            var row = UiFactory.Row(card, 14);
            var label = UiFactory.Label(row, Loc.Get("menu.profile.name"), 24, Theme.Muted);
            UiFactory.Size(label, minWidth: 190, preferredWidth: 190, flexibleWidth: 0);
            UiFactory.MakeInput(row, _model.HumanName, Loc.Get("menu.name_placeholder"),
                MatchSettings.MaxNameLength, value =>
                {
                    _model.SetHumanName(value);
                    RefreshStatus();
                    SavePreferences();
                });
        }

        private void BuildOpponentsSection()
        {
            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.section.opponents"), 36, Theme.Text, FontStyle.Bold);
            UiFactory.Label(card, Loc.Get("menu.opponents.hint"), 22, Theme.Muted);

            var difficulties = new[] { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard };
            var difficultyLabels = new[]
            {
                Loc.Get("difficulty.Easy.label"), Loc.Get("difficulty.Normal.label"), Loc.Get("difficulty.Hard.label")
            };

            for (int i = 0; i < _model.Roster.Count; i++)
            {
                int index = i;
                var ai = _model.Roster[i];
                bool used = _model.IsRosterEntryUsed(i);

                var row = UiFactory.Row(card, 14);
                if (!used) row.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;

                string seat = "#" + (i + 2) + (used ? string.Empty : "  (" + Loc.Get("menu.unused") + ")");
                var label = UiFactory.Label(row, seat, 22, Theme.Muted);
                UiFactory.Size(label, minWidth: 250, preferredWidth: 250, flexibleWidth: 0);

                UiFactory.MakeInput(row, ai.Name, Loc.Get("menu.name_placeholder"), MatchSettings.MaxNameLength,
                    value =>
                    {
                        _model.SetRosterName(index, value);
                        RefreshStatus();
                        SavePreferences();
                    });

                var segmented = UiFactory.Segmented(row, difficultyLabels, Array.IndexOf(difficulties, ai.Difficulty),
                    choice =>
                    {
                        _model.SetRosterDifficulty(index, difficulties[choice]);
                        Rebuild();
                    }, 22, 56);
                UiFactory.Size(segmented, minWidth: 380, preferredWidth: 440, flexibleWidth: 0);
            }
        }

        private void BuildPowersSection()
        {
            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.section.powers", _model.ActivePowerCount, _model.AvailablePowerCount),
                36, Theme.Text, FontStyle.Bold);
            UiFactory.Label(card, Loc.Get("menu.powers.hint"), 22, Theme.Muted);

            foreach (var power in PowerCatalog.All)
            {
                var captured = power;
                bool active = _model.IsPowerActive(power);

                var panel = UiFactory.Card(card, Theme.PanelLight, 16, 6);
                var row = UiFactory.Row(panel, 16);

                UiFactory.MakeButton(row, active ? "ON" : "OFF", () =>
                {
                    _model.TogglePower(captured);
                    Rebuild();
                }, 22, 110, 52, active ? Theme.Accent : Theme.Disabled);

                var text = UiFactory.Column(row, 2);
                UiFactory.Size(text, minWidth: 0, preferredWidth: 100, flexibleWidth: 1);
                UiFactory.Label(text, Loc.Get(PowerCatalog.LabelKey(power)), 26, Theme.Text, FontStyle.Bold);
                UiFactory.Label(text, Loc.Get(PowerCatalog.DescriptionKey(power)), 20, Theme.Muted);

                if (!active) continue;

                var stepper = UiFactory.Row(row, 8, TextAnchor.MiddleCenter);
                UiFactory.Size(stepper, minWidth: 330, preferredWidth: 330, flexibleWidth: 0);
                var caption = UiFactory.Label(stepper, Loc.Get("menu.power.value"), 20, Theme.Muted,
                    FontStyle.Normal, TextAnchor.MiddleRight);
                UiFactory.Size(caption, minWidth: 90, preferredWidth: 90, flexibleWidth: 0);
                AddRankButton(stepper, "<", captured, -1);
                var value = UiFactory.Label(stepper, RankText(_model.RankOf(power)), 28, Theme.Accent,
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                UiFactory.Size(value, minWidth: 90, preferredWidth: 90, flexibleWidth: 0);
                AddRankButton(stepper, ">", captured, 1);
            }
        }

        private void AddRankButton(Transform parent, string text, PowerType power, int direction)
        {
            UiFactory.MakeButton(parent, text, () =>
            {
                _model.StepPowerRank(power, direction);
                Rebuild();
            }, 26, 56, 52);
        }

        private static string RankText(int rank) =>
            rank <= 10 ? rank.ToString() : Loc.Get("card.rankname." + rank);

        private void BuildOptionsSection()
        {
            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.section.options"), 36, Theme.Text, FontStyle.Bold);

            AddOption(card, "option.four_of_a_kind", _model.FourOfAKindBurns,
                () => _model.FourOfAKindBurns = !_model.FourOfAKindBurns);
            AddOption(card, "option.jump_in", _model.JumpIn, () => _model.JumpIn = !_model.JumpIn);
            AddOption(card, "option.initial_swap", _model.AllowInitialSwap,
                () => _model.AllowInitialSwap = !_model.AllowInitialSwap);
        }

        private void AddOption(Transform parent, string keyPrefix, bool value, Action toggle)
        {
            var panel = UiFactory.Card(parent, Theme.PanelLight, 16, 6);
            var row = UiFactory.Row(panel, 16);

            UiFactory.MakeButton(row, value ? "ON" : "OFF", () =>
            {
                toggle();
                Rebuild();
            }, 22, 110, 52, value ? Theme.Accent : Theme.Disabled);

            var text = UiFactory.Column(row, 2);
            UiFactory.Size(text, minWidth: 0, preferredWidth: 100, flexibleWidth: 1);
            UiFactory.Label(text, Loc.Get(keyPrefix + ".label"), 26, Theme.Text, FontStyle.Bold);
            UiFactory.Label(text, Loc.Get(keyPrefix + ".description"), 20, Theme.Muted);
        }

        private void BuildLanguageSection()
        {
            var card = UiFactory.Card(_column);
            UiFactory.Label(card, Loc.Get("menu.language"), 36, Theme.Text, FontStyle.Bold);

            var codes = new[] { Loc.French, Loc.English };
            var labels = codes.Select(code => Loc.Get("language." + code)).ToArray();
            UiFactory.Segmented(card, labels, Array.IndexOf(codes, CurrentLanguage()), index => SwitchLanguage(codes[index]),
                26, 60);
        }

        private static string CurrentLanguage()
        {
            var locale = LocalizationSettings.SelectedLocale;
            return locale == null ? Loc.French : locale.Identifier.Code;
        }

        private void SwitchLanguage(string code)
        {
            string previousDefaultName = Loc.Get("menu.you");
            if (!Loc.SetLanguage(code)) return;

            _model.Language = code;
            StartCoroutine(RebuildAfterLanguageChange(previousDefaultName));
        }

        private IEnumerator RebuildAfterLanguageChange(string previousDefaultName)
        {
            yield return LocalizationSettings.SelectedLocaleAsync;

            // Le nom par défaut suit la langue tant que le joueur ne l'a pas personnalisé.
            if (_model.HumanName == previousDefaultName) _model.SetHumanName(Loc.Get("menu.you"));
            Rebuild();
        }
    }
}

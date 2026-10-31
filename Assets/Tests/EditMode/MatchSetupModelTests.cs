using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class MatchSetupModelTests
    {
        [Test]
        public void Defaults_AreValid_ClassicThreePlayersThreeRounds()
        {
            var model = new MatchSetupModel("Max");
            Assert.AreEqual(GameMode.Classic, model.Mode);
            Assert.AreEqual(3, model.PlayerCount);
            Assert.AreEqual(3, model.RoundCount);
            Assert.AreEqual("Max", model.HumanName);
            Assert.AreEqual(MatchSetupModel.RosterSize, model.Roster.Count);
            Assert.AreEqual(2, model.Opponents.Count);
            Assert.IsTrue(model.IsValid);
            Assert.AreEqual(2, model.ActivePowerCount, "2 = remise à zéro, 10 = brûlure");
        }

        [Test]
        public void PlayerCount_IsClampedToTheModeLimits()
        {
            var model = new MatchSetupModel();
            model.SetPlayerCount(1);
            Assert.AreEqual(3, model.PlayerCount);
            model.SetPlayerCount(99);
            Assert.AreEqual(5, model.PlayerCount, "Classique : 5 joueurs maximum");
            model.AdjustPlayerCount(+1);
            Assert.AreEqual(5, model.PlayerCount);

            model.SetMode(GameMode.Demon);
            model.SetPlayerCount(99);
            Assert.AreEqual(10, model.PlayerCount, "Démon : 10 joueurs maximum");
            Assert.AreEqual(9, model.Opponents.Count);
            Assert.IsTrue(model.IsValid);
        }

        [Test]
        public void SwitchingBackToClassic_ReducesThePlayerCount_ButKeepsTheRoster()
        {
            var model = new MatchSetupModel();
            model.SetMode(GameMode.Demon);
            model.SetPlayerCount(10);
            model.SetRosterName(8, "Dernier");

            model.SetMode(GameMode.Classic);
            Assert.AreEqual(5, model.PlayerCount);
            Assert.AreEqual(4, model.Opponents.Count);
            Assert.AreEqual("Dernier", model.Roster[8].Name, "L'IA inutilisée garde son nom");

            model.SetMode(GameMode.Demon);
            model.SetPlayerCount(10);
            Assert.AreEqual("Dernier", model.Opponents[8].Name);
        }

        [Test]
        public void OpponentsPlaying_FollowThePlayerCount_WithTheirOwnSettings()
        {
            var model = new MatchSetupModel();
            model.SetRosterName(2, "Gargouille");
            model.SetRosterDifficulty(2, AiDifficulty.Hard);

            model.SetPlayerCount(3);
            Assert.AreEqual(2, model.Opponents.Count);
            Assert.IsFalse(model.IsRosterEntryUsed(2));

            model.SetPlayerCount(5);
            Assert.IsTrue(model.IsRosterEntryUsed(2));
            Assert.AreEqual("Gargouille", model.Opponents[2].Name);

            var settings = model.BuildSettings();
            Assert.AreEqual(5, settings.PlayerCount);
            Assert.AreEqual("Gargouille", settings.Opponents[2].Name);
            Assert.AreEqual(AiDifficulty.Hard, settings.Opponents[2].Difficulty);
        }

        [Test]
        public void Names_AreValidatedForTheWholeRoster_EvenUnusedEntries()
        {
            var model = new MatchSetupModel("Max");
            Assert.IsTrue(model.IsValid);

            model.SetRosterName(7, model.Roster[0].Name.ToUpperInvariant()); // IA inutilisée en doublon
            Assert.IsFalse(model.IsValid);
            Assert.IsTrue(model.Validate().Any(e => e.Key == "validation.name_duplicate"));

            model.SetRosterName(7, "Unique");
            Assert.IsTrue(model.IsValid);

            model.SetRosterName(7, "   ");
            Assert.IsFalse(model.IsValid, "Nom vide");

            model.SetRosterName(7, "Unique");
            model.SetHumanName(model.Roster[3].Name);
            Assert.IsFalse(model.IsValid, "Le joueur porte le nom d'une IA");
        }

        [Test]
        public void NameErrors_AreReportedOnce_NotDuplicated()
        {
            var model = new MatchSetupModel();
            model.SetRosterName(0, "");
            Assert.AreEqual(1, model.Validate().Count(e => e.Key == "validation.name_required"));
        }

        [Test]
        public void RoundCount_IsClampedBetween1And999()
        {
            var model = new MatchSetupModel();
            model.SetRoundCount(0);
            Assert.AreEqual(1, model.RoundCount);
            model.SetRoundCount(5000);
            Assert.AreEqual(999, model.RoundCount);
            model.SetRoundCount(10);
            model.AdjustRoundCount(-1);
            Assert.AreEqual(9, model.RoundCount);
            model.AdjustRoundCount(-100);
            Assert.AreEqual(1, model.RoundCount);
            Assert.IsTrue(model.IsValid);
        }

        [Test]
        public void Powers_CanBeToggled_WithoutAnyLimit_UpToTheWholeCatalog()
        {
            var model = new MatchSetupModel();
            foreach (var power in PowerCatalog.All)
                if (!model.IsPowerActive(power)) model.TogglePower(power);

            Assert.AreEqual(PowerCatalog.All.Length, model.ActivePowerCount, "Les 9 pouvoirs sont actifs");
            Assert.AreEqual(model.AvailablePowerCount, model.ActivePowerCount);
            Assert.AreEqual(model.ActivePowerCount, model.ActivePowers.Select(p => p.Rank).Distinct().Count(),
                "Chaque pouvoir a sa propre valeur");
            Assert.IsTrue(model.IsValid);

            model.TogglePower(PowerType.Skip);
            Assert.IsFalse(model.IsPowerActive(PowerType.Skip));
            Assert.AreEqual(8, model.ActivePowerCount);
            model.TogglePower(PowerType.Skip);
            Assert.IsTrue(model.IsPowerActive(PowerType.Skip));
            Assert.IsTrue(model.IsValid);
        }

        [Test]
        public void Powers_AllActive_StillAllowStepsAndAFullGame()
        {
            var model = new MatchSetupModel("Max", GameMode.Classic, 3, 1);
            foreach (var power in PowerCatalog.All)
                if (!model.IsPowerActive(power)) model.TogglePower(power);

            Assert.IsTrue(model.StepPowerRank(PowerType.Skip, +1), "Il reste des valeurs libres");
            Assert.IsTrue(model.IsValid);

            var match = model.CreateMatch(5);
            var game = match.StartNextRound();
            Assert.AreEqual(9, game.Rules.Powers.Count);
        }

        [Test]
        public void Powers_Can_AlsoBeAllDisabled()
        {
            var model = new MatchSetupModel();
            model.TogglePower(PowerType.Reset);
            model.TogglePower(PowerType.Burn);
            Assert.AreEqual(0, model.ActivePowerCount);
            Assert.IsTrue(model.IsValid);
        }

        [Test]
        public void ActivatedPower_GetsDefaultRank_OrNearestFreeOne()
        {
            var model = new MatchSetupModel();
            model.TogglePower(PowerType.Skip);
            Assert.AreEqual(8, model.RankOf(PowerType.Skip));

            // « Plus bas » et « même valeur ou tête » visent tous deux le 7 : le second prend la valeur libre voisine.
            var sevens = new MatchSetupModel();
            sevens.TogglePower(PowerType.LowerOrEqual);
            sevens.TogglePower(PowerType.SameOrFace);
            Assert.AreEqual(7, sevens.RankOf(PowerType.LowerOrEqual));
            Assert.AreEqual(8, sevens.RankOf(PowerType.SameOrFace));
            Assert.IsTrue(sevens.IsValid);
        }

        [Test]
        public void StepPowerRank_SkipsTakenRanks_AndWraps()
        {
            var model = new MatchSetupModel();           // 2 = remise à zéro, 10 = brûlure
            model.TogglePower(PowerType.Skip);           // 8
            Assert.IsTrue(model.StepPowerRank(PowerType.Skip, +1));
            Assert.AreEqual(9, model.RankOf(PowerType.Skip));
            Assert.IsTrue(model.StepPowerRank(PowerType.Skip, +1));
            Assert.AreEqual(11, model.RankOf(PowerType.Skip), "Le 10 est pris par la brûlure");

            model.StepPowerRank(PowerType.Skip, +1);
            model.StepPowerRank(PowerType.Skip, +1);
            model.StepPowerRank(PowerType.Skip, +1);
            Assert.AreEqual(14, model.RankOf(PowerType.Skip));
            model.StepPowerRank(PowerType.Skip, +1);
            Assert.AreEqual(3, model.RankOf(PowerType.Skip), "Boucle : après l'As, le 3 (le 2 est pris)");

            model.StepPowerRank(PowerType.Skip, -1);
            Assert.AreEqual(14, model.RankOf(PowerType.Skip));
            Assert.IsFalse(model.StepPowerRank(PowerType.Reverse, +1), "Pouvoir inactif");
            Assert.IsTrue(model.IsValid);
        }

        [Test]
        public void LaunchedMatch_UsesTheModelsChoices()
        {
            var model = new MatchSetupModel("Max", GameMode.Demon, 6, 2);
            model.JumpIn = true;
            model.AllowInitialSwap = false;
            model.FourOfAKindBurns = false;
            model.TogglePower(PowerType.Skip);

            var match = model.CreateMatch(42, new StatsBook());
            Assert.AreEqual(6, match.Players.Count);
            Assert.AreEqual("Max", match.Players[0].Name);
            Assert.AreEqual(2, match.Settings.RoundCount);
            Assert.IsTrue(match.Settings.Rules.JumpIn);
            Assert.IsFalse(match.Settings.Rules.AllowInitialSwap);
            Assert.IsFalse(match.Settings.Rules.FourOfAKindBurns);

            var game = match.StartNextRound();
            Assert.AreEqual(GamePhase.Play, game.Phase, "Échange désactivé : démarre directement");
            Assert.AreEqual(2, game.DeckCount, "6 joueurs x 9 cartes + pioche minimale");
        }

        [Test]
        public void BuildSettings_IsAnIndependentCopy()
        {
            var model = new MatchSetupModel("Max", GameMode.Demon, 4, 7);
            model.TogglePower(PowerType.Skip);
            model.JumpIn = true;

            var copy = model.BuildSettings();
            model.SetRosterName(0, "Changé");
            model.TogglePower(PowerType.Reverse);
            model.JumpIn = false;
            model.SetRoundCount(1);

            Assert.AreNotEqual("Changé", copy.Opponents[0].Name);
            Assert.AreEqual(3, copy.Rules.Powers.Count);
            Assert.IsTrue(copy.Rules.JumpIn);
            Assert.AreEqual(7, copy.RoundCount);
            Assert.IsEmpty(copy.Validate());
        }

        [Test]
        public void CreateMatch_ThrowsWhenSettingsAreInvalid()
        {
            var model = new MatchSetupModel();
            model.SetRosterName(0, "");
            Assert.IsFalse(model.IsValid);
            Assert.Throws<System.ArgumentException>(() => model.CreateMatch(1));
        }

        // ---------------------------------------------------------------- Préférences enregistrées

        [Test]
        public void Preferences_RoundTrip_KeepEverythingTheUserChose()
        {
            var model = new MatchSetupModel("Max", GameMode.Demon, 8, 25);
            model.Language = "en";
            model.SetRosterName(0, "Gargouille");
            model.SetRosterDifficulty(0, AiDifficulty.Hard);
            model.SetRosterName(8, "Dernier");
            model.TogglePower(PowerType.Skip);
            model.StepPowerRank(PowerType.Skip, +1);
            model.JumpIn = true;
            model.AllowInitialSwap = false;
            model.FourOfAKindBurns = false;

            var restored = MatchSetupModel.FromPreferences(model.ToPreferences(), "Autre");

            Assert.AreEqual("Max", restored.HumanName, "Le nom saisi est conservé");
            Assert.AreEqual("en", restored.Language);
            Assert.AreEqual(GameMode.Demon, restored.Mode);
            Assert.AreEqual(8, restored.PlayerCount);
            Assert.AreEqual(25, restored.RoundCount);
            Assert.AreEqual("Gargouille", restored.Roster[0].Name);
            Assert.AreEqual(AiDifficulty.Hard, restored.Roster[0].Difficulty);
            Assert.AreEqual("Dernier", restored.Roster[8].Name);
            Assert.AreEqual(9, restored.RankOf(PowerType.Skip));
            Assert.IsTrue(restored.JumpIn);
            Assert.IsFalse(restored.AllowInitialSwap);
            Assert.IsFalse(restored.FourOfAKindBurns);
            Assert.AreEqual(model.ActivePowerCount, restored.ActivePowerCount);
            Assert.IsTrue(restored.IsValid);
        }

        [Test]
        public void Preferences_ZeroPowers_IsAChoice_NotAMissingValue()
        {
            var model = new MatchSetupModel();
            model.TogglePower(PowerType.Reset);
            model.TogglePower(PowerType.Burn);
            Assert.AreEqual(0, model.ActivePowerCount);

            var restored = MatchSetupModel.FromPreferences(model.ToPreferences());
            Assert.AreEqual(0, restored.ActivePowerCount);

            var legacy = new SetupPreferences { HasPowers = false };
            Assert.AreEqual(2, MatchSetupModel.FromPreferences(legacy).ActivePowerCount, "Absent : valeurs par défaut");
        }

        [Test]
        public void Volumes_HaveDefaults_AreClamped_AndAreSavedWithThePreferences()
        {
            var model = new MatchSetupModel();
            Assert.AreEqual(MatchSetupModel.DefaultMusicVolume, model.MusicVolume);
            Assert.AreEqual(MatchSetupModel.DefaultEffectsVolume, model.EffectsVolume);

            model.SetMusicVolume(2.5f);
            model.SetEffectsVolume(-1f);
            Assert.AreEqual(1f, model.MusicVolume);
            Assert.AreEqual(0f, model.EffectsVolume);

            model.SetMusicVolume(0.3f);
            model.SetEffectsVolume(0.65f);
            var restored = MatchSetupModel.FromPreferences(model.ToPreferences());
            Assert.AreEqual(0.3f, restored.MusicVolume, 1e-6f);
            Assert.AreEqual(0.65f, restored.EffectsVolume, 1e-6f);
        }

        [Test]
        public void Volumes_AbsurdValues_FallBackToDefaults_AndOldFilesKeepTheDefaults()
        {
            var prefs = new SetupPreferences { MusicVolume = float.NaN, EffectsVolume = float.PositiveInfinity };
            var model = MatchSetupModel.FromPreferences(prefs);
            Assert.AreEqual(MatchSetupModel.DefaultMusicVolume, model.MusicVolume);
            Assert.AreEqual(MatchSetupModel.DefaultEffectsVolume, model.EffectsVolume);

            // Un fichier d'avant le son n'a pas ces champs : les valeurs d'origine de la classe s'appliquent.
            var untouched = MatchSetupModel.FromPreferences(new SetupPreferences());
            Assert.AreEqual(MatchSetupModel.DefaultMusicVolume, untouched.MusicVolume);
            Assert.AreEqual(MatchSetupModel.DefaultEffectsVolume, untouched.EffectsVolume);
        }

        [Test]
        public void Preferences_NullOrEmpty_FallBackToDefaults()
        {
            var fromNull = MatchSetupModel.FromPreferences(null, "Vous");
            Assert.AreEqual("Vous", fromNull.HumanName);
            Assert.IsTrue(fromNull.IsValid);

            var fromEmpty = MatchSetupModel.FromPreferences(new SetupPreferences(), "You");
            Assert.AreEqual("You", fromEmpty.HumanName, "Nom vide : le nom par défaut de la langue");
            Assert.AreEqual(3, fromEmpty.PlayerCount);
            Assert.IsTrue(fromEmpty.IsValid);
        }

        [Test]
        public void Preferences_InvalidValues_AreSanitized()
        {
            var prefs = new SetupPreferences
            {
                HumanName = new string('x', 80),
                Mode = 99,
                PlayerCount = 50,
                RoundCount = 123456,
                HasPowers = true,
                Roster = new List<AiPreference>
                {
                    null,
                    new AiPreference { Name = "  Baal  ", Difficulty = 42 },
                    new AiPreference { Name = "" }
                },
                Powers = new List<PowerPreference>
                {
                    null,
                    new PowerPreference { Power = 99, Rank = 5 },                       // pouvoir inconnu
                    new PowerPreference { Power = (int)PowerType.Skip, Rank = 99 },     // valeur invalide
                    new PowerPreference { Power = (int)PowerType.Skip, Rank = 8 },      // valide
                    new PowerPreference { Power = (int)PowerType.Skip, Rank = 9 },      // pouvoir en double
                    new PowerPreference { Power = (int)PowerType.Reverse, Rank = 8 },   // valeur en double
                    new PowerPreference { Power = (int)PowerType.Reverse, Rank = 9 },
                    new PowerPreference { Power = (int)PowerType.Burn, Rank = 10 },
                    new PowerPreference { Power = (int)PowerType.Reset, Rank = 2 },
                    new PowerPreference { Power = (int)PowerType.PlayAgain, Rank = 5 }  // valide : plus de limite
                }
            };

            var model = MatchSetupModel.FromPreferences(prefs);

            Assert.AreEqual(MatchSettings.MaxNameLength, model.HumanName.Length);
            Assert.AreEqual(GameMode.Classic, model.Mode);
            Assert.AreEqual(5, model.PlayerCount);
            Assert.AreEqual(999, model.RoundCount);
            Assert.AreEqual("Baal", model.Roster[1].Name);
            Assert.AreEqual(AiDifficulty.Normal, model.Roster[1].Difficulty, "Difficulté inconnue : normale");
            Assert.AreEqual(MatchSetupModel.RosterSize, model.Roster.Count);
            Assert.AreEqual(5, model.ActivePowerCount);
            CollectionAssert.AreEquivalent(
                new[] { PowerType.Skip, PowerType.Reverse, PowerType.Burn, PowerType.Reset, PowerType.PlayAgain },
                model.ActivePowers.Select(p => p.Power).ToArray());
            Assert.IsEmpty(new RulesConfig { Powers = model.ActivePowers.ToList() }.Validate());
        }
    }
}

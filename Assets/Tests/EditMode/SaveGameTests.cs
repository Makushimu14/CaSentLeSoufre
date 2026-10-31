using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaSentLeSoufre.Core;
using CaSentLeSoufre.Core.Ai;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class SaveGameTests
    {
        private static List<Player> NewPlayers(int count, int seed) =>
            Enumerable.Range(0, count)
                .Select(i => new Player("P" + i, i == 0, (AiDifficulty)((i + seed) % 3))).ToList();

        /// <summary>Description textuelle complète d'une manche, pour comparer deux états carte par carte.</summary>
        private static string Describe(GameData d)
        {
            string Cards(List<CardData> list) => string.Join(",", list.Select(c => c.Rank + ":" + c.Suit));
            var text = $"phase={d.Phase};cur={d.CurrentPlayerIndex};burned={d.BurnedCount};dir={d.Direction};" +
                       $"decks={d.DeckCount};loser={d.LoserIndex};stale={d.IsStalemate};" +
                       $"order={string.Join(",", d.FinishOrder)};ready={string.Join(",", d.Ready)};" +
                       $"draw=[{Cards(d.DrawPile)}];discard=[{Cards(d.DiscardPile)}]";
            for (int i = 0; i < d.Players.Count; i++)
            {
                var p = d.Players[i];
                text += $";p{i}:h=[{Cards(p.Hand)}]u=[{Cards(p.FaceUp)}]d=[{Cards(p.FaceDown)}]k=[{Cards(p.KnownInHand)}]";
            }
            return text;
        }

        private static Game PlayMoves(Game game, Random rng, int moves)
        {
            for (int i = 0; i < moves && game.Phase == GamePhase.Play; i++)
            {
                for (int j = 0; j < game.Players.Count && game.Phase == GamePhase.Play; j++)
                    AiBrain.TryJumpIn(game, j, rng);
                if (game.Phase == GamePhase.Play) AiBrain.TakeTurn(game, rng);
            }
            return game;
        }

        private static void PlayToEnd(Game game, Random rng) => PlayMoves(game, rng, 100000);

        // ---------------------------------------------------------------- Une manche

        [Test]
        public void Game_RoundTrip_GivesTheSameState()
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var rules = new RulesConfig { JumpIn = seed % 2 == 0, Mode = seed % 3 == 0 ? GameMode.Demon : GameMode.Classic };
                int playerCount = 3 + seed % 4;
                if (rules.Mode == GameMode.Classic) playerCount = Math.Min(5, playerCount);
                var game = new Game(NewPlayers(playerCount, seed), rules, seed);
                var rng = new Random(seed);
                for (int i = 0; i < playerCount; i++) AiBrain.DoSwap(game, i, rng);
                PlayMoves(game, rng, 25 + seed * 7);

                var data = game.ToData();
                var restored = Game.FromData(NewPlayers(playerCount, seed), rules, data);

                Assert.AreEqual(Describe(data), Describe(restored.ToData()), $"graine {seed}");
                Assert.AreEqual(game.Phase, restored.Phase);
                Assert.AreEqual(game.CurrentPlayerIndex, restored.CurrentPlayerIndex);
            }
        }

        [Test]
        public void ResumedGame_PlaysOutExactlyLikeTheOriginal()
        {
            int compared = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                var rules = new RulesConfig { JumpIn = true, FourOfAKindBurns = seed % 2 == 0 };
                rules.Powers.Add(new PowerSlot(PowerType.Skip));
                var original = new Game(NewPlayers(4, seed), rules, seed);
                var rng = new Random(seed);
                for (int i = 0; i < 4; i++) AiBrain.DoSwap(original, i, rng);
                PlayMoves(original, rng, 30);
                if (original.Phase != GamePhase.Play) continue;

                var resumed = Game.FromData(NewPlayers(4, seed), rules, original.ToData());

                // Même suite de décisions aléatoires de part et d'autre : les deux parties doivent finir pareil.
                PlayToEnd(original, new Random(1000 + seed));
                PlayToEnd(resumed, new Random(1000 + seed));

                Assert.AreEqual(Describe(original.ToData()), Describe(resumed.ToData()), $"graine {seed}");
                CollectionAssert.AreEqual(original.FinishOrder, resumed.FinishOrder);
                Assert.AreEqual(original.LoserIndex, resumed.LoserIndex);
                compared++;
            }

            Assert.GreaterOrEqual(compared, 8, "La comparaison doit vraiment avoir eu lieu sur la plupart des parties");
        }

        [Test]
        public void SwapPhase_IsSaved_WithTheReadyFlags()
        {
            var game = new Game(NewPlayers(3, 4), new RulesConfig(), 4);
            var rng = new Random(4);
            AiBrain.DoSwap(game, 1, rng);
            var hand = game.Players[0].Hand[0];
            var up = game.Players[0].FaceUp[0];
            Assert.IsTrue(game.SwapCards(0, hand, up));
            game.SetReady(0);

            var restored = Game.FromData(NewPlayers(3, 4), new RulesConfig(), game.ToData());

            Assert.AreEqual(GamePhase.Swap, restored.Phase);
            Assert.IsTrue(restored.IsReady(0));
            Assert.IsTrue(restored.IsReady(1));
            Assert.IsFalse(restored.IsReady(2));
            CollectionAssert.Contains(restored.Players[0].KnownInHand, up, "La connaissance suit la sauvegarde");

            AiBrain.DoSwap(restored, 2, rng);
            Assert.AreEqual(GamePhase.Play, restored.Phase, "La manche démarre quand le dernier est prêt");
        }

        // ---------------------------------------------------------------- Une partie entière

        private static MatchSettings DemonSettings() => MatchSettings.CreateDefault(GameMode.Demon, 5, 4);

        private static void PlayRound(Match match, Random rng)
        {
            var game = match.StartNextRound();
            for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
            PlayToEnd(game, rng);
            match.CompleteRound();
        }

        [Test]
        public void Match_BetweenRounds_RestoresScoresSlotsAndTheNextDeal()
        {
            var original = new Match(DemonSettings(), 77);
            PlayRound(original, new Random(1));

            var restored = Match.FromData(original.ToData());

            Assert.AreEqual(original.RoundNumber, restored.RoundNumber);
            Assert.AreEqual(original.RoundsCompleted, restored.RoundsCompleted);
            CollectionAssert.AreEqual(original.GetStandings().Select(s => (s.Name, s.Points, s.ShitheadCount)).ToList(),
                restored.GetStandings().Select(s => (s.Name, s.Points, s.ShitheadCount)).ToList());
            for (int i = 0; i < original.Players.Count; i++)
            {
                Assert.AreEqual(original.Players[i].FaceDownSlots, restored.Players[i].FaceDownSlots, "cartes cachées");
                Assert.AreEqual(original.Players[i].FaceUpSlots, restored.Players[i].FaceUpSlots, "cartes visibles");
            }

            // La manche suivante est distribuée exactement pareil : la graine a été rejouée.
            var nextOriginal = original.StartNextRound();
            var nextRestored = restored.StartNextRound();
            Assert.AreEqual(Describe(nextOriginal.ToData()), Describe(nextRestored.ToData()));
        }

        [Test]
        public void Match_MidRound_ResumesAndFinishesTheSameWay()
        {
            var original = new Match(DemonSettings(), 31);
            PlayRound(original, new Random(2));

            var game = original.StartNextRound();
            var rng = new Random(3);
            for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
            PlayMoves(game, rng, 40);
            Assert.AreEqual(GamePhase.Play, game.Phase);

            var restored = Match.FromData(original.ToData());
            Assert.AreEqual(Describe(game.ToData()), Describe(restored.CurrentGame.ToData()));

            PlayToEnd(game, new Random(9));
            PlayToEnd(restored.CurrentGame, new Random(9));
            original.CompleteRound();
            restored.CompleteRound();

            CollectionAssert.AreEqual(original.GetStandings().Select(s => (s.Name, s.Points)).ToList(),
                restored.GetStandings().Select(s => (s.Name, s.Points)).ToList());
            CollectionAssert.AreEqual(original.Players.Select(p => p.FaceDownSlots).ToList(),
                restored.Players.Select(p => p.FaceDownSlots).ToList());
        }

        [Test]
        public void ResumedMatch_KeepsFeedingTheStatistics()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 3, 2);
            var first = new Match(settings, 5, new StatsBook());
            PlayRound(first, new Random(4));

            var book = new StatsBook();
            var resumed = Match.FromData(first.ToData(), book);
            PlayRound(resumed, new Random(5));

            Assert.IsTrue(resumed.IsOver);
            Assert.IsTrue(book.Players.All(p => p.Classic.RoundsPlayed == 1), "Seule la manche jouée après la reprise est ajoutée");
            Assert.IsTrue(book.Players.All(p => p.Classic.MatchesPlayed == 1), "La partie se termine bien ici");
        }

        [Test]
        public void TableSession_ResumesMidRound_AndFinishesIt()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 4, 2), 12);
            var session = new TableSession(match, TableTimings.Instant(), 12);
            session.StartRound();
            session.HumanReady();
            for (int i = 0; i < 25 && !session.RoundOver; i++)
            {
                if (session.IsHumanTurn) PlayHuman(session);
                else session.Tick(0.1f);
            }
            Assert.IsFalse(session.RoundOver);

            var restored = Match.FromData(match.ToData());
            var resumed = TableSession.Resume(restored, TableTimings.Instant(), 99);

            Assert.IsFalse(resumed.RoundOver);
            int guard = 0;
            while (!resumed.RoundOver && guard++ < 30000)
            {
                if (resumed.IsHumanTurn) PlayHuman(resumed);
                else resumed.Tick(0.1f);
            }
            Assert.IsTrue(resumed.RoundOver);
            Assert.AreEqual(1, restored.RoundsCompleted);
        }

        private static void PlayHuman(TableSession session)
        {
            var game = session.Game;
            if (game.GetActiveZone(0) == Zone.FaceDown)
            {
                session.HumanPlayFaceDown(0);
                return;
            }
            var groups = game.GetPlayableCards(0).GroupBy(c => c.Rank).OrderBy(g => g.Key).ToList();
            if (groups.Count == 0) session.HumanPickUp();
            else session.HumanPlay(groups[0].ToList());
        }

        [Test]
        public void TableSession_ResumesAtTheResultsScreen_ThenStartsTheNextRound()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 3), 8);
            var session = new TableSession(match, TableTimings.Instant(), 8);
            session.StartRound();
            session.HumanReady();
            int guard = 0;
            while (!session.RoundOver && guard++ < 30000)
            {
                if (session.IsHumanTurn) PlayHuman(session);
                else session.Tick(0.1f);
            }
            Assert.IsTrue(session.RoundOver);

            var restored = Match.FromData(match.ToData());
            var resumed = TableSession.Resume(restored, TableTimings.Instant(), 1);

            Assert.IsTrue(resumed.RoundOver, "Les résultats sont à nouveau affichés");
            Assert.AreEqual(GamePhase.Finished, resumed.Game.Phase);
            Assert.AreEqual(match.Players.Count, resumed.Game.FinishOrder.Count);

            resumed.StartRound();
            Assert.AreEqual(2, restored.RoundNumber);
            Assert.AreEqual(GamePhase.Swap, resumed.Game.Phase);
        }

        // ---------------------------------------------------------------- Données abîmées

        private static MatchData MidRoundData()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 4, 2), 3);
            var game = match.StartNextRound();
            var rng = new Random(3);
            for (int i = 0; i < 4; i++) AiBrain.DoSwap(game, i, rng);
            PlayMoves(game, rng, 20);
            return match.ToData();
        }

        [Test]
        public void ValidData_Loads()
        {
            Assert.DoesNotThrow(() => Match.FromData(MidRoundData()));
        }

        [Test]
        public void MissingCard_IsRejected()
        {
            var data = MidRoundData();
            data.Game.Players[1].Hand.RemoveAt(0);
            Assert.Throws<InvalidOperationException>(() => Match.FromData(data));
        }

        [Test]
        public void DuplicatedOrInventedCard_IsRejected()
        {
            var data = MidRoundData();
            data.Game.DrawPile.Add(CardData.From(new Card(5, Suit.Hearts)));
            Assert.Throws<InvalidOperationException>(() => Match.FromData(data));
        }

        [Test]
        public void KnownCardNotInHand_IsRejected()
        {
            var data = MidRoundData();
            data.Game.Players[2].KnownInHand.Add(CardData.From(new Card(14, Suit.Clubs)));
            data.Game.Players[2].KnownInHand.Add(CardData.From(new Card(14, Suit.Clubs)));
            data.Game.Players[2].KnownInHand.Add(CardData.From(new Card(14, Suit.Clubs)));
            Assert.Throws<InvalidOperationException>(() => Match.FromData(data));
        }

        [Test]
        public void NonsenseValues_AreRejected()
        {
            var badRank = MidRoundData();
            badRank.Game.DrawPile[0].Rank = 99;
            Assert.Catch<Exception>(() => Match.FromData(badRank));

            var badPhase = MidRoundData();
            badPhase.Game.Phase = 42;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badPhase));

            var badPlayer = MidRoundData();
            badPlayer.Game.CurrentPlayerIndex = 17;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badPlayer));

            var badCount = MidRoundData();
            badCount.Game.Players.RemoveAt(3);
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badCount));

            var badVersion = MidRoundData();
            badVersion.Version = 99;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badVersion));

            var badScores = MidRoundData();
            badScores.Points.RemoveAt(0);
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badScores));

            var badRounds = MidRoundData();
            badRounds.RoundsStarted = 50;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badRounds));

            var badSlots = MidRoundData();
            badSlots.FaceDownSlots[0] = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => Match.FromData(badSlots));
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            var data = MidRoundData();
            data.Settings.Opponents.Clear(); // 1 seul joueur
            Assert.Throws<InvalidOperationException>(() => Match.FromData(data));

            var badMode = MidRoundData();
            badMode.Settings.Mode = 9;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(badMode));

            Assert.Throws<InvalidOperationException>(() => Match.FromData(null));
        }

        [Test]
        public void RoundInProgress_WithoutAGame_IsRejected()
        {
            var data = MidRoundData();
            data.HasGame = false;
            Assert.Throws<InvalidOperationException>(() => Match.FromData(data));
        }

        [Test]
        public void BeforeTheFirstRound_TheEmptyGameObjectIsIgnored()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 2), 6);
            var data = match.ToData();

            Assert.IsFalse(data.HasGame);
            Assert.IsNotNull(data.Game, "Le format JSON relit un objet absent comme un objet vide");

            var restored = Match.FromData(data);
            Assert.IsNull(restored.CurrentGame);
            Assert.DoesNotThrow(() => restored.StartNextRound());
        }

        // ---------------------------------------------------------------- Format JSON

        private static readonly Type[] DataTypes =
        {
            typeof(MatchData), typeof(GameData), typeof(SettingsData), typeof(PlayerCardsData), typeof(CardData),
            typeof(PowerPreference), typeof(AiPreference)
        };

        [Test]
        public void DataClasses_OnlyUseWhatJsonUtilityCanStore()
        {
            foreach (var type in DataTypes) AssertStorable(type, new HashSet<Type>());
        }

        private static void AssertStorable(Type type, HashSet<Type> visiting)
        {
            Assert.IsTrue(type.IsDefined(typeof(SerializableAttribute), false), $"{type.Name} doit être [Serializable]");
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), $"{type.Name} : constructeur sans paramètre requis");
            if (!visiting.Add(type)) return;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (field.IsStatic) continue; // les constantes ne sont pas enregistrées
                Assert.IsFalse(field.IsInitOnly, $"{type.Name}.{field.Name} ne doit pas être readonly");
                var t = field.FieldType;
                if (t == typeof(int) || t == typeof(bool) || t == typeof(string)) continue;
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var item = t.GetGenericArguments()[0];
                    if (item == typeof(int) || item == typeof(bool)) continue;
                    AssertStorable(item, visiting);
                    continue;
                }
                AssertStorable(t, visiting);
            }
        }

        [Test]
        public void Properties_ArePlainData_NoDictionariesOrArrays()
        {
            foreach (var type in DataTypes)
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsFalse(typeof(IDictionary).IsAssignableFrom(field.FieldType), $"{type.Name}.{field.Name}");
                    Assert.IsFalse(field.FieldType.IsArray, $"{type.Name}.{field.Name} : pas de tableau");
                }
        }
    }
}

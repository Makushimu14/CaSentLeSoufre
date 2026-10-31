using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using CaSentLeSoufre.Core.Ai;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class StatsTests
    {
        private static List<Player> Table(params (string name, bool human, AiDifficulty diff)[] who) =>
            who.Select(w => new Player(w.name, w.human, w.diff)).ToList();

        private static void PlayMatch(Match match, Random rng, bool stopAfterFirstRound = false)
        {
            while (!match.IsOver)
            {
                var game = match.StartNextRound();
                for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
                while (game.Phase == GamePhase.Play) AiBrain.TakeTurn(game, rng);
                match.CompleteRound();
                if (stopAfterFirstRound) return;
            }
        }

        [Test]
        public void RecordRound_CountsPlayedForAll_WinForFirst_LossForShithead()
        {
            var players = Table(("Max", true, AiDifficulty.Normal), ("Lilith", false, AiDifficulty.Hard),
                ("Baal", false, AiDifficulty.Easy));
            var book = new StatsBook();

            // Ordre de sortie : Baal, Max, Lilith (Shithead).
            book.RecordRound(players, new[] { 2, 0, 1 }, 1, GameMode.Classic);

            var max = book.Find("Max", true).Classic;
            var lilith = book.Find("Lilith", false, AiDifficulty.Hard).Classic;
            var baal = book.Find("Baal", false, AiDifficulty.Easy).Classic;

            Assert.AreEqual(1, max.RoundsPlayed);
            Assert.AreEqual(0, max.RoundWins);
            Assert.AreEqual(0, max.RoundLosses, "Deuxième : ni victoire ni défaite");
            Assert.AreEqual(1, baal.RoundWins);
            Assert.AreEqual(1, lilith.RoundLosses);
            Assert.AreEqual(1.0, baal.RoundWinRate);
            Assert.AreEqual(1.0, lilith.RoundLossRate);
            Assert.AreEqual(0.0, max.RoundWinRate);
        }

        [Test]
        public void Rates_AreZero_WhenNothingPlayed_AndAreFractions()
        {
            var line = new StatLine();
            Assert.AreEqual(0.0, line.RoundWinRate);
            Assert.AreEqual(0.0, line.MatchLossRate);

            line.RoundsPlayed = 8;
            line.RoundWins = 2;
            line.RoundLosses = 3;
            Assert.AreEqual(0.25, line.RoundWinRate, 1e-9);
            Assert.AreEqual(0.375, line.RoundLossRate, 1e-9);
        }

        [Test]
        public void Modes_AreTrackedSeparately_AndOverallIsTheSum()
        {
            var players = Table(("A", true, AiDifficulty.Normal), ("B", false, AiDifficulty.Normal),
                ("C", false, AiDifficulty.Normal));
            var book = new StatsBook();

            book.RecordRound(players, new[] { 0, 1, 2 }, 2, GameMode.Classic);
            book.RecordRound(players, new[] { 0, 2, 1 }, 1, GameMode.Classic);
            book.RecordRound(players, new[] { 1, 0, 2 }, 2, GameMode.Demon);

            var a = book.Find("A", true);
            Assert.AreEqual(2, a.Classic.RoundsPlayed);
            Assert.AreEqual(2, a.Classic.RoundWins);
            Assert.AreEqual(1, a.Demon.RoundsPlayed);
            Assert.AreEqual(0, a.Demon.RoundWins);

            var overall = a.Overall();
            Assert.AreEqual(3, overall.RoundsPlayed);
            Assert.AreEqual(2, overall.RoundWins);
            Assert.AreEqual(2.0 / 3.0, overall.RoundWinRate, 1e-9);
            Assert.AreEqual(a.Classic.RoundWins + a.Demon.RoundWins, overall.RoundWins);
        }

        [Test]
        public void Identity_SeparatesHumanFromAi_AndAiDifficulties_ButIgnoresCase()
        {
            var book = new StatsBook();
            var table1 = Table(("Lilith", true, AiDifficulty.Normal), ("Lilith", false, AiDifficulty.Easy),
                ("Lilith", false, AiDifficulty.Hard));
            book.RecordRound(table1, new[] { 0, 1, 2 }, 2, GameMode.Classic);
            Assert.AreEqual(3, book.Players.Count, "Humain, IA facile et IA difficile du même nom sont distincts");

            var table2 = Table(("LILITH", true, AiDifficulty.Normal), ("x", false, AiDifficulty.Normal),
                ("y", false, AiDifficulty.Normal));
            book.RecordRound(table2, new[] { 0, 1, 2 }, 2, GameMode.Classic);
            Assert.AreEqual(2, book.Find("lilith", true).Classic.RoundsPlayed, "La casse ne change pas l'identité");
            Assert.AreEqual(5, book.Players.Count);
        }

        [Test]
        public void Match_FeedsStats_Rounds_AndMatchResult()
        {
            var book = new StatsBook();
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 4, 3);
            var match = new Match(settings, 11, book);
            PlayMatch(match, new Random(11));

            Assert.AreEqual(4, book.Players.Count);
            Assert.IsTrue(book.Players.All(p => p.Classic.RoundsPlayed == 3));
            Assert.AreEqual(3, book.Players.Sum(p => p.Classic.RoundWins));
            Assert.AreEqual(3, book.Players.Sum(p => p.Classic.RoundLosses));
            Assert.IsTrue(book.Players.All(p => p.Classic.MatchesPlayed == 1));
            Assert.AreEqual(1, book.Players.Sum(p => p.Classic.MatchWins));
            Assert.AreEqual(1, book.Players.Sum(p => p.Classic.MatchLosses));
            Assert.IsTrue(book.Players.All(p => p.Demon.RoundsPlayed == 0), "Rien en mode démon");

            // Le vainqueur de la partie est le premier du classement.
            var winner = match.GetStandings()[0];
            Assert.AreEqual(1, book.Find(winner.Name, match.Players[winner.PlayerIndex].IsHuman,
                match.Players[winner.PlayerIndex].Difficulty).Classic.MatchWins);
        }

        [Test]
        public void Match_Demon_IsCountedInDemonOnly_AndAccumulatesOnSameBook()
        {
            var book = new StatsBook();
            PlayMatch(new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 2), 5, book), new Random(5));
            PlayMatch(new Match(MatchSettings.CreateDefault(GameMode.Demon, 3, 4), 6, book), new Random(6));

            Assert.AreEqual(3, book.Players.Count, "Mêmes noms, mêmes difficultés : mêmes joueurs");
            foreach (var p in book.Players)
            {
                Assert.AreEqual(2, p.Classic.RoundsPlayed);
                Assert.AreEqual(4, p.Demon.RoundsPlayed);
                Assert.AreEqual(1, p.Classic.MatchesPlayed);
                Assert.AreEqual(1, p.Demon.MatchesPlayed);
                Assert.AreEqual(6, p.Overall().RoundsPlayed);
                Assert.AreEqual(2, p.Overall().MatchesPlayed);
            }
        }

        [Test]
        public void AbandonedMatch_CountsFinishedRoundsButNotTheMatch()
        {
            var book = new StatsBook();
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 3), 8, book);
            PlayMatch(match, new Random(8), stopAfterFirstRound: true);

            Assert.IsTrue(book.Players.All(p => p.Classic.RoundsPlayed == 1));
            Assert.IsTrue(book.Players.All(p => p.Classic.MatchesPlayed == 0));
        }

        [Test]
        public void Match_WithoutStatsBook_StillWorks()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 1), 3);
            Assert.IsNull(match.Stats);
            Assert.DoesNotThrow(() => PlayMatch(match, new Random(3)));
        }

        [Test]
        public void Snapshot_RoundTrips_AndIsACopy()
        {
            var book = new StatsBook();
            PlayMatch(new Match(MatchSettings.CreateDefault(GameMode.Demon, 4, 2), 2, book), new Random(2));

            var snapshot = book.ToSnapshot();
            var restored = StatsBook.FromSnapshot(snapshot);

            Assert.AreEqual(book.Players.Count, restored.Players.Count);
            foreach (var original in book.Players)
            {
                var copy = restored.Find(original.Name, original.IsHuman, original.Difficulty);
                Assert.IsNotNull(copy, original.Name);
                Assert.AreEqual(original.Demon.RoundsPlayed, copy.Demon.RoundsPlayed);
                Assert.AreEqual(original.Demon.RoundWins, copy.Demon.RoundWins);
                Assert.AreEqual(original.Demon.MatchLosses, copy.Demon.MatchLosses);
            }

            snapshot.Players[0].Classic.RoundsPlayed = 99;
            Assert.AreEqual(0, book.Players[0].Classic.RoundsPlayed, "Le snapshot est une copie");
        }

        [Test]
        public void FromSnapshot_ToleratesNull_BlankNames_AndMergesDuplicates()
        {
            Assert.AreEqual(0, StatsBook.FromSnapshot(null).Players.Count);
            Assert.AreEqual(0, StatsBook.FromSnapshot(new StatsSnapshot { Players = null }).Players.Count);

            var snapshot = new StatsSnapshot();
            snapshot.Players.Add(null);
            snapshot.Players.Add(new PlayerStats { Name = "  " });
            snapshot.Players.Add(new PlayerStats { Name = "Max", IsHuman = true, Classic = new StatLine { RoundsPlayed = 2 } });
            snapshot.Players.Add(new PlayerStats { Name = "max", IsHuman = true, Classic = new StatLine { RoundsPlayed = 3 } });

            var book = StatsBook.FromSnapshot(snapshot);
            Assert.AreEqual(1, book.Players.Count);
            Assert.AreEqual(5, book.Find("Max", true).Classic.RoundsPlayed);
        }
    }
}

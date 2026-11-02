using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using CaSentLeSoufre.Core.Ai;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    /// <summary>Stratégie des IA difficiles et ordre des places à la table.</summary>
    public class AiStrategyAndSeatingTests
    {
        private static Card C(int rank, Suit suit = Suit.Spades) => new Card(rank, suit);

        /// <summary>A (IA difficile) joue en premier ; B a la main donnée, sans que personne ne la connaisse.</summary>
        private static Game Scenario(RulesConfig rules, IEnumerable<Card> handA, IEnumerable<Card> handB,
            IEnumerable<Card> knownB = null, IEnumerable<Card> discard = null)
        {
            var a = new Player("A", false, AiDifficulty.Hard);
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(handA);
            b.Hand.AddRange(handB);
            if (knownB != null) b.KnownInHand.AddRange(knownB);
            c.Hand.AddRange(new[] { C(13, Suit.Hearts), C(14, Suit.Hearts), C(12, Suit.Hearts) });
            return Game.FromSetup(new[] { a, b, c }, rules, new List<Card>(), discard?.ToList(), 0);
        }

        [Test]
        public void HardAi_GetsRidOfTheThreeFirst_EvenWhenItCarriesAPower()
        {
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 3));
            var game = Scenario(rules, new[] { C(14), C(3), C(9) }, new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) });

            var move = AiBrain.TakeTurn(game, new Random(1));

            Assert.IsTrue(move.Ok);
            Assert.AreEqual(3, move.Played[0].Rank, "Le 3 est la carte la plus difficile à poser : on la joue d'abord");
        }

        [Test]
        public void HardAi_KeepsTheTwoAndTheBurnForLater()
        {
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.Reset, 2), new PowerSlot(PowerType.Burn, 10));
            var game = Scenario(rules, new[] { C(2), C(10), C(8) }, new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) },
                discard: new[] { C(5) });

            var move = AiBrain.TakeTurn(game, new Random(1));

            Assert.AreEqual(8, move.Played[0].Rank);
        }

        [Test]
        public void HardAi_PlaysItsWeakestCard_WhenTheNextPlayerIsNotAboutToFinish()
        {
            // B a cinq cartes, toutes connues et toutes basses : un 9 le bloquerait, mais pour rien.
            var hand = new[]
            {
                C(5, Suit.Hearts), C(6, Suit.Hearts), C(7, Suit.Hearts), C(8, Suit.Hearts), C(4, Suit.Hearts)
            };
            var game = Scenario(new RulesConfig(), new[] { C(5), C(9), C(13) }, hand, hand, new[] { C(4) });

            var move = AiBrain.TakeTurn(game, new Random(1));

            Assert.AreEqual(5, move.Played[0].Rank, "Bloquer un joueur loin de sortir ne sert qu'à gaspiller une carte");
        }

        [Test]
        public void HardAi_StillBlocksTheNextPlayer_WhenHeIsAboutToFinish()
        {
            var hand = new[] { C(5, Suit.Hearts), C(6, Suit.Hearts) };
            var game = Scenario(new RulesConfig(), new[] { C(5), C(9), C(13) }, hand, hand, new[] { C(4) });

            var move = AiBrain.TakeTurn(game, new Random(1));

            Assert.AreEqual(9, move.Played[0].Rank, "Le suivant va gagner : on le force à ramasser");
        }

        [Test]
        public void EasyAi_ShortGamesStillEnd_WithoutTheStalemateGuard()
        {
            int stalemates = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var players = Enumerable.Range(0, 3).Select(i => new Player("P" + i, false, AiDifficulty.Easy)).ToList();
                var game = new Game(players, RulesConfig.Classic, seed);
                var rng = new Random(seed);
                for (int i = 0; i < 3; i++) AiBrain.DoSwap(game, i, rng);

                int moves = 0;
                while (game.Phase == GamePhase.Play && moves++ < 5000) AiBrain.TakeTurn(game, rng);
                Assert.Less(moves, 1500, $"Partie entre débutants trop longue (graine {seed})");
                if (game.IsStalemate) stalemates++;
            }
            Assert.LessOrEqual(stalemates, 2);
        }

        // ---------------------------------------------------------------- Places à la table

        private static MatchSettings Settings(int players)
        {
            var settings = MatchSettings.CreateDefault(GameMode.Demon, players, 3);
            settings.HumanName = "Maxime";
            return settings;
        }

        [Test]
        public void Seating_KeepsThePlayerFirst_AndEveryOpponentExactlyOnce()
        {
            var settings = Settings(8);
            var expected = settings.Opponents.Select(o => o.Name.Trim()).OrderBy(n => n).ToList();

            for (int seed = 0; seed < 30; seed++)
            {
                var match = new Match(settings, seed);
                Assert.IsTrue(match.Players[0].IsHuman);
                Assert.AreEqual("Maxime", match.Players[0].Name);
                CollectionAssert.AreEqual(expected, match.Players.Skip(1).Select(p => p.Name).OrderBy(n => n).ToList());
            }
        }

        [Test]
        public void Seating_DiffersFromOneMatchToTheNext_ButNotForTheSameSeed()
        {
            var settings = Settings(6);
            var orders = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
                orders.Add(string.Join(",", new Match(settings, seed).Players.Select(p => p.Name)));
            Assert.Greater(orders.Count, 5, "Les places doivent varier d'une partie à l'autre");

            string first = string.Join(",", new Match(settings, 12).Players.Select(p => p.Name));
            string second = string.Join(",", new Match(settings, 12).Players.Select(p => p.Name));
            Assert.AreEqual(first, second);
        }

        [Test]
        public void Seating_IsRestoredIdentically_WhenASavedGameIsResumed()
        {
            var settings = Settings(7);
            var match = new Match(settings, 99);
            match.StartNextRound();

            var restored = Match.FromData(match.ToData());

            CollectionAssert.AreEqual(match.Players.Select(p => p.Name).ToList(),
                restored.Players.Select(p => p.Name).ToList());
            CollectionAssert.AreEqual(match.Players.Select(p => p.Difficulty).ToList(),
                restored.Players.Select(p => p.Difficulty).ToList());
        }
    }
}

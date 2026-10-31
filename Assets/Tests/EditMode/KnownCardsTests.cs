using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using CaSentLeSoufre.Core.Ai;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class KnownCardsTests
    {
        private static Card C(int rank, Suit suit = Suit.Spades) => new Card(rank, suit);

        private static Game Scenario(AiDifficulty ai, IEnumerable<Card> handA, IEnumerable<Card> handB,
            IEnumerable<Card> knownB, IEnumerable<Card> faceUpB = null, IEnumerable<Card> discard = null,
            IEnumerable<Card> draw = null)
        {
            var a = new Player("A", false, ai);
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(handA);
            b.Hand.AddRange(handB);
            b.KnownInHand.AddRange(knownB);
            if (faceUpB != null) b.FaceUp.AddRange(faceUpB);
            c.Hand.AddRange(new[] { C(13, Suit.Hearts), C(14, Suit.Hearts), C(12, Suit.Hearts) });
            return Game.FromSetup(new[] { a, b, c }, new RulesConfig(), draw?.ToList() ?? new List<Card>(),
                discard?.ToList(), 0);
        }

        /// <summary>Chaque carte « connue » est bien dans la main (autant d'exemplaires que de fois connue).</summary>
        public static void AssertKnownIsConsistent(Game game)
        {
            foreach (var p in game.Players)
            {
                var inHand = p.Hand.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
                foreach (var known in p.KnownInHand.GroupBy(c => c))
                {
                    Assert.IsTrue(inHand.TryGetValue(known.Key, out int count) && count >= known.Count(),
                        $"{p.Name} : {known.Key} est connue mais n'est pas (assez) en main");
                }
            }
        }

        // ---------------------------------------------------------------- Suivi de la connaissance

        [Test]
        public void NewDeal_StartsWithNothingKnownInHand()
        {
            var game = new Game(new[] { new Player("A"), new Player("B"), new Player("C") }, new RulesConfig(), 5);
            foreach (var p in game.Players)
            {
                Assert.IsEmpty(p.KnownInHand);
                Assert.IsFalse(p.HandFullyKnown);
            }
        }

        [Test]
        public void Swap_MakesTheFormerFaceUpCardKnown_AndNotTheOneNowFaceUp()
        {
            var game = new Game(new[] { new Player("A"), new Player("B"), new Player("C") }, new RulesConfig(), 6);
            var a = game.Players[0];
            var handCard = a.Hand[0];
            var faceUpCard = a.FaceUp[0];

            Assert.IsTrue(game.SwapCards(0, handCard, faceUpCard));
            CollectionAssert.Contains(a.KnownInHand, faceUpCard, "Elle était visible de tous");
            CollectionAssert.DoesNotContain(a.KnownInHand, handCard, "Elle est maintenant devant le joueur, face visible");

            Assert.IsTrue(game.SwapCards(0, faceUpCard, handCard), "On remet les choses comme avant");
            CollectionAssert.Contains(a.KnownInHand, handCard);
            CollectionAssert.DoesNotContain(a.KnownInHand, faceUpCard);
            AssertKnownIsConsistent(game);
        }

        [Test]
        public void PickingUp_MakesEveryPileCardKnown_PlayingThemForgetsThem()
        {
            var game = Scenario(AiDifficulty.Normal, new[] { C(3), C(4) }, new[] { C(5, Suit.Hearts) },
                new Card[0], discard: new[] { C(14, Suit.Clubs), C(13, Suit.Clubs) });

            Assert.IsTrue(game.PickUpPile(0).Ok);
            var a = game.Players[0];
            Assert.AreEqual(4, a.Hand.Count);
            CollectionAssert.AreEquivalent(new[] { C(14, Suit.Clubs), C(13, Suit.Clubs) }, a.KnownInHand,
                "Seules les cartes ramassées sont connues, pas les 3 et 4 d'origine");
            Assert.IsFalse(a.HandFullyKnown);

            // Plus tard, il pose un roi connu : il disparaît de la connaissance.
            game.Players[1].Hand.Clear();
            game.Players[1].Hand.Add(C(3, Suit.Hearts));
            game.PickUpPile(1); // le joueur B ramasse (défausse vide : rien) pour rendre la main à A
            AdvanceUntil(game, 0);
            Assert.IsTrue(game.Play(0, new[] { C(13, Suit.Clubs) }).Ok);
            CollectionAssert.DoesNotContain(a.KnownInHand, C(13, Suit.Clubs));
            AssertKnownIsConsistent(game);
        }

        private static void AdvanceUntil(Game game, int player)
        {
            int guard = 0;
            while (game.CurrentPlayerIndex != player && guard++ < 10)
            {
                var cards = game.GetPlayableCards(game.CurrentPlayerIndex);
                if (cards.Count > 0) game.Play(game.CurrentPlayerIndex, new[] { cards[0] });
                else game.PickUpPile(game.CurrentPlayerIndex);
            }
        }

        [Test]
        public void DrawnCards_AreNeverKnown()
        {
            var game = Scenario(AiDifficulty.Normal, new[] { C(8), C(9), C(11) }, new[] { C(5, Suit.Hearts) },
                new Card[0], draw: new[] { C(3, Suit.Clubs), C(4, Suit.Clubs) });
            game.Players[0].KnownInHand.Add(C(9));

            Assert.IsTrue(game.Play(0, new[] { C(8) }).Ok);
            Assert.AreEqual(3, game.Players[0].Hand.Count, "Il a pioché");
            CollectionAssert.AreEqual(new[] { C(9) }, game.Players[0].KnownInHand, "La carte piochée reste inconnue");
        }

        [Test]
        public void ForcedPickUp_AndBlindFailure_AlsoRevealTheCardsToEveryone()
        {
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.ForcePickUp, 11));
            var a = new Player("A");
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(new[] { C(11), C(13) });
            b.Hand.AddRange(new[] { C(3), C(4) });
            c.Hand.Add(C(9));
            var game = Game.FromSetup(new[] { a, b, c }, rules, new List<Card>(), new List<Card> { C(5) }, 0);

            var result = game.Play(0, new[] { C(11) });
            Assert.AreEqual(1, result.ForcedPickUpPlayer);
            CollectionAssert.AreEquivalent(new[] { C(5), C(11) }, b.KnownInHand, "Le valet et le 5 sont vus de tous");

            // Une carte aveugle injouable : elle est révélée puis ramassée avec la défausse.
            var d = new Player("D");
            d.FaceDown.Add(C(4, Suit.Clubs));
            var e = new Player("E");
            e.Hand.Add(C(9, Suit.Hearts));
            var f = new Player("F");
            f.Hand.Add(C(9, Suit.Diamonds));
            var blind = Game.FromSetup(new[] { d, e, f }, new RulesConfig(), new List<Card>(),
                new List<Card> { C(13, Suit.Hearts) }, 0);
            Assert.IsTrue(blind.PlayFaceDown(0, 0).PickedUp);
            CollectionAssert.AreEquivalent(new[] { C(13, Suit.Hearts), C(4, Suit.Clubs) }, d.KnownInHand);
            Assert.IsTrue(d.HandFullyKnown);
        }

        [Test]
        public void PeekNextPlayer_FollowsTheDirection_AndSkipsFinishedPlayers()
        {
            var game = Scenario(AiDifficulty.Normal, new[] { C(8) }, new[] { C(9) }, new Card[0]);
            Assert.AreEqual(1, game.PeekNextPlayerIndex());
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Simple consultation : le tour ne bouge pas");
        }

        [Test]
        public void CanPlayRankOn_AnswersForAHypotheticalTopCard()
        {
            var game = Scenario(AiDifficulty.Normal, new[] { C(8) }, new[] { C(9) }, new Card[0]);
            Assert.IsTrue(game.CanPlayRankOn(null, 3));
            Assert.IsTrue(game.CanPlayRankOn(5, 6));
            Assert.IsFalse(game.CanPlayRankOn(9, 6));
            Assert.IsTrue(game.CanPlayRankOn(9, 2), "Le 2 est la carte la plus forte");
            Assert.IsTrue(game.CanPlayRankOn(Card.Ace, 10), "La brûlure est jouable partout");
        }

        // ---------------------------------------------------------------- IA difficile : contrer sans tricher

        private static Card TopAfterAiMove(Game game)
        {
            var result = AiBrain.TakeTurn(game, new Random(1));
            Assert.IsTrue(result.Ok);
            return result.Played.Single();
        }

        [Test]
        public void HardAi_PlaysTheLowestCardThatCertainlyBlocksTheNextPlayer()
        {
            // B garde 6 et 7, connus de tous : le 5 ne le bloquerait pas, le 9 oui.
            var game = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(13) },
                new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) }, new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) });

            Assert.AreEqual(C(9), TopAfterAiMove(game));
        }

        [Test]
        public void NormalAi_DoesNotUseThatKnowledge()
        {
            var game = Scenario(AiDifficulty.Normal, new[] { C(5), C(9), C(13) },
                new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) }, new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) });

            Assert.AreEqual(C(5), TopAfterAiMove(game));
        }

        [Test]
        public void HardAi_DoesNotCheat_WhenTheNextHandIsNotFullyKnown()
        {
            // B a les mêmes cartes mais personne ne les a vues : l'IA ne peut rien affirmer et joue sa plus faible.
            var unknown = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(13) },
                new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) }, new Card[0]);
            Assert.AreEqual(C(5), TopAfterAiMove(unknown));

            // Une seule des deux cartes est connue : toujours pas de certitude.
            var partial = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(13) },
                new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) }, new[] { C(6, Suit.Hearts) });
            Assert.AreEqual(C(5), TopAfterAiMove(partial));
        }

        [Test]
        public void HardAi_SeesTheNextPlayersFaceUpCards()
        {
            // B n'a plus de main : ses cartes visibles (6 et 7) sont connues de tous.
            var game = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(13) }, new Card[0], new Card[0],
                faceUpB: new[] { C(6, Suit.Hearts), C(7, Suit.Hearts) });
            game.Players[1].FaceDown.Add(C(3, Suit.Hearts));

            Assert.AreEqual(C(9), TopAfterAiMove(game));
        }

        [Test]
        public void HardAi_CannotBlockABlindPlayer_SoItFallsBackToTheLowestCard()
        {
            var game = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(13) }, new Card[0], new Card[0]);
            game.Players[1].FaceDown.Add(C(3, Suit.Hearts));

            Assert.AreEqual(C(5), TopAfterAiMove(game));
        }

        [Test]
        public void HardAi_DoesNotWasteAHighCard_WhenTheNextPlayerCouldAnswerAnyway()
        {
            // B connu pour avoir un roi : aucun coup de A ne le bloque, donc la carte la plus faible suffit.
            var game = Scenario(AiDifficulty.Hard, new[] { C(5), C(9), C(11) },
                new[] { C(13, Suit.Hearts), C(7, Suit.Hearts) }, new[] { C(13, Suit.Hearts), C(7, Suit.Hearts) });

            Assert.AreEqual(C(5), TopAfterAiMove(game));
        }

        // ---------------------------------------------------------------- Cohérence sur des parties entières

        [Test]
        public void KnownCards_StayConsistentWithHands_ThroughWholeAiGames()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                int playerCount = 3 + seed % 3;
                var players = Enumerable.Range(0, playerCount)
                    .Select(i => new Player("P" + i, false, (AiDifficulty)((i + seed) % 3))).ToList();
                var rules = new RulesConfig { JumpIn = seed % 2 == 0 };
                var game = new Game(players, rules, seed);
                var rng = new Random(seed);
                for (int i = 0; i < playerCount; i++) AiBrain.DoSwap(game, i, rng);
                AssertKnownIsConsistent(game);

                int moves = 0;
                while (game.Phase == GamePhase.Play && moves++ < 20000)
                {
                    if (rules.JumpIn)
                        for (int j = 0; j < playerCount && game.Phase == GamePhase.Play; j++)
                            AiBrain.TryJumpIn(game, j, rng);
                    if (game.Phase != GamePhase.Play) break;

                    AiBrain.TakeTurn(game, rng);
                    AssertKnownIsConsistent(game);
                }
            }
        }
    }
}

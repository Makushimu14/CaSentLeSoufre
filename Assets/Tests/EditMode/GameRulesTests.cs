using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using CaSentLeSoufre.Core.Ai;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class GameRulesTests
    {
        private static Card C(int rank, Suit suit = Suit.Spades) => new Card(rank, suit);

        /// <summary>Règles par défaut (2 = remise à zéro, 10 = brûlure) + pouvoirs supplémentaires à leur valeur traditionnelle.</summary>
        private static RulesConfig Rules(params PowerType[] extra)
        {
            var rules = new RulesConfig();
            foreach (var p in extra) rules.Powers.Add(new PowerSlot(p));
            return rules;
        }

        /// <summary>3 joueurs en phase de jeu : A (main donnée) joue en premier ; B et C ont de quoi tenir.</summary>
        private static Game Setup3(IEnumerable<Card> hand0, RulesConfig rules, IEnumerable<Card> discard = null)
        {
            var a = new Player("A");
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(hand0);
            b.Hand.AddRange(new[] { C(9, Suit.Hearts), C(12, Suit.Hearts), C(13, Suit.Hearts) });
            c.Hand.AddRange(new[] { C(9, Suit.Diamonds), C(12, Suit.Diamonds), C(13, Suit.Diamonds) });
            return Game.FromSetup(new[] { a, b, c }, rules, new List<Card>(), discard?.ToList(), 0);
        }

        /// <summary>Partie à 2 joueurs en phase de jeu, joueur 0 à la main.</summary>
        private static Game Setup(IEnumerable<Card> hand0, IEnumerable<Card> discard = null,
            RulesConfig rules = null, IEnumerable<Card> draw = null,
            IEnumerable<Card> up0 = null, IEnumerable<Card> down0 = null)
        {
            var p0 = new Player("A");
            var p1 = new Player("B");
            p0.Hand.AddRange(hand0);
            if (up0 != null) p0.FaceUp.AddRange(up0);
            if (down0 != null) p0.FaceDown.AddRange(down0);
            p1.Hand.Add(C(9, Suit.Hearts));
            p1.Hand.Add(C(12, Suit.Hearts));
            return Game.FromSetup(new[] { p0, p1 }, rules,
                draw?.ToList() ?? new List<Card>(), discard?.ToList(), 0);
        }

        [Test]
        public void NewGame_DealsNineCardsEach_AndLeavesRestInDrawPile()
        {
            var players = new[] { new Player("A"), new Player("B"), new Player("C") };
            var game = new Game(players, RulesConfig.Classic, 42);

            foreach (var p in game.Players)
            {
                Assert.AreEqual(3, p.Hand.Count);
                Assert.AreEqual(3, p.FaceUp.Count);
                Assert.AreEqual(3, p.FaceDown.Count);
            }
            Assert.AreEqual(52 - 27, game.DrawPile.Count);
            Assert.AreEqual(GamePhase.Swap, game.Phase);
        }

        [Test]
        public void Swap_ExchangesHandAndFaceUp_ThenAllReadyStartsPlay()
        {
            var players = new[] { new Player("A"), new Player("B"), new Player("C") };
            var game = new Game(players, RulesConfig.Classic, 1);
            var a = game.Players[0];
            var handCard = a.Hand[0];
            var upCard = a.FaceUp[0];

            Assert.IsTrue(game.SwapCards(0, handCard, upCard));
            Assert.AreEqual(upCard, a.Hand[0]);
            Assert.AreEqual(handCard, a.FaceUp[0]);

            game.SetReady(0);
            Assert.AreEqual(GamePhase.Swap, game.Phase);
            Assert.IsFalse(game.SwapCards(0, a.Hand[0], a.FaceUp[0]), "Plus d'échange une fois prêt");
            game.SetReady(1);
            Assert.AreEqual(GamePhase.Swap, game.Phase);
            game.SetReady(2);
            Assert.AreEqual(GamePhase.Play, game.Phase);
        }

        [Test]
        public void StartingPlayer_HoldsLowestOrdinaryCard()
        {
            // Vraie partie (phase d'échange), puis mains remplacées pour maîtriser le scénario.
            var game = new Game(new[] { new Player("A"), new Player("B"), new Player("C") }, RulesConfig.Classic, 3);
            var a = game.Players[0];
            var b = game.Players[1];
            var c = game.Players[2];
            a.Hand.Clear(); a.Hand.AddRange(new[] { C(2), C(8), C(13) });
            b.Hand.Clear(); b.Hand.AddRange(new[] { C(5), C(10), C(14) });
            c.Hand.Clear(); c.Hand.AddRange(new[] { C(9), C(11), C(12) });
            game.SetReady(0);
            game.SetReady(1);
            game.SetReady(2);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "B possède le 5, plus petit que le 8 de A (le 2 est spécial)");
        }

        [Test]
        public void CannotPlayLowerThanTopCard_ButCanPlayEqualOrHigher()
        {
            var game = Setup(new[] { C(5), C(8, Suit.Hearts) }, new[] { C(7) });
            Assert.AreEqual(MoveError.NotPlayable, game.Play(0, new[] { C(5) }).Error);
            Assert.IsTrue(game.Play(0, new[] { C(8, Suit.Hearts) }).Ok);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void Two_AlwaysPlayable_AndResets()
        {
            var game = Setup(new[] { C(2), C(3) }, new[] { C(14) });
            Assert.IsTrue(game.Play(0, new[] { C(2) }).Ok);
            Assert.IsTrue(game.CanPlayRank(3), "Après un 2, tout est jouable");
        }

        [Test]
        public void Ten_BurnsPile_AndSamePlayerPlaysAgain()
        {
            var game = Setup(new[] { C(10), C(4) }, new[] { C(9), C(11) });
            var r = game.Play(0, new[] { C(10) });
            Assert.IsTrue(r.Burned);
            Assert.AreEqual(0, game.DiscardPile.Count);
            Assert.AreEqual(0, game.CurrentPlayerIndex);
            Assert.AreEqual(3, game.BurnedCount);
        }

        [Test]
        public void FourOfAKind_Burns_AcrossTurns()
        {
            var game = Setup(new[] { C(7, Suit.Hearts), C(7, Suit.Clubs), C(9) },
                new[] { C(7, Suit.Diamonds), C(7, Suit.Spades) });
            var r = game.Play(0, new[] { C(7, Suit.Hearts), C(7, Suit.Clubs) });
            Assert.IsTrue(r.Burned);
            Assert.AreEqual(0, game.DiscardPile.Count);
        }

        [Test]
        public void FourOfAKind_DoesNotBurn_WhenVariantDisabled()
        {
            var rules = new RulesConfig { FourOfAKindBurns = false };
            var game = Setup(new[] { C(7, Suit.Hearts), C(7, Suit.Clubs), C(9) },
                new[] { C(7, Suit.Diamonds), C(7, Suit.Spades) }, rules);
            var r = game.Play(0, new[] { C(7, Suit.Hearts), C(7, Suit.Clubs) });
            Assert.IsFalse(r.Burned);
            Assert.AreEqual(4, game.DiscardPile.Count);
        }

        [Test]
        public void MultipleCards_MustShareRank()
        {
            var game = Setup(new[] { C(8), C(9, Suit.Hearts), C(8, Suit.Hearts) });
            Assert.AreEqual(MoveError.InvalidCards, game.Play(0, new[] { C(8), C(9, Suit.Hearts) }).Error);
            Assert.IsTrue(game.Play(0, new[] { C(8), C(8, Suit.Hearts) }).Ok);
        }

        [Test]
        public void CannotPlayCardsNotInHand()
        {
            var game = Setup(new[] { C(8) });
            Assert.AreEqual(MoveError.InvalidCards, game.Play(0, new[] { C(9) }).Error);
        }

        [Test]
        public void NotYourTurn_IsRejected()
        {
            var game = Setup(new[] { C(8) });
            Assert.AreEqual(MoveError.NotYourTurn, game.Play(1, new[] { C(9, Suit.Hearts) }).Error);
        }

        [Test]
        public void PlayerRefillsHandToThree_WhileDrawPileLasts()
        {
            var game = Setup(new[] { C(8), C(9), C(11) }, null, null,
                new[] { C(3), C(4), C(5), C(6) });
            game.Play(0, new[] { C(8) });
            Assert.AreEqual(3, game.Players[0].Hand.Count);
            Assert.AreEqual(3, game.DrawPile.Count);
        }

        [Test]
        public void PickUp_OnlyAllowedWhenNoLegalPlay()
        {
            var game = Setup(new[] { C(4), C(5) }, new[] { C(9), C(11) });
            var r = game.PickUpPile(0);
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.PickedUp);
            Assert.AreEqual(4, game.Players[0].Hand.Count);
            Assert.AreEqual(0, game.DiscardPile.Count);
            Assert.AreEqual(1, game.CurrentPlayerIndex);

            var game2 = Setup(new[] { C(12), C(5) }, new[] { C(9) });
            Assert.AreEqual(MoveError.MustPlay, game2.PickUpPile(0).Error);
        }

        [Test]
        public void ThreeTransparent_LooksUnderTheThree()
        {
            var rules = Rules(PowerType.Transparent);
            var game = Setup(new[] { C(6), C(9) }, new[] { C(8), C(3) }, rules);
            Assert.AreEqual(8, game.RankToBeat());
            Assert.IsFalse(game.CanPlayRank(6));
            Assert.IsTrue(game.CanPlayRank(3));
        }

        [Test]
        public void SevenLower_ForcesSevenOrLess()
        {
            var rules = Rules(PowerType.LowerOrEqual);
            var game = Setup(new[] { C(5), C(9) }, new[] { C(7) }, rules);
            Assert.IsTrue(game.CanPlayRank(5));
            Assert.IsFalse(game.CanPlayRank(9));
            Assert.IsTrue(game.CanPlayRank(7));
            Assert.IsTrue(game.CanPlayRank(2));
            Assert.IsTrue(game.CanPlayRank(10));
        }

        [Test]
        public void PlayingFaceUp_OnlyAfterHandIsEmpty()
        {
            var game = Setup(new[] { C(8) }, null, null, null, new[] { C(9), C(12) });
            Assert.AreEqual(Zone.Hand, game.GetActiveZone(0));
            Assert.AreEqual(MoveError.InvalidCards, game.Play(0, new[] { C(9) }).Error);
            game.Play(0, new[] { C(8) });
            // Tour de B puis retour sur A : sa main est vide.
            game.PickUpPile(1);
            Assert.AreEqual(Zone.FaceUp, game.GetActiveZone(0));
        }

        [Test]
        public void FaceDown_UnplayableCardIsPickedUpWithPile()
        {
            var game = Setup(new List<Card>(), new[] { C(13) }, null, null, null, new[] { C(4) });
            Assert.AreEqual(Zone.FaceDown, game.GetActiveZone(0));
            var r = game.PlayFaceDown(0, 0);
            Assert.IsTrue(r.PickedUp);
            Assert.AreEqual(C(4), r.Revealed);
            Assert.AreEqual(2, game.Players[0].Hand.Count);
            Assert.AreEqual(0, game.DiscardPile.Count);
        }

        [Test]
        public void FaceDown_PlayableCardGoesOnPile()
        {
            var game = Setup(new List<Card>(), new[] { C(5) }, null, null, null, new[] { C(9), C(4) });
            var r = game.PlayFaceDown(0, 0);
            Assert.IsFalse(r.PickedUp);
            Assert.AreEqual(2, game.DiscardPile.Count);
            Assert.AreEqual(1, game.Players[0].FaceDown.Count);
        }

        [Test]
        public void LastPlayerWithCards_IsTheLoser()
        {
            var game = Setup(new[] { C(13) });
            var r = game.Play(0, new[] { C(13) });
            Assert.IsTrue(r.PlayerFinished);
            Assert.AreEqual(GamePhase.Finished, game.Phase);
            Assert.AreEqual(1, game.LoserIndex);
            CollectionAssert.AreEqual(new[] { 0, 1 }, game.FinishOrder);
        }

        [Test]
        public void FinishingWithATen_DoesNotLeaveTurnOnFinishedPlayer()
        {
            var p0 = new Player("A");
            var p1 = new Player("B");
            var p2 = new Player("C");
            p0.Hand.Add(C(10));
            p1.Hand.Add(C(9));
            p2.Hand.Add(C(9, Suit.Hearts));
            var game = Game.FromSetup(new[] { p0, p1, p2 }, null, new List<Card>(), null, 0);
            var r = game.Play(0, new[] { C(10) });
            Assert.IsTrue(r.Burned);
            Assert.IsTrue(r.PlayerFinished);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        private static List<Player> MakePlayers(int count, int seed)
        {
            return Enumerable.Range(0, count)
                .Select(i => new Player("P" + i, false, (AiDifficulty)((i + seed) % 3)))
                .ToList();
        }

        [Test]
        public void Ai_FinishesGames_ConservesCards_AndTerminates()
        {
            var variants = new[]
            {
                RulesConfig.Classic,
                Rules(PowerType.Transparent, PowerType.LowerOrEqual),
                new RulesConfig { FourOfAKindBurns = false },
                new RulesConfig { Mode = GameMode.Demon },
                RulesConfig.WithPowers(new PowerSlot(PowerType.Skip), new PowerSlot(PowerType.Reverse),
                    new PowerSlot(PowerType.ForcePickUp), new PowerSlot(PowerType.PlayAgain)),
                Rules(PowerType.Skip, PowerType.Reverse),
                new RulesConfig { JumpIn = true, AllowInitialSwap = false },
                new RulesConfig { JumpIn = true, Mode = GameMode.Demon,
                    Powers = RulesConfig.DefaultPowers() }
            };

            foreach (var rules in variants)
            {
                int maxPlayers = RulesConfig.MaxPlayersFor(rules.Mode);
                for (int playerCount = RulesConfig.MinPlayers; playerCount <= maxPlayers; playerCount++)
                {
                    for (int seed = 0; seed < 25; seed++)
                    {
                        var game = new Game(MakePlayers(playerCount, seed), rules, seed);
                        var rng = new Random(seed);
                        for (int i = 0; i < playerCount; i++) AiBrain.DoSwap(game, i, rng);
                        Assert.AreEqual(GamePhase.Play, game.Phase);
                        Assert.AreEqual(playerCount > 5 ? 2 : 1, game.DeckCount, "Tailles par défaut : 9 cartes/joueur");

                        // Grâce à la sécurité anti-blocage, toute manche se termine, quels que soient les pouvoirs.
                        int moves = 0;
                        while (game.Phase == GamePhase.Play)
                        {
                            Assert.Less(++moves, 20000,
                                $"Manche sans fin (joueurs={playerCount}, seed={seed}, mode={rules.Mode})");
                            if (rules.JumpIn)
                                for (int j = 0; j < playerCount && game.Phase == GamePhase.Play; j++)
                                    AiBrain.TryJumpIn(game, j, rng);
                            if (game.Phase != GamePhase.Play) break;

                            var result = AiBrain.TakeTurn(game, rng);
                            Assert.IsTrue(result.Ok, $"Coup refusé : {result} (seed={seed})");

                            int total = game.DrawPile.Count + game.DiscardPile.Count + game.BurnedCount
                                        + game.Players.Sum(p => p.TotalCards);
                            Assert.AreEqual(game.TotalCardCount, total, "Conservation des cartes");
                        }

                        Assert.AreEqual(GamePhase.Finished, game.Phase);
                        Assert.IsNotNull(game.LoserIndex);
                        Assert.AreEqual(playerCount, game.FinishOrder.Count);
                    }
                }
            }
        }

        [Test]
        public void Stalemate_EndsRound_RanksByCardCount_AndMostCardsLoses()
        {
            // Partie connue pour boucler (deux joueurs se repassent la défausse en fin de manche).
            var rules = Rules(PowerType.Skip, PowerType.Reverse);
            var game = new Game(MakePlayers(5, 1), rules, 1);
            var rng = new Random(1);
            for (int i = 0; i < 5; i++) AiBrain.DoSwap(game, i, rng);

            int moves = 0;
            while (game.Phase == GamePhase.Play && moves++ < 20000) AiBrain.TakeTurn(game, rng);

            Assert.AreEqual(GamePhase.Finished, game.Phase);
            Assert.IsTrue(game.IsStalemate);
            Assert.Less(moves, 1000, "Une boucle exacte doit être coupée rapidement (détection de répétition)");
            Assert.AreEqual(5, game.FinishOrder.Count);
            Assert.AreEqual(game.FinishOrder[4], game.LoserIndex);

            int loserCards = game.Players[game.LoserIndex.Value].TotalCards;
            foreach (var p in game.Players)
                Assert.LessOrEqual(p.TotalCards, loserCards, "Le Shithead a le plus de cartes");
            Assert.AreEqual(game.TotalCardCount, game.DrawPile.Count + game.DiscardPile.Count
                + game.BurnedCount + game.Players.Sum(p => p.TotalCards));
        }

        [Test]
        public void NormalGames_AreNotCutShortByStalemateGuard()
        {
            int stalemates = 0;
            for (int seed = 0; seed < 60; seed++)
            {
                var game = new Game(MakePlayers(3 + seed % 3, seed), RulesConfig.Classic, seed);
                var rng = new Random(seed);
                for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
                while (game.Phase == GamePhase.Play) AiBrain.TakeTurn(game, rng);
                if (game.IsStalemate) stalemates++;
            }
            Assert.AreEqual(0, stalemates);
        }

        [Test]
        public void PlayerCountLimits_DependOnMode()
        {
            Player[] Make(int n) => Enumerable.Range(0, n).Select(i => new Player("P" + i)).ToArray();

            Assert.Throws<ArgumentException>(() => new Game(Make(2), RulesConfig.Classic, 1));
            Assert.Throws<ArgumentException>(() => new Game(Make(6), RulesConfig.Classic, 1));
            Assert.DoesNotThrow(() => new Game(Make(5), RulesConfig.Classic, 1));

            var demon = new RulesConfig { Mode = GameMode.Demon };
            Assert.Throws<ArgumentException>(() => new Game(Make(2), demon, 1));
            Assert.Throws<ArgumentException>(() => new Game(Make(11), demon, 1));
            var ten = new Game(Make(10), demon, 1);
            Assert.AreEqual(2, ten.DeckCount);
            Assert.AreEqual(104 - 90, ten.DrawPile.Count);
        }

        [Test]
        public void Settings_Validation()
        {
            Assert.IsEmpty(MatchSettings.CreateDefault(GameMode.Classic, 5, 999).Validate());
            Assert.IsEmpty(MatchSettings.CreateDefault(GameMode.Demon, 10, 1).Validate());

            Assert.IsNotEmpty(MatchSettings.CreateDefault(GameMode.Classic, 2).Validate(), "2 joueurs : trop peu");
            Assert.IsNotEmpty(MatchSettings.CreateDefault(GameMode.Classic, 6).Validate(), "6 joueurs en classique");
            Assert.IsNotEmpty(MatchSettings.CreateDefault(GameMode.Demon, 11).Validate(), "11 joueurs en démon");
            Assert.IsNotEmpty(MatchSettings.CreateDefault(GameMode.Classic, 3, 0).Validate(), "0 manche");
            Assert.IsNotEmpty(MatchSettings.CreateDefault(GameMode.Classic, 3, 1000).Validate(), "1000 manches");

            var dup = MatchSettings.CreateDefault(GameMode.Classic, 3);
            dup.Opponents[1].Name = dup.Opponents[0].Name.ToUpperInvariant();
            Assert.IsNotEmpty(dup.Validate(), "Noms en double");

            var blank = MatchSettings.CreateDefault(GameMode.Classic, 3);
            blank.Opponents[0].Name = "   ";
            Assert.IsNotEmpty(blank.Validate(), "Nom vide");

            Assert.Throws<ArgumentException>(() => new Match(blank, 1));
        }

        [Test]
        public void Match_PlaysAllRounds_AndAccumulatesScores()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 4, 3);
            settings.HumanName = "Maxime";
            settings.Opponents[0].Difficulty = AiDifficulty.Easy;
            settings.Opponents[1].Difficulty = AiDifficulty.Normal;
            settings.Opponents[2].Difficulty = AiDifficulty.Hard;

            var match = new Match(settings, 7);
            Assert.AreEqual("Maxime", match.Players[0].Name);
            Assert.IsTrue(match.Players[0].IsHuman);
            Assert.AreEqual(AiDifficulty.Hard, match.Players[3].Difficulty);

            var rng = new Random(7);
            while (!match.IsOver)
            {
                var game = match.StartNextRound();
                for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
                while (game.Phase == GamePhase.Play) AiBrain.TakeTurn(game, rng);
                match.CompleteRound();
            }

            Assert.AreEqual(3, match.RoundsCompleted);
            Assert.Throws<InvalidOperationException>(() => match.StartNextRound());

            var standings = match.GetStandings();
            Assert.AreEqual(4, standings.Count);
            // 3 manches x (3+2+1+0) points distribués.
            Assert.AreEqual(18, standings.Sum(s => s.Points));
            Assert.AreEqual(3, standings.Sum(s => s.ShitheadCount));
            for (int i = 1; i < standings.Count; i++)
                Assert.GreaterOrEqual(standings[i - 1].Points, standings[i].Points);
        }

        [Test]
        public void Match_CannotStartRoundBeforePreviousIsRecorded()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 2), 1);
            match.StartNextRound();
            Assert.Throws<InvalidOperationException>(() => match.StartNextRound());
            Assert.Throws<InvalidOperationException>(() => match.CompleteRound(), "Manche pas finie");
        }

        // ---------------------------------------------------------------- Pouvoirs configurables

        [Test]
        public void Power_Skip_SkipsOnePlayerPerCardPlayed()
        {
            var rules = Rules(PowerType.Skip);
            var one = Setup3(new[] { C(8), C(13) }, rules);
            var r = one.Play(0, new[] { C(8) });
            Assert.AreEqual(PowerType.Skip, r.Power);
            Assert.AreEqual(2, one.CurrentPlayerIndex, "B est sauté");

            var two = Setup3(new[] { C(8), C(8, Suit.Hearts), C(13) }, rules);
            two.Play(0, new[] { C(8), C(8, Suit.Hearts) });
            Assert.AreEqual(0, two.CurrentPlayerIndex, "B et C sont sautés : retour à A");
        }

        [Test]
        public void Power_Reverse_FlipsDirection()
        {
            var game = Setup3(new[] { C(9), C(13) }, Rules(PowerType.Reverse));
            game.Play(0, new[] { C(9) });
            Assert.AreEqual(-1, game.Direction);
            Assert.AreEqual(2, game.CurrentPlayerIndex, "Le jeu repart vers C");
            game.Play(2, new[] { C(12, Suit.Diamonds) });
            Assert.AreEqual(1, game.CurrentPlayerIndex);
        }

        [Test]
        public void Power_ForcePickUp_MakesNextPlayerTakePileAndSkip()
        {
            var game = Setup3(new[] { C(11), C(13) }, Rules(PowerType.ForcePickUp), new[] { C(4) });
            var r = game.Play(0, new[] { C(11) });
            Assert.AreEqual(1, r.ForcedPickUpPlayer);
            Assert.AreEqual(0, game.DiscardPile.Count);
            Assert.AreEqual(5, game.Players[1].Hand.Count, "3 cartes + le 4 + le valet");
            Assert.AreEqual(2, game.CurrentPlayerIndex, "B a perdu son tour");
        }

        [Test]
        public void Power_PlayAgain_KeepsTurn()
        {
            var game = Setup3(new[] { C(5), C(13) }, Rules(PowerType.PlayAgain));
            game.Play(0, new[] { C(5) });
            Assert.AreEqual(0, game.CurrentPlayerIndex);
        }

        [Test]
        public void Power_CanBeAssignedToAnyRank()
        {
            // Remise à zéro sur le 5, « plus bas » sur le 6.
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.Reset, 5),
                new PowerSlot(PowerType.LowerOrEqual, 6));
            var onFive = Setup3(new[] { C(3) }, rules, new[] { C(5) });
            Assert.IsTrue(onFive.CanPlayRank(3), "Après un 5 (remise à zéro) tout est jouable");
            Assert.IsTrue(onFive.CanPlayRank(5));

            var onSix = Setup3(new[] { C(3) }, rules, new[] { C(6) });
            Assert.IsTrue(onSix.CanPlayRank(4));
            Assert.IsFalse(onSix.CanPlayRank(9));
            Assert.IsTrue(onSix.CanPlayRank(5), "5 = remise à zéro, toujours jouable");

            // Le 2 et le 10 ne sont plus spéciaux ici.
            Assert.IsFalse(Setup3(new[] { C(2) }, rules, new[] { C(9) }).CanPlayRank(2));
            Assert.IsFalse(Setup3(new[] { C(10) }, rules, new[] { C(12) }).CanPlayRank(10));
        }

        [Test]
        public void Powers_Validation()
        {
            Assert.IsEmpty(new RulesConfig().Validate());
            Assert.IsEmpty(RulesConfig.WithPowers().Validate(), "Zéro pouvoir est permis");

            var five = new RulesConfig { Powers = PowerCatalog.All.Take(5).Select(p => new PowerSlot(p)).ToList() };
            Assert.IsNotEmpty(five.Validate(), "Plus de 4 pouvoirs");

            var dupRank = RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 8), new PowerSlot(PowerType.Reverse, 8));
            Assert.IsNotEmpty(dupRank.Validate());

            var dupPower = RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 8), new PowerSlot(PowerType.Skip, 9));
            Assert.IsNotEmpty(dupPower.Validate());

            Assert.IsNotEmpty(RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 1)).Validate());
            Assert.IsNotEmpty(RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 15)).Validate());

            var settings = MatchSettings.CreateDefault(GameMode.Classic, 3);
            settings.Rules = dupRank;
            Assert.IsNotEmpty(settings.Validate());
            Assert.Throws<ArgumentException>(() =>
                new Game(new[] { new Player("A"), new Player("B"), new Player("C") }, dupRank, 1));
        }

        [Test]
        public void EveryPower_HasDefaultRank_AndDistinctRanks()
        {
            var ranks = PowerCatalog.All.Select(PowerCatalog.DefaultRank).ToList();
            Assert.AreEqual(ranks.Count, ranks.Distinct().Count());
            Assert.AreEqual(Enum.GetValues(typeof(PowerType)).Length, PowerCatalog.All.Length);
        }

        // ---------------------------------------------------------------- Jeu à la volée

        [Test]
        public void JumpIn_PlaysOutOfTurn_AndTurnResumesAfterJumper()
        {
            var rules = new RulesConfig { JumpIn = true };
            var game = Setup3(new[] { C(13) }, rules, new[] { C(7) });
            game.Players[2].Hand.Add(C(7, Suit.Hearts));

            var r = game.JumpIn(2, new[] { C(7, Suit.Hearts) });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Après C, c'est à A");
            Assert.AreEqual(2, game.DiscardPile.Count);
        }

        [Test]
        public void JumpIn_Refused_WhenDisabledMismatchedOrCurrentPlayer()
        {
            var off = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = false }, new[] { C(7) });
            off.Players[1].Hand.Add(C(7, Suit.Hearts));
            Assert.AreEqual(MoveError.CannotJumpIn, off.JumpIn(1, new[] { C(7, Suit.Hearts) }).Error);

            var on = Setup3(new[] { C(13), C(7, Suit.Clubs) }, new RulesConfig { JumpIn = true }, new[] { C(7) });
            Assert.AreEqual(MoveError.CannotJumpIn, on.JumpIn(1, new[] { C(9, Suit.Hearts) }).Error, "Valeur différente");
            Assert.AreEqual(MoveError.CannotJumpIn, on.JumpIn(0, new[] { C(7, Suit.Clubs) }).Error, "C'est son tour");

            var empty = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = true });
            Assert.AreEqual(MoveError.CannotJumpIn, empty.JumpIn(1, new[] { C(9, Suit.Hearts) }).Error, "Défausse vide");
        }

        [Test]
        public void JumpIn_CompletingFourOfAKind_BurnsAndJumperPlaysAgain()
        {
            var game = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = true },
                new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts) });
            game.Players[2].Hand.Add(C(7));

            var r = game.JumpIn(2, new[] { C(7) });
            Assert.IsTrue(r.Burned);
            Assert.AreEqual(2, game.CurrentPlayerIndex);
            Assert.AreEqual(0, game.DiscardPile.Count);
        }

        // ---------------------------------------------------------------- Échange de début de manche

        [Test]
        public void InitialSwapDisabled_StartsDirectlyInPlay()
        {
            var players = new[] { new Player("A"), new Player("B"), new Player("C") };
            var game = new Game(players, new RulesConfig { AllowInitialSwap = false }, 4);
            Assert.AreEqual(GamePhase.Play, game.Phase);
            var a = game.Players[0];
            Assert.IsFalse(game.SwapCards(0, a.Hand[0], a.FaceUp[0]));

            var enabled = new Game(new[] { new Player("A"), new Player("B"), new Player("C") }, RulesConfig.Classic, 4);
            Assert.AreEqual(GamePhase.Swap, enabled.Phase);
        }

        [Test]
        public void DeckCount_IsComputedFromCardsToDeal()
        {
            Assert.AreEqual(1, RulesConfig.DeckCountFor(27));  // 3 joueurs
            Assert.AreEqual(1, RulesConfig.DeckCountFor(45));  // 5 joueurs : 45 + 7 = 52
            Assert.AreEqual(2, RulesConfig.DeckCountFor(46));
            Assert.AreEqual(2, RulesConfig.DeckCountFor(90));  // 10 joueurs
            Assert.AreEqual(2, RulesConfig.DeckCountFor(97));  // 97 + 7 = 104
            Assert.AreEqual(3, RulesConfig.DeckCountFor(98));
        }

        [Test]
        public void Demon_DealUsesPerPlayerSlots_AndRecomputesDeckCount()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Demon, 5, 5);
            var match = new Match(settings, 5);

            var first = match.StartNextRound();
            Assert.AreEqual(1, first.DeckCount, "5 joueurs x 9 cartes = 45");

            // Le joueur 0 a accumulé des cartes : la manche suivante demande plus de paquets.
            var match2 = new Match(settings, 5);
            match2.Players[0].SetSlots(10, 12);
            var game = match2.StartNextRound();
            Assert.AreEqual(10, game.Players[0].FaceDown.Count);
            Assert.AreEqual(12, game.Players[0].FaceUp.Count);
            Assert.AreEqual(3, game.Players[0].Hand.Count);
            Assert.AreEqual(3, game.Players[1].FaceDown.Count);
            int dealt = 10 + 12 + 3 + 4 * 9;
            Assert.AreEqual(RulesConfig.DeckCountFor(dealt), game.DeckCount);
            Assert.AreEqual(2, game.DeckCount);
            Assert.AreEqual(game.TotalCardCount - dealt, game.DrawPile.Count);
        }

        [Test]
        public void Demon_LoserGainsCards_WinnerLosesCards_WithMinimumOne()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Demon, 4, 12);
            var match = new Match(settings, 21);
            var rng = new Random(21);

            while (!match.IsOver)
            {
                var before = match.Players.Select(p => (down: p.FaceDownSlots, up: p.FaceUpSlots)).ToList();
                var game = match.StartNextRound();
                for (int i = 0; i < game.Players.Count; i++)
                {
                    Assert.AreEqual(before[i].down, game.Players[i].FaceDown.Count);
                    Assert.AreEqual(before[i].up, game.Players[i].FaceUp.Count);
                    AiBrain.DoSwap(game, i, rng);
                }
                int guard = 0;
                while (game.Phase == GamePhase.Play && guard++ < 20000) AiBrain.TakeTurn(game, rng);
                Assert.AreEqual(GamePhase.Finished, game.Phase);
                match.CompleteRound();

                int winner = game.FinishOrder[0];
                int loser = game.LoserIndex.Value;
                for (int i = 0; i < before.Count; i++)
                {
                    var p = match.Players[i];
                    int expectedDown = before[i].down, expectedUp = before[i].up;
                    if (i == winner) { expectedDown = Math.Max(1, expectedDown - 1); expectedUp = Math.Max(1, expectedUp - 1); }
                    if (i == loser) { expectedDown++; expectedUp++; }
                    Assert.AreEqual(expectedDown, p.FaceDownSlots);
                    Assert.AreEqual(expectedUp, p.FaceUpSlots);
                    Assert.GreaterOrEqual(p.FaceDownSlots, 1);
                    Assert.GreaterOrEqual(p.FaceUpSlots, 1);
                }
            }
        }

        [Test]
        public void Classic_NeverChangesCardCounts()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 4, 3), 2);
            var rng = new Random(2);
            while (!match.IsOver)
            {
                var game = match.StartNextRound();
                for (int i = 0; i < game.Players.Count; i++) AiBrain.DoSwap(game, i, rng);
                while (game.Phase == GamePhase.Play) AiBrain.TakeTurn(game, rng);
                match.CompleteRound();
            }
            foreach (var p in match.Players)
            {
                Assert.AreEqual(3, p.FaceDownSlots);
                Assert.AreEqual(3, p.FaceUpSlots);
            }
        }

        [Test]
        public void Ai_Easy_Normal_Hard_AllProduceLegalMoves()
        {
            foreach (AiDifficulty d in Enum.GetValues(typeof(AiDifficulty)))
            {
                var players = new[]
                {
                    new Player("A", false, d), new Player("B", false, d), new Player("C", false, d)
                };
                var game = new Game(players, RulesConfig.Classic, 11);
                var rng = new Random(11);
                for (int i = 0; i < 3; i++) AiBrain.DoSwap(game, i, rng);
                int guard = 0;
                while (game.Phase == GamePhase.Play && guard++ < 20000)
                    Assert.IsTrue(AiBrain.TakeTurn(game, rng).Ok, $"Difficulté {d}");
                Assert.AreEqual(GamePhase.Finished, game.Phase, $"Difficulté {d}");
            }
        }
    }
}

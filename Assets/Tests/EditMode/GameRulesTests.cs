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

        /// <summary>Les 9 pouvoirs actifs en même temps, sur les valeurs 2 à 10.</summary>
        private static RulesConfig AllPowers() => RulesConfig.WithPowers(
            PowerCatalog.All.Select((p, i) => new PowerSlot(p, 2 + i)).ToArray());

        /// <summary>3 joueurs en phase de jeu : A (main donnée) joue en premier ; B et C ont de quoi tenir.</summary>
        private static Game Setup3(IEnumerable<Card> hand0, RulesConfig rules, IEnumerable<Card> discard = null,
            int deckCount = 1)
        {
            var a = new Player("A");
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(hand0);
            b.Hand.AddRange(new[] { C(9, Suit.Hearts), C(12, Suit.Hearts), C(13, Suit.Hearts) });
            c.Hand.AddRange(new[] { C(9, Suit.Diamonds), C(12, Suit.Diamonds), C(13, Suit.Diamonds) });
            return Game.FromSetup(new[] { a, b, c }, rules, new List<Card>(), discard?.ToList(), 0, deckCount);
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
            var game = Setup(new[] { C(10), C(4) }, new[] { C(8), C(9) });
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
        public void AfterTheTwo_NextPlayerMayPlayAnything_AndOnlyAceOrTwoBeatsTheAce()
        {
            var rules = Rules(PowerType.Reset);
            var a = new Player("A");
            var b = new Player("B");
            var c = new Player("C");
            a.Hand.AddRange(new[] { C(2), C(5) });
            b.Hand.AddRange(new[] { C(Card.Ace), C(4) });
            c.Hand.AddRange(new[] { C(9, Suit.Diamonds), C(12, Suit.Diamonds) });
            var game = Game.FromSetup(new[] { a, b, c }, rules, new List<Card>(), new[] { C(13) }.ToList(), 0, 1);

            Assert.IsTrue(game.Play(0, new[] { C(2) }).Ok);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            Assert.IsTrue(game.CanPlayRank(4), "Tout se pose sur le 2");

            Assert.IsTrue(game.Play(1, new[] { C(Card.Ace) }).Ok);
            Assert.AreEqual(2, game.CurrentPlayerIndex);
            Assert.IsFalse(game.HasLegalPlay(2), "Ni 9 ni dame ne battent l'as");
            Assert.AreEqual(3, game.DiscardPile.Count);
            Assert.IsTrue(game.PickUpPile(2).Ok);
            Assert.AreEqual(5, c.Hand.Count);
        }

        [Test]
        public void ThreeTransparent_LooksUnderTheThree()
        {
            var rules = Rules(PowerType.Transparent);
            var game = Setup(new[] { C(6), C(9) }, new[] { C(8), C(3) }, rules);
            Assert.AreEqual(8, game.RankToBeat());
            Assert.IsFalse(game.CanPlayRank(6));
            Assert.IsFalse(game.CanPlayRank(3));
            var empty = Setup(new[] { C(3), C(9) }, new Card[0], rules);
            Assert.IsTrue(empty.CanPlayRank(3));
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
            Assert.IsFalse(game.CanPlayRank(10));
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
                Rules(PowerType.SameOrFace, PowerType.Transparent),
                AllPowers(),
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
            // Partie connue pour boucler (3 joueurs difficiles, graine 2) : le valet fait ramasser le suivant, qui
            // récupère le valet et le rejoue : les cartes tournent sans que personne ne progresse.
            var rules = RulesConfig.WithPowers(
                new PowerSlot(PowerType.Reset, 2), new PowerSlot(PowerType.Transparent, 3),
                new PowerSlot(PowerType.PlayAgain, 5), new PowerSlot(PowerType.LowerOrEqual, 7),
                new PowerSlot(PowerType.Skip, 8), new PowerSlot(PowerType.Reverse, 9),
                new PowerSlot(PowerType.Burn, 10), new PowerSlot(PowerType.ForcePickUp, Card.Jack));
            var players = Enumerable.Range(0, 3).Select(i => new Player("P" + i, false, AiDifficulty.Hard)).ToList();
            var game = new Game(players, rules, 2);
            var rng = new Random(2);
            for (int i = 0; i < 3; i++) AiBrain.DoSwap(game, i, rng);

            int moves = 0;
            while (game.Phase == GamePhase.Play && moves++ < 20000) AiBrain.TakeTurn(game, rng);

            Assert.AreEqual(GamePhase.Finished, game.Phase);
            Assert.IsTrue(game.IsStalemate);
            Assert.Less(moves, 1000, "Une boucle exacte doit être coupée rapidement (détection de répétition)");
            Assert.AreEqual(3, game.FinishOrder.Count);
            Assert.AreEqual(game.FinishOrder[2], game.LoserIndex);

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
            // Quelques boucles réelles entre deux IA difficiles existent (arrêtées par la garde) : elles doivent rester rares.
            Assert.LessOrEqual(stalemates, 3);
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
            // Les adversaires s'assoient dans un ordre tiré au sort : on vérifie leurs niveaux sans compter sur la place.
            CollectionAssert.AreEquivalent(
                new[] { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard },
                match.Players.Skip(1).Select(p => p.Difficulty).ToArray());

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

            // Le 10 n'est plus spécial ici : il ne bat pas un roi. Le 2, lui, reste la carte la plus forte.
            Assert.IsFalse(Setup3(new[] { C(10) }, rules, new[] { C(12) }).CanPlayRank(10));
            Assert.IsTrue(Setup3(new[] { C(2) }, rules, new[] { C(9) }).CanPlayRank(2));
        }

        [Test]
        public void Burn_FollowsTheCardValue_AndCannotBePlayedOnAHead()
        {
            // Réglages par défaut : le 10 brûle. Il garde sa valeur de 10, comme une carte ordinaire.
            foreach (int head in new[] { Card.Jack, Card.Queen, Card.King, Card.Ace })
                Assert.IsFalse(Setup3(new[] { C(10) }, new RulesConfig(), new[] { C(head) }).CanPlayRank(10),
                    $"Un 10 ne se pose pas sur la carte de valeur {head}");

            foreach (int below in new[] { 3, 8, 9, 10 })
                Assert.IsTrue(Setup3(new[] { C(10) }, new RulesConfig(), new[] { C(below) }).CanPlayRank(10),
                    $"Un 10 se pose sur {below}");
            Assert.IsTrue(Setup3(new[] { C(10) }, new RulesConfig(), null).CanPlayRank(10), "Sur une défausse vide");

            // Le 2, la plus forte, se pose sur une tête.
            Assert.IsTrue(Setup3(new[] { C(2) }, new RulesConfig(), new[] { C(Card.King) }).CanPlayRank(2));

            // Même chose avec la brûlure placée sur une autre valeur : elle ne passe pas outre la valeur.
            var onFour = RulesConfig.WithPowers(new PowerSlot(PowerType.Burn, 4));
            Assert.IsFalse(Setup3(new[] { C(4) }, onFour, new[] { C(9) }).CanPlayRank(4));
            Assert.IsTrue(Setup3(new[] { C(4) }, onFour, new[] { C(3) }).CanPlayRank(4));
        }

        [Test]
        public void AllPowersActive_NoCardOutranksTheTopOneExceptByValueOrByItsOwnPower()
        {
            // Tous les pouvoirs actifs, chacun sur sa valeur habituelle : on vérifie ce qui se pose sur un valet.
            var rules = RulesConfig.WithPowers(
                new PowerSlot(PowerType.Reset, 2), new PowerSlot(PowerType.Transparent, 3),
                new PowerSlot(PowerType.PlayAgain, 5), new PowerSlot(PowerType.LowerOrEqual, 7),
                new PowerSlot(PowerType.Skip, 8), new PowerSlot(PowerType.Reverse, 9),
                new PowerSlot(PowerType.Burn, 10), new PowerSlot(PowerType.ForcePickUp, Card.Queen),
                new PowerSlot(PowerType.SameOrFace, Card.King));
            var onJack = Setup3(new[] { C(2) }, rules, new[] { C(Card.Jack) });

            Assert.IsFalse(onJack.CanPlayRank(10), "Le 10 (brûlure) ne se pose pas sur un valet");
            Assert.IsFalse(onJack.CanPlayRank(9));
            Assert.IsFalse(onJack.CanPlayRank(8));
            Assert.IsFalse(onJack.CanPlayRank(5));

            Assert.IsTrue(onJack.CanPlayRank(Card.Jack), "Un autre valet");
            Assert.IsTrue(onJack.CanPlayRank(Card.Queen));
            Assert.IsTrue(onJack.CanPlayRank(Card.King));
            Assert.IsTrue(onJack.CanPlayRank(Card.Ace));
            Assert.IsTrue(onJack.CanPlayRank(2), "Le 2 est la plus forte");
            Assert.IsFalse(onJack.CanPlayRank(3), "Le 3 est la carte la plus faible, même transparent");
        }

        [Test]
        public void Two_IsTheStrongestCard_WhenItHasNoPower()
        {
            var noPowers = RulesConfig.WithPowers();

            foreach (int below in new[] { 3, 4, 8, 10, Card.Jack, Card.King, Card.Ace })
                Assert.IsTrue(Setup3(new[] { C(2) }, noPowers, new[] { C(below) }).CanPlayRank(2),
                    $"Le 2 bat le {below}");

            var onTwo = Setup3(new[] { C(3) }, noPowers, new[] { C(2) });
            Assert.IsFalse(onTwo.CanPlayRank(3));
            Assert.IsFalse(onTwo.CanPlayRank(Card.Ace), "Même l'as ne bat pas le 2");
            Assert.IsTrue(onTwo.CanPlayRank(2), "Un autre 2 reste jouable (égalité)");

            Assert.AreEqual(15, Card.Strength(2));
            Assert.Greater(Card.Strength(2), Card.Strength(Card.Ace));
            Assert.Less(Card.Strength(3), Card.Strength(4));
        }

        [Test]
        public void Two_WithoutPower_IsTheLastCardToStartWith()
        {
            var game = new Game(new[] { new Player("A"), new Player("B"), new Player("C") },
                RulesConfig.WithPowers(), 3);
            game.Players[0].Hand.Clear(); game.Players[0].Hand.AddRange(new[] { C(2), C(2, Suit.Hearts), C(14) });
            game.Players[1].Hand.Clear(); game.Players[1].Hand.AddRange(new[] { C(9), C(12), C(13) });
            game.Players[2].Hand.Clear(); game.Players[2].Hand.AddRange(new[] { C(10), C(11), C(14, Suit.Hearts) });
            game.SetReady(0);
            game.SetReady(1);
            game.SetReady(2);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Le plus petit : le 9 du joueur B (le 2 est le plus fort)");
        }

        [Test]
        public void LowerOrEqual_TreatsTheTwoAsTheHighestCard()
        {
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.LowerOrEqual, 7));
            var game = Setup3(new[] { C(3) }, rules, new[] { C(7) });
            Assert.IsTrue(game.CanPlayRank(5));
            Assert.IsFalse(game.CanPlayRank(2), "Le 2 est plus fort que 7 : refusé après un « plus bas »");
        }

        [Test]
        public void Power_SameOrFace_AllowsOnlySameValueOrAFaceCard()
        {
            var game = Setup3(new[] { C(13) }, Rules(PowerType.SameOrFace), new[] { C(9), C(7) });

            Assert.IsTrue(game.CanPlayRank(7), "Le même 7");
            Assert.IsTrue(game.CanPlayRank(Card.Jack));
            Assert.IsTrue(game.CanPlayRank(Card.Queen));
            Assert.IsTrue(game.CanPlayRank(Card.King));
            Assert.IsTrue(game.CanPlayRank(2), "La remise à zéro reste jouable partout");
            Assert.IsFalse(game.CanPlayRank(10), "La brûlure suit sa valeur : un 10 n'est ni un 7 ni une tête");

            Assert.IsFalse(game.CanPlayRank(5), "Plus bas : refusé");
            Assert.IsFalse(game.CanPlayRank(8), "Plus haut mais ni 7 ni tête : refusé");
            Assert.IsFalse(game.CanPlayRank(9));
            Assert.IsFalse(game.CanPlayRank(Card.Ace), "L'as n'est pas une tête");
        }

        [Test]
        public void Power_SameOrFace_WorksOnAnyRank_AndLeavesOtherCardsAlone()
        {
            var rules = RulesConfig.WithPowers(new PowerSlot(PowerType.SameOrFace, 4), new PowerSlot(PowerType.Reset));
            var onFour = Setup3(new[] { C(13) }, rules, new[] { C(4) });
            Assert.IsTrue(onFour.CanPlayRank(4));
            Assert.IsTrue(onFour.CanPlayRank(Card.Queen));
            Assert.IsFalse(onFour.CanPlayRank(6));

            // Sur un 7 ordinaire (sans pouvoir ici), la règle habituelle s'applique.
            var onSeven = Setup3(new[] { C(13) }, rules, new[] { C(7) });
            Assert.IsTrue(onSeven.CanPlayRank(8));
            Assert.IsFalse(onSeven.CanPlayRank(5));
        }

        [Test]
        public void Power_SameOrFace_CanBePlayedAndFollowedByAFace()
        {
            var game = Setup3(new[] { C(7), C(13) }, Rules(PowerType.SameOrFace));
            Assert.IsTrue(game.Play(0, new[] { C(7) }).Ok);
            Assert.AreEqual(1, game.CurrentPlayerIndex);
            Assert.AreEqual(MoveError.NotPlayable, game.Play(1, new[] { C(9, Suit.Hearts) }).Error);
            Assert.IsTrue(game.Play(1, new[] { C(12, Suit.Hearts) }).Ok, "Une dame est une tête");
        }

        [Test]
        public void Powers_Validation()
        {
            Assert.IsEmpty(new RulesConfig().Validate());
            Assert.IsEmpty(RulesConfig.WithPowers().Validate(), "Zéro pouvoir est permis");

            // Plus aucune limite de nombre : tout le catalogue peut être actif, chaque pouvoir sur sa propre valeur.
            Assert.IsEmpty(AllPowers().Validate());

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
        public void ValidationMessages_AreKeysWithArguments_NotText()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 2, 0);
            var keys = settings.Validate().Select(m => m.Key).ToList();
            CollectionAssert.Contains(keys, "validation.players_count");
            CollectionAssert.Contains(keys, "validation.rounds_range");

            var players = settings.Validate().First(m => m.Key == "validation.players_count");
            CollectionAssert.AreEqual(new object[] { 3, 5, 2 }, players.Args);

            var dup = RulesConfig.WithPowers(new PowerSlot(PowerType.Skip, 8), new PowerSlot(PowerType.Skip, 9));
            var dupMsg = dup.Validate().Single();
            Assert.AreEqual("validation.power_duplicate", dupMsg.Key);
            Assert.AreEqual("power.Skip.label", ((KeyRef)dupMsg.Args[0]).Key);

            var noRules = MatchSettings.CreateDefault(GameMode.Classic, 3);
            noRules.Rules = null;
            Assert.DoesNotThrow(() => noRules.Validate(), "Règles nulles : pas d'exception");
            Assert.AreEqual("validation.rules_missing", noRules.Validate()[0].Key);
        }

        [Test]
        public void PowerAndCardKeys_FollowNamingScheme()
        {
            foreach (var p in PowerCatalog.All)
            {
                Assert.AreEqual($"power.{p}.label", PowerCatalog.LabelKey(p));
                Assert.AreEqual($"power.{p}.description", PowerCatalog.DescriptionKey(p));
            }
            Assert.IsNull(C(10).RankKey);
            Assert.AreEqual("card.rank.11", C(11).RankKey);
            Assert.AreEqual("card.rank.14", C(14).RankKey);
        }

        [Test]
        public void EveryPower_HasDefaultRank_AndDistinctRanks()
        {
            // « Plus bas » et « même valeur ou tête » sont deux variantes du 7 : elles partagent leur valeur
            // traditionnelle, et le modèle de configuration leur donne des valeurs distinctes si on les cumule.
            var ranks = PowerCatalog.All.Where(p => p != PowerType.SameOrFace)
                .Select(PowerCatalog.DefaultRank).ToList();
            Assert.AreEqual(ranks.Count, ranks.Distinct().Count());
            Assert.AreEqual(PowerCatalog.DefaultRank(PowerType.LowerOrEqual), PowerCatalog.DefaultRank(PowerType.SameOrFace));
            Assert.AreEqual(Enum.GetValues(typeof(PowerType)).Length, PowerCatalog.All.Length);
        }

        // ---------------------------------------------------------------- Jeu à la volée

        private static Card[] Sevens(int count) =>
            new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts), C(7, Suit.Spades) }
                .Concat(new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts), C(7, Suit.Spades) })
                .Take(count).ToArray();

        [Test]
        public void JumpIn_CompletesTheSet_BurnsThePile_AndJumperPlaysAgain()
        {
            // Un paquet : 3 sept déjà posés à la suite, le joueur C détient le quatrième.
            var game = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = true }, Sevens(3));
            game.Players[2].Hand.Add(C(7, Suit.Spades));

            CollectionAssert.AreEqual(new[] { C(7, Suit.Spades) }, game.GetJumpInCards(2));
            var r = game.JumpIn(2, new[] { C(7, Suit.Spades) });

            Assert.IsTrue(r.Ok);
            Assert.IsTrue(r.Burned);
            Assert.AreEqual(2, game.CurrentPlayerIndex, "Défausse brûlée : le joueur C rejoue");
            Assert.AreEqual(0, game.DiscardPile.Count);
        }

        [Test]
        public void JumpIn_IsRefused_WhenTheSetIsNotAlmostComplete()
        {
            var rules = new RulesConfig { JumpIn = true };

            var one = Setup3(new[] { C(13) }, rules, new[] { C(7, Suit.Clubs) });
            one.Players[2].Hand.Add(C(7, Suit.Spades));
            Assert.IsEmpty(one.GetJumpInCards(2), "Un seul 7 posé : trop tôt");
            Assert.AreEqual(MoveError.CannotJumpIn, one.JumpIn(2, new[] { C(7, Suit.Spades) }).Error);

            var two = Setup3(new[] { C(13) }, rules, Sevens(2));
            two.Players[2].Hand.Add(C(7, Suit.Spades));
            Assert.IsEmpty(two.GetJumpInCards(2), "Deux 7 posés : il en manque encore deux");
        }

        [Test]
        public void JumpIn_IsRefused_ForTheCurrentPlayer_WhenDisabled_OrWithoutTheLastCard()
        {
            var on = Setup3(new[] { C(13), C(7, Suit.Spades) }, new RulesConfig { JumpIn = true }, Sevens(3));
            Assert.IsEmpty(on.GetJumpInCards(0), "C'est son tour : il joue normalement");
            Assert.AreEqual(MoveError.CannotJumpIn, on.JumpIn(0, new[] { C(7, Suit.Spades) }).Error);
            Assert.AreEqual(MoveError.CannotJumpIn, on.JumpIn(1, new[] { C(9, Suit.Hearts) }).Error,
                "Le joueur B n'a pas de 7");

            var off = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = false }, Sevens(3));
            off.Players[1].Hand.Add(C(7, Suit.Spades));
            Assert.AreEqual(MoveError.CannotJumpIn, off.JumpIn(1, new[] { C(7, Suit.Spades) }).Error, "Option désactivée");

            var empty = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = true });
            Assert.AreEqual(MoveError.CannotJumpIn, empty.JumpIn(1, new[] { C(9, Suit.Hearts) }).Error, "Défausse vide");
        }

        [Test]
        public void JumpIn_NeedsExactlyTheOneMissingCard()
        {
            var game = Setup3(new[] { C(13) }, new RulesConfig { JumpIn = true }, Sevens(3));
            game.Players[2].Hand.AddRange(new[] { C(7, Suit.Spades), C(9, Suit.Spades) });

            Assert.AreEqual(MoveError.InvalidCards, game.JumpIn(2, new[] { C(9, Suit.Spades) }).Error);
            Assert.AreEqual(MoveError.InvalidCards,
                game.JumpIn(2, new[] { C(7, Suit.Spades), C(9, Suit.Spades) }).Error);
            Assert.IsTrue(game.JumpIn(2, new[] { C(7, Suit.Spades) }).Ok);
        }

        [Test]
        public void JumpIn_WithTwoDecks_NeedsSevenCardsAlreadyDown()
        {
            var rules = new RulesConfig { JumpIn = true };

            var three = Setup3(new[] { C(13) }, rules, Sevens(3), deckCount: 2);
            three.Players[2].Hand.Add(C(7, Suit.Spades));
            Assert.AreEqual(8, three.FullSetSize);
            Assert.IsEmpty(three.GetJumpInCards(2), "Avec deux paquets, 3 ne suffisent plus");

            var seven = Setup3(new[] { C(13) }, rules, Sevens(7), deckCount: 2);
            seven.Players[2].Hand.Add(C(7, Suit.Spades));
            Assert.AreEqual(1, seven.GetJumpInCards(2).Count);
            Assert.IsTrue(seven.JumpIn(2, new[] { C(7, Suit.Spades) }).Burned);
        }

        [Test]
        public void FullSet_BurnsTheNeededNumberOfCards_AccordingToTheDeckCount()
        {
            var oneDeck = Setup3(new[] { C(7, Suit.Spades) }, new RulesConfig(), Sevens(3));
            Assert.IsTrue(oneDeck.Play(0, new[] { C(7, Suit.Spades) }).Burned, "Quatre 7 avec un paquet");

            var twoDecks = Setup3(new[] { C(7, Suit.Spades) }, new RulesConfig(), Sevens(3), deckCount: 2);
            var r = twoDecks.Play(0, new[] { C(7, Suit.Spades) });
            Assert.IsFalse(r.Burned, "Quatre 7 avec deux paquets : il en faut huit");
            Assert.AreEqual(4, twoDecks.DiscardPile.Count);

            var eight = Setup3(new[] { C(7, Suit.Spades) }, new RulesConfig(), Sevens(7), deckCount: 2);
            Assert.IsTrue(eight.Play(0, new[] { C(7, Suit.Spades) }).Burned, "Huit 7 avec deux paquets");
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

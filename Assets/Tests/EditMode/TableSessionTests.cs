using System;
using System.Collections.Generic;
using System.Linq;
using CaSentLeSoufre.Core;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class TableSessionTests
    {
        private static Card C(int rank, Suit suit = Suit.Spades) => new Card(rank, suit);

        private static TableTimings Steady() => new TableTimings { Jitter = 0 };

        /// <summary>Joueur 0 humain ; les joueurs 1 et 2 sont des IA de la difficulté donnée.</summary>
        private static Game Scenario(RulesConfig rules, int current, AiDifficulty ai1, AiDifficulty ai2,
            IEnumerable<Card> discard, IEnumerable<Card> hand0, IEnumerable<Card> hand1, IEnumerable<Card> hand2)
        {
            var p0 = new Player("Moi", true);
            var p1 = new Player("Bot1", false, ai1);
            var p2 = new Player("Bot2", false, ai2);
            p0.Hand.AddRange(hand0);
            p1.Hand.AddRange(hand1);
            p2.Hand.AddRange(hand2);
            return Game.FromSetup(new[] { p0, p1, p2 }, rules, new List<Card>(), discard?.ToList(), current);
        }

        private static readonly Card[] ThreeSevens = { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts) };

        private static readonly Card[] Filler = { C(13, Suit.Hearts), C(12, Suit.Hearts), C(14, Suit.Hearts) };

        // ---------------------------------------------------------------- Rythme des IA

        [Test]
        public void Ai_PlaysOnlyAfterItsDelay()
        {
            var game = Scenario(new RulesConfig(), 1, AiDifficulty.Normal, AiDifficulty.Normal, null,
                Filler, new[] { C(9, Suit.Hearts), C(13, Suit.Clubs) }, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Tick(1.9f);
            Assert.AreEqual(0, game.DiscardPile.Count, "Normale : 2 s de réflexion");
            Assert.IsEmpty(session.DrainEvents());

            session.Tick(0.2f);
            Assert.AreEqual(1, game.DiscardPile.Count);
            var events = session.DrainEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TableEventKind.Played, events[0].Kind);
            Assert.AreEqual(1, events[0].PlayerIndex);
            Assert.AreEqual(C(9, Suit.Hearts), events[0].Result.Played[0]);
        }

        [Test]
        public void Paused_FreezesTheAiAndTheirReactions_UntilResumed()
        {
            var game = Scenario(new RulesConfig(), 1, AiDifficulty.Normal, AiDifficulty.Normal, null,
                Filler, new[] { C(9, Suit.Hearts), C(13, Suit.Clubs) }, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Paused = true;
            session.Tick(10f);
            Assert.AreEqual(0, game.DiscardPile.Count, "Rien ne bouge pendant la pause");

            session.Paused = false;
            session.Tick(1.9f);
            Assert.AreEqual(0, game.DiscardPile.Count, "Le temps perdu pendant la pause ne compte pas");
            session.Tick(0.2f);
            Assert.AreEqual(1, game.DiscardPile.Count);
        }

        [Test]
        public void Paused_StillLetsThePlayerAct()
        {
            var game = Scenario(new RulesConfig(), 0, AiDifficulty.Normal, AiDifficulty.Normal, null,
                new[] { C(5), C(13) }, Filler, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Paused = true;
            Assert.IsTrue(session.HumanPlay(new[] { C(5) }).Ok);
        }

        [Test]
        public void HardAi_IsFasterThanEasyAi()
        {
            var hard = Scenario(new RulesConfig(), 1, AiDifficulty.Hard, AiDifficulty.Hard, null,
                Filler, new[] { C(9, Suit.Hearts), C(13, Suit.Clubs) }, Filler);
            var easy = Scenario(new RulesConfig(), 1, AiDifficulty.Easy, AiDifficulty.Easy, null,
                Filler, new[] { C(9, Suit.Hearts), C(13, Suit.Clubs) }, Filler);
            var hardSession = TableSession.ForGame(hard, Steady(), 1);
            var easySession = TableSession.ForGame(easy, Steady(), 1);

            hardSession.Tick(1.5f);
            easySession.Tick(1.5f);

            Assert.AreEqual(1, hard.DiscardPile.Count, "Difficile : 1,2 s");
            Assert.AreEqual(0, easy.DiscardPile.Count, "Facile : 3 s");
            easySession.Tick(1.6f);
            Assert.AreEqual(1, easy.DiscardPile.Count);
        }

        [Test]
        public void Human_IsNeverPlayedForAndAiTimerRestartsAfterTheirMove()
        {
            var game = Scenario(new RulesConfig(), 0, AiDifficulty.Normal, AiDifficulty.Normal, null,
                new[] { C(5), C(13) }, new[] { C(9, Suit.Hearts), C(13, Suit.Clubs) }, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Tick(100f);
            Assert.IsTrue(session.IsHumanTurn, "Le joueur prend le temps qu'il veut");
            Assert.AreEqual(0, game.DiscardPile.Count);

            Assert.IsTrue(session.HumanPlay(new[] { C(5) }).Ok);
            Assert.AreEqual(1, game.CurrentPlayerIndex);

            session.Tick(1.9f);
            Assert.AreEqual(1, game.DiscardPile.Count, "Le délai de l'IA repart de zéro");
            session.Tick(0.2f);
            Assert.AreEqual(2, game.DiscardPile.Count);
        }

        [Test]
        public void HumanActions_AreRefusedOutsideTheirTurn_WithoutProducingEvents()
        {
            var game = Scenario(new RulesConfig(), 1, AiDifficulty.Normal, AiDifficulty.Normal, null,
                new[] { C(5), C(13) }, Filler, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            var result = session.HumanPlay(new[] { C(5) });
            Assert.AreEqual(MoveError.NotYourTurn, result.Error);
            Assert.IsEmpty(session.DrainEvents());
        }

        // ---------------------------------------------------------------- Jeu à la volée

        [Test]
        public void JumpIn_CardsStayClickable_UntilTheCurrentPlayerPlays()
        {
            var rules = new RulesConfig { JumpIn = true };
            var game = Scenario(rules, 1, AiDifficulty.Easy, AiDifficulty.Easy, ThreeSevens,
                new[] { C(7, Suit.Spades), C(13) }, new[] { C(9, Suit.Hearts), C(12, Suit.Clubs) }, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            CollectionAssert.AreEqual(new[] { C(7, Suit.Spades) }, session.HumanJumpInCards());
            session.Tick(2.9f);
            CollectionAssert.AreEqual(new[] { C(7, Suit.Spades) }, session.HumanJumpInCards(),
                "Facile : l'IA n'a pas encore posé, la fenêtre reste ouverte");

            session.Tick(0.2f); // l'IA facile pose enfin son 9 : la fenêtre se ferme
            Assert.IsEmpty(session.HumanJumpInCards());
        }

        [Test]
        public void HumanJumpIn_PlaysOutOfTurn_AndTurnResumesAfterTheHuman()
        {
            var rules = new RulesConfig { JumpIn = true };
            var game = Scenario(rules, 1, AiDifficulty.Easy, AiDifficulty.Easy, ThreeSevens,
                new[] { C(7, Suit.Spades), C(13) }, new[] { C(9, Suit.Hearts), C(12, Suit.Clubs) }, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Tick(1.0f);
            var result = session.HumanJumpIn(new[] { C(7, Suit.Spades) });

            Assert.IsTrue(result.Ok);
            Assert.IsTrue(result.Burned, "Le quatrième 7 complète le jeu : la défausse brûle");
            Assert.AreEqual(0, game.DiscardPile.Count);
            Assert.AreEqual(0, game.CurrentPlayerIndex, "Défausse brûlée : le joueur rejoue");
            var events = session.DrainEvents();
            Assert.AreEqual(TableEventKind.JumpedIn, events.Last().Kind);
            Assert.AreEqual(0, events.Last().PlayerIndex);
            Assert.IsEmpty(session.HumanJumpInCards());
        }

        [Test]
        public void HumanJumpIn_WithoutBurn_PassesTheTurnToTheNextPlayerAfterTheHuman()
        {
            var rules = new RulesConfig { JumpIn = true, FourOfAKindBurns = false };
            var game = Scenario(rules, 2, AiDifficulty.Easy, AiDifficulty.Easy, ThreeSevens,
                new[] { C(7, Suit.Spades), C(13) }, Filler, new[] { C(9, Suit.Hearts), C(12, Suit.Clubs) });
            var session = TableSession.ForGame(game, Steady(), 1);

            Assert.IsTrue(session.HumanJumpIn(new[] { C(7, Suit.Spades) }).Ok);
            Assert.AreEqual(1, game.CurrentPlayerIndex, "Le tour reprend après le joueur 0");
            Assert.AreEqual(4, game.DiscardPile.Count);
        }

        [Test]
        public void HumanJumpIn_IsRefusedWhenTheRuleIsOffOrTheValueDiffers()
        {
            var off = Scenario(new RulesConfig { JumpIn = false }, 1, AiDifficulty.Easy, AiDifficulty.Easy,
                ThreeSevens, new[] { C(7, Suit.Spades), C(13) }, Filler, Filler);
            var offSession = TableSession.ForGame(off, Steady(), 1);
            Assert.IsEmpty(offSession.HumanJumpInCards());
            Assert.AreEqual(MoveError.CannotJumpIn, offSession.HumanJumpIn(new[] { C(7, Suit.Spades) }).Error);

            var other = Scenario(new RulesConfig { JumpIn = true }, 1, AiDifficulty.Easy, AiDifficulty.Easy,
                ThreeSevens, new[] { C(8, Suit.Hearts), C(13) }, Filler, Filler);
            var otherSession = TableSession.ForGame(other, Steady(), 1);
            Assert.IsEmpty(otherSession.HumanJumpInCards(), "Pas de 7 en main");

            var early = Scenario(new RulesConfig { JumpIn = true }, 1, AiDifficulty.Easy, AiDifficulty.Easy,
                new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds) }, new[] { C(7, Suit.Spades), C(13) }, Filler, Filler);
            var earlySession = TableSession.ForGame(early, Steady(), 1);
            Assert.IsEmpty(earlySession.HumanJumpInCards(), "Deux 7 seulement : le jeu n'est pas presque complet");
        }

        [Test]
        public void JumpIn_IsAlsoOpenWhileTheHumanIsTheOneWhoMustPlay_ForTheAi()
        {
            // Quatre 7 en vue pour un carré : la Difficile y va dès son temps de réaction, même si c'est au joueur.
            var rules = new RulesConfig { JumpIn = true };
            var game = Scenario(rules, 0, AiDifficulty.Easy, AiDifficulty.Hard,
                new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts) },
                new[] { C(13), C(12) }, Filler, new[] { C(7, Suit.Spades), C(9) });
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Tick(0.5f);
            Assert.AreEqual(3, game.DiscardPile.Count, "Réaction de 0,7 s pas encore écoulée");
            session.Tick(0.3f);
            Assert.AreEqual(0, game.DiscardPile.Count, "Carré complété : défausse brûlée");
            Assert.AreEqual(2, game.CurrentPlayerIndex, "La Difficile rejoue après la brûlure");

            var jump = session.DrainEvents().Single(e => e.Kind == TableEventKind.JumpedIn);
            Assert.AreEqual(2, jump.PlayerIndex);
            Assert.IsTrue(jump.Result.Burned);
        }

        [Test]
        public void EasyAi_NeverJumpsIn()
        {
            var rules = new RulesConfig { JumpIn = true };
            var game = Scenario(rules, 1, AiDifficulty.Hard, AiDifficulty.Easy,
                new[] { C(7, Suit.Clubs), C(7, Suit.Diamonds), C(7, Suit.Hearts) },
                Filler, new[] { C(9, Suit.Hearts), C(12) }, new[] { C(7, Suit.Spades), C(9) });
            var session = TableSession.ForGame(game, Steady(), 1);

            session.Tick(0.9f);
            Assert.IsFalse(session.DrainEvents().Any(e => e.Kind == TableEventKind.JumpedIn));
            Assert.AreEqual(3, game.DiscardPile.Count);
        }

        // ---------------------------------------------------------------- Manches complètes

        private static void PlayHumanTurn(TableSession session, Random rng)
        {
            var game = session.Game;
            var zone = game.GetActiveZone(TableSession.HumanIndex);
            if (zone == Zone.FaceDown)
            {
                session.HumanPlayFaceDown(0);
                return;
            }

            var groups = game.GetPlayableCards(TableSession.HumanIndex).GroupBy(c => c.Rank)
                .OrderBy(g => g.Key).ToList();
            if (groups.Count == 0) session.HumanPickUp();
            else session.HumanPlay(groups[0].ToList());
        }

        [Test]
        public void FullMatch_RunsThroughTheSession_RecordsStats_AndEmitsEvents()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 4, 2);
            settings.Rules.JumpIn = true;
            var book = new StatsBook();
            var match = new Match(settings, 9, book);
            var session = new TableSession(match, TableTimings.Instant(), 9);
            var rng = new Random(9);

            var all = new List<TableEvent>();
            int guard = 0;
            while (!match.IsOver && guard++ < 100)
            {
                session.StartRound();
                Assert.IsTrue(session.HumanReady());

                int steps = 0;
                while (!session.RoundOver && steps++ < 30000)
                {
                    if (session.IsHumanTurn) PlayHumanTurn(session, rng);
                    else session.Tick(0.1f);
                }
                Assert.IsTrue(session.RoundOver, "La manche doit se terminer");
                all.AddRange(session.DrainEvents());
            }

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(2, all.Count(e => e.Kind == TableEventKind.RoundStarted));
            Assert.AreEqual(2, all.Count(e => e.Kind == TableEventKind.PlayStarted));
            Assert.AreEqual(2, all.Count(e => e.Kind == TableEventKind.RoundFinished));
            Assert.IsFalse(all.First(e => e.Kind == TableEventKind.RoundFinished).MatchOver);
            Assert.IsTrue(all.Last(e => e.Kind == TableEventKind.RoundFinished).MatchOver);
            Assert.IsTrue(all.Any(e => e.Kind == TableEventKind.Played));

            Assert.IsTrue(book.Players.All(p => p.Classic.RoundsPlayed == 2));
            Assert.IsTrue(book.Players.All(p => p.Classic.MatchesPlayed == 1));
        }

        [Test]
        public void Round_CannotStartTwice_AndHumanReadyNeedsAStartedSwap()
        {
            var match = new Match(MatchSettings.CreateDefault(GameMode.Classic, 3, 2), 3);
            var session = new TableSession(match, TableTimings.Instant(), 3);
            Assert.IsFalse(session.HumanReady(), "Rien n'est commencé");

            session.StartRound();
            Assert.Throws<InvalidOperationException>(() => session.StartRound());
            Assert.AreEqual(GamePhase.Swap, session.Game.Phase, "En attente de l'échange du joueur");
            Assert.IsTrue(session.HumanReady());
            Assert.AreEqual(GamePhase.Play, session.Game.Phase);
        }

        [Test]
        public void WithoutInitialSwap_TheRoundStartsPlayingImmediately()
        {
            var settings = MatchSettings.CreateDefault(GameMode.Classic, 3, 1);
            settings.Rules.AllowInitialSwap = false;
            var session = new TableSession(new Match(settings, 4), TableTimings.Instant(), 4);

            session.StartRound();
            Assert.AreEqual(GamePhase.Play, session.Game.Phase);
            Assert.IsTrue(session.DrainEvents().Any(e => e.Kind == TableEventKind.PlayStarted));
        }

        [Test]
        public void PickUp_ReportsHowManyCardsWereTaken()
        {
            var game = Scenario(new RulesConfig(), 0, AiDifficulty.Normal, AiDifficulty.Normal,
                new[] { C(14), C(13) }, new[] { C(3), C(4) }, Filler, Filler);
            var session = TableSession.ForGame(game, Steady(), 1);

            var result = session.HumanPickUp();
            Assert.IsTrue(result.Ok);
            Assert.AreEqual(2, result.PickedUpCount);
            Assert.AreEqual(TableEventKind.PickedUp, session.DrainEvents().Single().Kind);
        }
    }
}

using System.Linq;
using CaSentLeSoufre.Core;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class StatsRowsTests
    {
        private static StatsBook Book()
        {
            var book = new StatsBook();
            var max = book.GetOrCreate(new Player("Max", true));
            var azazel = book.GetOrCreate(new Player("Azazel", false, AiDifficulty.Hard));
            var baal = book.GetOrCreate(new Player("Baal", false, AiDifficulty.Easy));
            var lilith = book.GetOrCreate(new Player("Lilith", false, AiDifficulty.Normal));

            // Mode classique : Azazel 3/4, Max 2/4, Baal 0/4 ; Lilith n'a joué qu'en démon.
            Set(max.Classic, rounds: 4, wins: 2, losses: 1, matches: 1, matchWins: 0, matchLosses: 0);
            Set(azazel.Classic, rounds: 4, wins: 3, losses: 0, matches: 1, matchWins: 1, matchLosses: 0);
            Set(baal.Classic, rounds: 4, wins: 0, losses: 3, matches: 1, matchWins: 0, matchLosses: 1);

            Set(max.Demon, rounds: 6, wins: 1, losses: 2, matches: 2, matchWins: 0, matchLosses: 1);
            Set(lilith.Demon, rounds: 6, wins: 4, losses: 0, matches: 2, matchWins: 2, matchLosses: 0);
            return book;
        }

        private static void Set(StatLine line, int rounds, int wins, int losses, int matches, int matchWins,
            int matchLosses)
        {
            line.RoundsPlayed = rounds;
            line.RoundWins = wins;
            line.RoundLosses = losses;
            line.MatchesPlayed = matches;
            line.MatchWins = matchWins;
            line.MatchLosses = matchLosses;
        }

        [Test]
        public void ClassicFilter_ListsOnlyThoseWhoPlayedClassic_BestWinRateFirst()
        {
            var rows = Book().GetRows(GameMode.Classic);

            CollectionAssert.AreEqual(new[] { "Azazel", "Max", "Baal" }, rows.Select(r => r.Player.Name).ToArray());
            Assert.AreEqual(0.75, rows[0].Line.RoundWinRate, 1e-9);
            Assert.AreEqual(0.5, rows[1].Line.RoundWinRate, 1e-9);
            Assert.AreEqual(0.75, rows[2].Line.RoundLossRate, 1e-9);
        }

        [Test]
        public void DemonFilter_ListsOnlyThoseWhoPlayedDemon()
        {
            var rows = Book().GetRows(GameMode.Demon);
            CollectionAssert.AreEqual(new[] { "Lilith", "Max" }, rows.Select(r => r.Player.Name).ToArray());
        }

        [Test]
        public void AllModes_SumsBothModes_ForEachPlayer()
        {
            var rows = Book().GetRows();
            Assert.AreEqual(4, rows.Count);

            var max = rows.Single(r => r.Player.Name == "Max").Line;
            Assert.AreEqual(10, max.RoundsPlayed);
            Assert.AreEqual(3, max.RoundWins);
            Assert.AreEqual(3, max.MatchesPlayed);
            Assert.AreEqual(0.3, max.RoundWinRate, 1e-9);
            Assert.AreEqual(1.0 / 3.0, max.MatchLossRate, 1e-9);
        }

        [Test]
        public void Ties_AreBrokenByRoundsPlayed_ThenByName()
        {
            var book = new StatsBook();
            Set(book.GetOrCreate(new Player("Zed", true)).Classic, 4, 2, 0, 1, 0, 0);
            Set(book.GetOrCreate(new Player("Amy", true)).Classic, 4, 2, 0, 1, 0, 0);
            Set(book.GetOrCreate(new Player("Bob", true)).Classic, 8, 4, 0, 2, 0, 0);

            var names = book.GetRows(GameMode.Classic).Select(r => r.Player.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "Bob", "Amy", "Zed" }, names);
        }

        [Test]
        public void ReturnedLines_AreCopies_AndAnEmptyBookGivesNoRows()
        {
            var book = Book();
            var rows = book.GetRows(GameMode.Classic);
            rows[0].Line.RoundsPlayed = 999;
            Assert.AreEqual(4, book.Find("Azazel", false, AiDifficulty.Hard).Classic.RoundsPlayed);

            Assert.IsEmpty(new StatsBook().GetRows());
            Assert.IsEmpty(new StatsBook().GetRows(GameMode.Demon));
        }

        [Test]
        public void MatchesRanking_IsSeparate_AndSortedOnItsOwnWinRate()
        {
            var book = Book();

            // En classique : Azazel gagne sa partie (1/1), Max et Baal non ; en manches Azazel est aussi premier,
            // mais l'ordre des deux classements est calculé séparément.
            var classic = book.GetRows(GameMode.Classic, StatsScope.Matches);
            Assert.AreEqual("Azazel", classic[0].Player.Name);
            Assert.AreEqual(1.0, classic[0].Line.MatchWinRate, 1e-9);
            Assert.AreEqual(1.0, classic.Single(r => r.Player.Name == "Baal").Line.MatchLossRate, 1e-9);

            // Tous modes : Lilith (2 parties sur 2) devant Azazel (1 sur 1), puis à 0 % Max (3 parties) avant Baal (1).
            var all = book.GetRows(null, StatsScope.Matches);
            CollectionAssert.AreEqual(new[] { "Lilith", "Azazel", "Max", "Baal" },
                all.Select(r => r.Player.Name).ToArray());
        }

        [Test]
        public void MatchesRanking_OmitsThoseWhoOnlyPlayedUnfinishedGames()
        {
            var book = new StatsBook();
            Set(book.GetOrCreate(new Player("Quitte", true)).Classic, 2, 1, 0, 0, 0, 0); // 2 manches, aucune partie finie
            Set(book.GetOrCreate(new Player("Fini", true)).Classic, 3, 1, 1, 1, 0, 0);

            // Manches : « Quitte » gagne 1 sur 2 (50 %), « Fini » 1 sur 3 (33 %).
            CollectionAssert.AreEqual(new[] { "Quitte", "Fini" },
                book.GetRows(GameMode.Classic, StatsScope.Rounds).Select(r => r.Player.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Fini" },
                book.GetRows(GameMode.Classic, StatsScope.Matches).Select(r => r.Player.Name).ToArray());
        }

        [Test]
        public void PlayersWithNothingPlayed_AreOmitted()
        {
            var book = new StatsBook();
            book.GetOrCreate(new Player("Ghost", true));
            Assert.IsEmpty(book.GetRows());
        }
    }
}

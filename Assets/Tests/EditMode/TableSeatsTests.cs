using System.Linq;
using CaSentLeSoufre.Core;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    public class TableSeatsTests
    {
        [Test]
        public void EveryPlayerCount_GetsOneSeatPerPlayer()
        {
            for (int count = RulesConfig.MinPlayers; count <= 10; count++)
                Assert.AreEqual(count, TableSeats.For(count).Length, $"{count} joueurs");
        }

        [Test]
        public void Seats_StayOnScreen_AndNeverOverlapEachOther()
        {
            for (int count = 3; count <= 10; count++)
            {
                var seats = TableSeats.For(count);
                foreach (var s in seats)
                {
                    Assert.GreaterOrEqual(s.Left, -TableSeats.ScreenWidth / 2 - 0.01f, $"{count} joueurs : trop à gauche");
                    Assert.LessOrEqual(s.Right, TableSeats.ScreenWidth / 2 + 0.01f, $"{count} joueurs : trop à droite");
                    Assert.GreaterOrEqual(s.Bottom, -TableSeats.ScreenHeight / 2 - 10f, $"{count} joueurs : trop bas");
                    Assert.LessOrEqual(s.Top, TableSeats.ScreenHeight / 2 + 10f, $"{count} joueurs : trop haut");
                }

                for (int a = 0; a < seats.Length; a++)
                    for (int b = a + 1; b < seats.Length; b++)
                    {
                        bool overlap = seats[a].Left < seats[b].Right - 0.5f && seats[b].Left < seats[a].Right - 0.5f
                                       && seats[a].Bottom < seats[b].Top - 0.5f && seats[b].Bottom < seats[a].Top - 0.5f;
                        Assert.IsFalse(overlap, $"{count} joueurs : les bandes {a} et {b} se chevauchent");
                    }
            }
        }

        [Test]
        public void HumanSeat_IsAlwaysAHorizontalStripAtTheBottom()
        {
            for (int count = 3; count <= 10; count++)
            {
                var human = TableSeats.For(count)[0];
                Assert.IsFalse(human.IsVertical, $"{count} joueurs");
                Assert.Less(human.Y, 0, $"{count} joueurs : en bas de l'écran");
            }
        }

        [Test]
        public void CenterArea_StaysClearOfEverySeat()
        {
            var center = TableSeats.Center;
            for (int count = 3; count <= 10; count++)
                foreach (var s in TableSeats.For(count))
                {
                    bool overlap = center.Left < s.Right && s.Left < center.Right
                                   && center.Bottom < s.Top && s.Bottom < center.Top;
                    Assert.IsFalse(overlap, $"{count} joueurs : le centre empiète sur une bande");
                }
        }

        [Test]
        public void SideColumns_AreVertical_AndStripsHorizontal()
        {
            foreach (var s in TableSeats.For(10))
                Assert.AreEqual(System.Math.Abs(s.X) > 700, s.IsVertical, $"({s.X}, {s.Y})");
        }
    }
}

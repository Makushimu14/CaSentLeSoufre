using System;
using System.Collections.Generic;
using System.Linq;

namespace CaSentLeSoufre.Core.Ai
{
    /// <summary>
    /// Décisions des joueurs IA, selon <see cref="Player.Difficulty"/> :
    /// facile = coups au hasard ; normale = garde ses cartes à pouvoir et ses cartes fortes ;
    /// difficile = idem + enchaîne les brûlures et joue à la volée.
    /// </summary>
    public static class AiBrain
    {
        /// <summary>Valeur « de conservation » d'une carte : celles qui portent un pouvoir sont précieuses.</summary>
        public static int Strength(RulesConfig rules, int rank)
        {
            var power = rules.PowerOf(rank);
            if (power == null) return rank;
            switch (power.Value)
            {
                case PowerType.Burn: return 22;
                case PowerType.Reset: return 21;
                case PowerType.Transparent: return 20;
                case PowerType.ForcePickUp: return 19;
                case PowerType.Skip: return 18;
                case PowerType.Reverse:
                case PowerType.PlayAgain: return 17;
                default: return 16; // LowerOrEqual, SameOrFace
            }
        }

        // ---------------------------------------------------------------- Échange

        /// <summary>Phase d'échange du joueur IA, puis validation. Sans effet si l'échange est désactivé.</summary>
        public static void DoSwap(Game game, int playerIndex, Random rng)
        {
            if (game.Phase != GamePhase.Swap) return;

            var p = game.Players[playerIndex];
            switch (p.Difficulty)
            {
                case AiDifficulty.Easy:
                    if (rng.Next(2) == 0)
                        game.SwapCards(playerIndex, p.Hand[rng.Next(p.Hand.Count)], p.FaceUp[rng.Next(p.FaceUp.Count)]);
                    break;
                case AiDifficulty.Normal:
                    OptimizeSwaps(game, playerIndex, 2);
                    break;
                default:
                    OptimizeSwaps(game, playerIndex, p.FaceUp.Count);
                    break;
            }
            game.SetReady(playerIndex);
        }

        private static void OptimizeSwaps(Game game, int playerIndex, int maxSwaps)
        {
            var rules = game.Rules;
            var p = game.Players[playerIndex];
            for (int i = 0; i < maxSwaps; i++)
            {
                var bestHand = p.Hand.OrderByDescending(c => Strength(rules, c.Rank)).First();
                var worstUp = p.FaceUp.OrderBy(c => Strength(rules, c.Rank)).First();
                if (Strength(rules, bestHand.Rank) <= Strength(rules, worstUp.Rank)) return;
                game.SwapCards(playerIndex, bestHand, worstUp);
            }
        }

        // ---------------------------------------------------------------- Tour de jeu

        /// <summary>Joue un coup pour le joueur courant.</summary>
        public static MoveResult TakeTurn(Game game, Random rng)
        {
            int pi = game.CurrentPlayerIndex;
            var player = game.Players[pi];

            if (game.GetActiveZone(pi) == Zone.FaceDown)
                return game.PlayFaceDown(pi, rng.Next(player.FaceDown.Count));

            var groups = game.GetPlayableCards(pi)
                .GroupBy(c => c.Rank)
                .Select(g => g.ToList())
                .ToList();
            if (groups.Count == 0) return game.PickUpPile(pi);

            switch (player.Difficulty)
            {
                case AiDifficulty.Easy:
                    var pick = groups[rng.Next(groups.Count)];
                    return game.Play(pi, pick.Take(1 + rng.Next(pick.Count)).ToList());
                case AiDifficulty.Normal:
                    return game.Play(pi, groups.OrderBy(g => Strength(game.Rules, g[0].Rank)).First());
                default:
                    return game.Play(pi, ChooseHard(game, groups));
            }
        }

        /// <summary>
        /// Jeu à la volée : tente de jouer hors tour les cartes de même valeur que le dessus de la défausse.
        /// Retourne true si le joueur a joué. Jamais pour une IA facile.
        /// </summary>
        public static bool TryJumpIn(Game game, int playerIndex, Random rng)
        {
            if (!game.Rules.JumpIn || game.Phase != GamePhase.Play
                || playerIndex == game.CurrentPlayerIndex || game.DiscardPile.Count == 0)
                return false;

            var player = game.Players[playerIndex];
            var zone = game.GetActiveZone(playerIndex);
            if (!player.HasCards || zone == Zone.FaceDown) return false;

            int topRank = game.DiscardPile[game.DiscardPile.Count - 1].Rank;
            var matching = (zone == Zone.Hand ? player.Hand : player.FaceUp)
                .Where(c => c.Rank == topRank).ToList();
            if (matching.Count == 0) return false;

            bool wants;
            switch (player.Difficulty)
            {
                case AiDifficulty.Easy:
                    wants = false;
                    break;
                case AiDifficulty.Normal:
                    wants = rng.Next(100) < 30;
                    break;
                default:
                    bool completesFour = game.Rules.FourOfAKindBurns && TopRun(game) + matching.Count >= 4;
                    wants = completesFour || rng.Next(100) < 60;
                    break;
            }

            return wants && game.JumpIn(playerIndex, matching).Ok;
        }

        private static List<Card> ChooseHard(Game game, List<List<Card>> groups)
        {
            var rules = game.Rules;

            // 1. Une brûlure « gratuite » (carré) permet de rejouer aussitôt.
            if (rules.FourOfAKindBurns)
            {
                int run = TopRun(game);
                foreach (var g in groups)
                    if (run > 0 && game.DiscardPile[game.DiscardPile.Count - 1].Rank == g[0].Rank
                        && run + g.Count >= 4)
                        return g;
            }

            // 2. Cartes sans pouvoir d'abord, de la plus faible à la plus forte.
            var ordinary = groups.Where(g => rules.PowerOf(g[0].Rank) == null)
                .OrderBy(g => g[0].Rank).FirstOrDefault();
            if (ordinary != null) return ordinary;

            // 3. Sinon un pouvoir : la brûlure sur une grosse défausse, le moins précieux sinon.
            var powered = groups.OrderBy(g => Strength(rules, g[0].Rank)).ToList();
            var burn = powered.FirstOrDefault(g => rules.PowerOf(g[0].Rank) == PowerType.Burn);
            if (burn != null && game.DiscardPile.Count >= 6) return burn;
            return powered[0];
        }

        /// <summary>Nombre de cartes consécutives de même rang au sommet de la défausse.</summary>
        private static int TopRun(Game game)
        {
            var pile = game.DiscardPile;
            if (pile.Count == 0) return 0;
            int rank = pile[pile.Count - 1].Rank;
            int run = 0;
            for (int i = pile.Count - 1; i >= 0 && pile[i].Rank == rank; i--) run++;
            return run;
        }
    }
}

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
            if (power == null) return Card.Strength(rank);
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
            var cards = ChooseJumpIn(game, playerIndex, rng);
            return cards != null && game.JumpIn(playerIndex, cards).Ok;
        }

        /// <summary>
        /// Décide si le joueur veut jouer à la volée dans la situation actuelle (selon sa difficulté) et avec
        /// quelles cartes. Retourne null s'il ne peut pas ou ne veut pas. Ne joue rien.
        /// </summary>
        public static List<Card> ChooseJumpIn(Game game, int playerIndex, Random rng)
        {
            // Seule la dernière carte d'un jeu complet de même valeur se joue à la volée.
            var cards = game.GetJumpInCards(playerIndex);
            if (cards.Count == 0) return null;

            bool wants;
            switch (game.Players[playerIndex].Difficulty)
            {
                case AiDifficulty.Easy:
                    wants = false;
                    break;
                case AiDifficulty.Normal:
                    wants = rng.Next(100) < 50;
                    break;
                default:
                    wants = true; // la défausse brûle et on rejoue : toujours intéressant
                    break;
            }

            return wants ? cards.Take(1).ToList() : null;
        }

        /// <summary>
        /// Après avoir posé <paramref name="playedRank"/>, le joueur suivant sera-t-il à coup sûr bloqué ? Seul ce qui
        /// est connu de tous compte (cartes visibles, cartes ramassées sur la défausse) : si sa main contient
        /// des cartes inconnues, ou s'il joue à l'aveugle, on ne peut rien affirmer.
        /// </summary>
        private static bool TrapsNextPlayer(Game game, int playedRank)
        {
            int next = game.PeekNextPlayerIndex();
            if (next == game.CurrentPlayerIndex) return false;

            var opponent = game.Players[next];
            IEnumerable<Card> visible;
            switch (game.GetActiveZone(next))
            {
                case Zone.Hand:
                    if (!opponent.HandFullyKnown) return false;
                    visible = opponent.KnownInHand;
                    break;
                case Zone.FaceUp:
                    visible = opponent.FaceUp;
                    break;
                default:
                    return false; // cartes cachées : inconnues
            }

            foreach (var card in visible)
                if (game.CanPlayRankOn(playedRank, card.Rank)) return false;
            return true;
        }

        private static List<Card> ChooseHard(Game game, List<List<Card>> groups)
        {
            var rules = game.Rules;

            // 1. Une brûlure « gratuite » (jeu complet de même valeur) permet de rejouer aussitôt.
            if (rules.FourOfAKindBurns)
            {
                int run = game.TopRunLength();
                foreach (var g in groups)
                    if (run > 0 && game.DiscardPile[game.DiscardPile.Count - 1].Rank == g[0].Rank
                        && run + g.Count >= game.FullSetSize)
                        return g;
            }

            // 2. Cartes sans pouvoir d'abord, de la plus faible à la plus forte (le 2 sans pouvoir est la plus forte).
            //    Si une carte met le joueur suivant dans l'impossibilité de jouer d'après ce que tout le monde sait
            //    de sa main, on prend la plus faible de celles-là : il devra ramasser.
            var ordinary = groups.Where(g => rules.PowerOf(g[0].Rank) == null)
                .OrderBy(g => Strength(rules, g[0].Rank)).ToList();
            if (ordinary.Count > 0)
                return ordinary.FirstOrDefault(g => TrapsNextPlayer(game, g[0].Rank)) ?? ordinary[0];

            // 3. Sinon un pouvoir : la brûlure sur une grosse défausse, le moins précieux sinon.
            var powered = groups.OrderBy(g => Strength(rules, g[0].Rank)).ToList();
            var burn = powered.FirstOrDefault(g => rules.PowerOf(g[0].Rank) == PowerType.Burn);
            if (burn != null && game.DiscardPile.Count >= 6) return burn;
            return powered[0];
        }
    }
}

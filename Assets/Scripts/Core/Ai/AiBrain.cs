using System;
using System.Collections.Generic;
using System.Linq;

namespace CaSentLeSoufre.Core.Ai
{
    /// <summary>
    /// Décisions des joueurs IA, selon <see cref="Player.Difficulty"/> :
    /// facile = coups souvent au hasard ; normale = garde ses cartes à pouvoir et ses cartes fortes ;
    /// difficile = idem + enchaîne les brûlures et joue à la volée.
    /// </summary>
    public static class AiBrain
    {
        /// <summary>
        /// Valeur « de conservation » d'une carte : plus elle est haute, plus on la garde pour plus tard. Une carte
        /// vaut d'abord sa force réelle : les basses cartes sont les plus difficiles à poser, on s'en débarrasse
        /// tant qu'on peut, même si elles portent un pouvoir (un 3 qui fait passer un tour n'en reste pas moins
        /// un 3). Seules la remise à zéro, qui se pose sur tout, et la brûlure, qui fait rejouer, sont des atouts
        /// à garder ; un pouvoir quelconque ajoute juste un petit poids à force égale.
        /// </summary>
        public static int Strength(RulesConfig rules, int rank)
        {
            var power = rules.PowerOf(rank);
            if (power == null) return Card.Strength(rank);
            switch (power.Value)
            {
                case PowerType.Reset: return 30;
                case PowerType.Burn: return 25;
                default: return Card.Strength(rank) + 1;
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
                    // Un débutant : une fois sur deux il joue sa plus faible carte (c'est le réflexe naturel),
                    // sinon n'importe quelle carte jouable. Sans cela les parties entre débutants n'en finissent pas.
                    var pick = rng.Next(2) == 0
                        ? groups.OrderBy(g => Strength(game.Rules, g[0].Rank)).First()
                        : groups[rng.Next(groups.Count)];
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

        /// <summary>Un adversaire qui n'a plus que ce nombre de cartes (au total) est sur le point de sortir.</summary>
        private const int NearlyOutCards = 3;

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

            var ordered = groups.OrderBy(g => Strength(rules, g[0].Rank)).ToList();

            // 2. La brûlure sur une grosse défausse : on la vide et on rejoue.
            var burn = ordered.FirstOrDefault(g => rules.PowerOf(g[0].Rank) == PowerType.Burn);
            if (burn != null && game.DiscardPile.Count >= 6) return burn;

            // 3. Bloquer le suivant n'a de sens que s'il est sur le point de sortir : sinon on dépense une bonne
            //    carte pour rien, et on laisse le joueur d'après en profiter. Alors, et alors seulement, on prend
            //    la plus faible des cartes qui le forcent à ramasser (d'après ce que tout le monde sait de sa main).
            int next = game.PeekNextPlayerIndex();
            if (next != game.CurrentPlayerIndex && game.Players[next].TotalCards <= NearlyOutCards)
            {
                var trap = ordered.FirstOrDefault(g => TrapsNextPlayer(game, g[0].Rank));
                if (trap != null) return trap;
            }

            // 4. Sinon, comme un bon joueur : la carte la plus faible (qu'elle soit en main ou devant soi).
            return ordered[0];
        }
    }
}

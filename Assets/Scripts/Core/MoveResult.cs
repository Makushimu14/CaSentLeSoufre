using System;
using System.Collections.Generic;

namespace CaSentLeSoufre.Core
{
    public enum GamePhase
    {
        Swap,
        Play,
        Finished
    }

    public enum Zone
    {
        Hand,
        FaceUp,
        FaceDown
    }

    public enum MoveError
    {
        None,
        WrongPhase,
        NotYourTurn,
        InvalidCards,
        NotPlayable,
        MustPlay,

        /// <summary>Jeu à la volée refusé : option désactivée, défausse vide, valeur différente, ou c'est votre tour.</summary>
        CannotJumpIn
    }

    public readonly struct MoveResult
    {
        public MoveError Error { get; }
        public bool Ok => Error == MoveError.None;

        /// <summary>La défausse a été brûlée par ce coup.</summary>
        public bool Burned { get; }

        /// <summary>Le joueur a ramassé la défausse.</summary>
        public bool PickedUp { get; }

        /// <summary>Le joueur n'a plus aucune carte : il est sauvé.</summary>
        public bool PlayerFinished { get; }

        /// <summary>Carte aveugle révélée (jeu d'une carte face cachée uniquement).</summary>
        public Card? Revealed { get; }

        /// <summary>Pouvoir porté par la valeur jouée, le cas échéant.</summary>
        public PowerType? Power { get; }

        /// <summary>Joueur contraint de ramasser la défausse par un pouvoir de ramassage forcé.</summary>
        public int? ForcedPickUpPlayer { get; }

        /// <summary>Cartes posées par ce coup (vide pour un ramassage), pour l'affichage et les animations.</summary>
        public IReadOnlyList<Card> Played => _played ?? Array.Empty<Card>();

        private readonly IReadOnlyList<Card> _played;

        /// <summary>Nombre de cartes ramassées par le joueur (ramassage, ou carte aveugle injouable).</summary>
        public int PickedUpCount { get; }

        public MoveResult(MoveError error, bool burned = false, bool pickedUp = false,
            bool playerFinished = false, Card? revealed = null, PowerType? power = null,
            int? forcedPickUpPlayer = null, IReadOnlyList<Card> played = null, int pickedUpCount = 0)
        {
            Error = error;
            Burned = burned;
            PickedUp = pickedUp;
            PlayerFinished = playerFinished;
            Revealed = revealed;
            Power = power;
            ForcedPickUpPlayer = forcedPickUpPlayer;
            _played = played;
            PickedUpCount = pickedUpCount;
        }

        public static MoveResult Fail(MoveError error) => new MoveResult(error);

        public override string ToString() =>
            Ok ? $"Ok(burned={Burned}, pickedUp={PickedUp}, finished={PlayerFinished}, power={Power})"
               : $"Erreur({Error})";
    }
}

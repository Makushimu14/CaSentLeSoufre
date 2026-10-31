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

        public MoveResult(MoveError error, bool burned = false, bool pickedUp = false,
            bool playerFinished = false, Card? revealed = null, PowerType? power = null,
            int? forcedPickUpPlayer = null)
        {
            Error = error;
            Burned = burned;
            PickedUp = pickedUp;
            PlayerFinished = playerFinished;
            Revealed = revealed;
            Power = power;
            ForcedPickUpPlayer = forcedPickUpPlayer;
        }

        public static MoveResult Fail(MoveError error) => new MoveResult(error);

        public override string ToString() =>
            Ok ? $"Ok(burned={Burned}, pickedUp={PickedUp}, finished={PlayerFinished}, power={Power})"
               : $"Erreur({Error})";
    }
}

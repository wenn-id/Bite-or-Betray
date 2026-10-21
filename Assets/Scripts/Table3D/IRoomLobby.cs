namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Main menu gateway to the online lobby (BOB-035). Implementations set CandyTableDirector.Lobby, or simply act as
    /// an active scene component (found when the main menu opens). Without an implementation or while Ready is false,
    /// Create Room and Join Room are gray and cannot be selected.
    /// </summary>
    public interface IRoomLobby
    {
        /// <summary>True when online is ready (packages installed, UGS project linked, signed in).</summary>
        bool Ready { get; }

        /// <summary>
        /// The main menu is already hidden; the lobby takes over the table. To return, call director.OpenMainMenu().
        /// The director catches exceptions from here and displays them as table messages.
        /// </summary>
        void CreateRoom(CandyTableDirector director);

        /// <summary>Same as CreateRoom, for joining with a room code.</summary>
        void JoinRoom(CandyTableDirector director);
    }
}

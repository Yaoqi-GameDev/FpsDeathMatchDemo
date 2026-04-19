namespace FpsDemo.Core
{
    /// <summary>
    /// Set before loading <see cref="LobbyNetSession"/> offline path; cleared when starting host/client.
    /// Gameplay in DeathMatch can branch on <see cref="IsOfflineSession"/> without touching NGO types in core scripts.
    /// </summary>
    public static class GameSessionContext
    {
        public static bool IsOfflineSession { get; private set; }

        public static void SetOfflineSession(bool offline) => IsOfflineSession = offline;

        public static void ClearOfflineSession() => IsOfflineSession = false;
    }
}

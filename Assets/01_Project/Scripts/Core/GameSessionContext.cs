namespace FpsDemo.Core
{
    /// <summary>
    /// Session flags shared across scenes without referencing NGO in gameplay scripts.
    /// Lobby solo / create / join all clear this (<c>false</c>) — they use Host/Client so PlayerPrefab can spawn.
    /// Reserved if a true no-NGO offline path is added later.
    /// </summary>
    public static class GameSessionContext
    {
        public static bool IsOfflineSession { get; private set; }

        public static void SetOfflineSession(bool offline) => IsOfflineSession = offline;

        public static void ClearOfflineSession() => IsOfflineSession = false;
    }
}

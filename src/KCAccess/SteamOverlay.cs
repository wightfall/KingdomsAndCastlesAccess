using System;
using Steamworks;

namespace KCAccess
{
    /// <summary>
    /// Shift+Tab (Steam's default overlay shortcut) opens the Steam overlay over the game. While it is open the
    /// game receives no keys at all, which feels like the mod froze. Steam reports the overlay opening and closing;
    /// the mod announces it, stops acting on keys behind it, and the Windows key fallback stays quiet.
    /// </summary>
    internal static class SteamOverlay
    {
        private static Callback<GameOverlayActivated_t> callback;
        private static bool tried;

        internal static bool Active { get; private set; }

        /// <summary>Registers for Steam's overlay event once Steam is ready (called every frame until it works).</summary>
        internal static void Ensure()
        {
            if (callback != null || tried) return;
            try
            {
                if (!SteamManager.Initialized) return;
                tried = true;
                callback = Callback<GameOverlayActivated_t>.Create(OnOverlay);
                Plugin.Log.LogInfo("Listening for the Steam overlay.");
            }
            catch (Exception e)
            {
                tried = true;
                Plugin.Log.LogWarning("Steam overlay detection unavailable: " + e.Message);
            }
        }

        private static void OnOverlay(GameOverlayActivated_t data)
        {
            Active = data.m_bActive != 0;
            Plugin.Log.LogInfo("[overlay] Steam overlay " + (Active ? "opened" : "closed"));
            if (Active)
            {
                A.Cue(KCAccess.Core.Cue.Open);
                A.Say(KCAccess.Core.Loc.T("Steam overlay opened. It takes the keyboard away from the game. Press Shift Tab or Escape to return. "
                    + "To stop this, turn off the Steam overlay or change its shortcut in Steam settings."), force: true);
            }
            else
            {
                A.Cue(KCAccess.Core.Cue.Close);
                A.Say(KCAccess.Core.Loc.T("Back in the game"), force: true);
            }
        }
    }
}

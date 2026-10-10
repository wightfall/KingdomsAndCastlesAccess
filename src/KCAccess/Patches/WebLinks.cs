using HarmonyLib;
using KCAccess.Core;
using Steamworks;
using UnityEngine;

namespace KCAccess
{
    /// <summary>
    /// Store and workshop links (DLC Available, Wishlist our next game, workshop items and banners) opened in the Steam
    /// overlay, which screen readers cannot read and which only Shift+Tab closes. They open in the web browser instead,
    /// like the game's other links, and say so.
    /// </summary>
    [HarmonyPatch(typeof(SteamFriends), nameof(SteamFriends.ActivateGameOverlayToWebPage))]
    internal static class Patch_OverlayWebPage
    {
        private static bool Prefix(string pchURL)
        {
            if (string.IsNullOrEmpty(pchURL)) return true;
            string host = pchURL;
            try { host = new System.Uri(pchURL).Host.Replace("www.", ""); } catch { }
            ExternalLinks.Leaving(Loc.F("a web page, {0}, in your web browser", host));
            Application.OpenURL(pchURL);
            return false;
        }
    }

    [HarmonyPatch(typeof(SteamFriends), nameof(SteamFriends.ActivateGameOverlayToStore))]
    internal static class Patch_OverlayStore
    {
        private static bool Prefix(AppId_t nAppID)
        {
            ExternalLinks.Leaving(Loc.T("the Steam store page in your web browser"));
            Application.OpenURL("https://store.steampowered.com/app/" + nAppID.m_AppId + "/");
            return false;
        }
    }
}

using System.Collections.Generic;
using I2.Loc;
using KCAccess.Core;
using KCAccess.UI;
using UnityEngine;

namespace KCAccess.Game
{
    /// <summary>
    /// A merchant ship's trade window (MerchantUI). The game answers a purchase or a sale only with a sound, refuses a
    /// purchase it cannot pay for with another sound, shows the totals as bare numbers, and the line saying where the
    /// ship comes from is overwritten by the window title. Says what changed hands, why a purchase is refused, the totals
    /// with the gold there is, and where the ship comes from.
    /// </summary>
    internal static class Trade
    {
        private static MerchantUI WindowOf(UIItem item)
        {
            var w = GameUI.inst != null ? GameUI.inst.merchantUI : null;
            if (w == null || w.ship == null || item == null || item.Go == null || !w.gameObject.activeInHierarchy) return null;
            return item.Go.transform.IsChildOf(w.transform) ? w : null;
        }

        private static int Gold() => Player.inst != null && Player.inst.PlayerLandmassOwner != null ? Player.inst.PlayerLandmassOwner.Gold : 0;

        /// <summary>The gold the order lines under content add up to, and what they order ("11 tools, 3 wood").</summary>
        private static int Order(Transform content, out string goods)
        {
            int cost = 0;
            var parts = new List<string>();
            for (int i = 0; content != null && i < content.childCount; i++)
            {
                var row = content.GetChild(i).GetComponent<ResourceLineItemUI>();
                if (row == null) continue;
                cost += row.GetCost();
                int n = row.GetOrderAmount();
                if (n > 0) parts.Add(n + " " + ResourceNames.Name(row.rtype.ToString()));
            }
            goods = string.Join(", ", parts.ToArray());
            return cost;
        }

        private static bool Shows(UIItem item, TMPro.TMP_Text text) =>
            text != null && (item.Go == text.gameObject || (item.Texts != null && item.Texts.Contains(text)));

        /// <summary>The game writes "from {kingdom}" there, then the line's own term puts the title back ("Merchant Ship" twice).</summary>
        private static string From(MerchantUI w)
        {
            var kingdom = AIBrainsContainer.inst != null ? AIBrainsContainer.inst.GetKingdomFromTeamId(w.ship.TeamID()) : null;
            return TextUtil.Clean(kingdom != null ? string.Format(ScriptLocalization.ShipFromKingOrQueen, kingdom.GetLocalizedName()) : ScriptLocalization.ShipFromBeyondTheSea);
        }

        /// <summary>An upgrade's name: the game's text is "name - what it does" (" — " or ": " in some languages).</summary>
        private static string UpgradeShortName(Player.UpgradeType upgrade)
        {
            string full = Special.UpgradeName(upgrade);
            int cut = -1;
            foreach (var sep in new[] { " - ", " — ", " – ", ": " })
            {
                int i = full.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 0 && (cut < 0 || i < cut)) cut = i;
            }
            return cut > 0 ? full.Substring(0, cut) : full;
        }

        private static int UpgradeCost(MerchantUI w) =>
            w.upgradeDescs != null && w.upgradeIdx >= 0 && w.upgradeIdx < w.upgradeDescs.Count ? w.upgradeDescs[w.upgradeIdx].cost : 0;

        internal static string Describe(UIItem item)
        {
            var w = WindowOf(item);
            if (w == null) return null;
            if (item.IsControl && item.Control == w.closeBtn) return Loc.T("Close, button");
            if (w.fromTxt != null && item.Go == w.fromTxt.gameObject) return From(w);
            if (Shows(item, w.buyTotal)) return Loc.F("Total cost: {0} gold, you have {1}", Order(w.buyContent, out _), Gold());
            if (Shows(item, w.sellTotal)) return Loc.F("Total earnings: {0} gold", Order(w.sellContent, out _));
            if (Shows(item, w.upgradeCost)) return Loc.F("Cost: {0} gold, you have {1}", UpgradeCost(w), Gold());
            if (item.IsControl && (item.Control == w.buyCompleteTransaction || item.Control == w.sellCompleteTransaction))
            {
                Order(item.Control == w.buyCompleteTransaction ? w.buyContent : w.sellContent, out string goods);
                return TextUtil.Join(", ", UIText.LabelOf(item.Control), UIText.ButtonRole, goods.Length > 0 ? goods : Loc.T("no amounts chosen yet"));
            }
            return null;
        }

        /// <summary>Enter on a complete-transaction button: refuses what cannot be paid for, else clicks and says what changed hands.</summary>
        internal static bool HandleKey(UIItem item)
        {
            var w = WindowOf(item);
            if (w == null || !item.IsControl || !(KInput.Plain(KeyCode.Return) || KInput.Plain(KeyCode.KeypadEnter) || KInput.Plain(KeyCode.Space))) return false;
            bool buy = item.Control == w.buyCompleteTransaction, sell = item.Control == w.sellCompleteTransaction, special = item.Control == w.specialCompleteTransaction;
            if (!buy && !sell && !special) return false;
            KInput.Consume(KeyCode.Return);
            KInput.Consume(KeyCode.KeypadEnter);
            KInput.Consume(KeyCode.Space);
            int gold = Gold();
            if (special)
            {
                int price = UpgradeCost(w);
                if (price > gold)
                {
                    A.Cue(Cue.Error);
                    A.Say(Loc.F("Not enough gold: costs {0}, you have {1}", price, gold), force: true);
                    return true;
                }
                string upgrade = UpgradeShortName(w.ship.GetSpecialUpgrade());
                UINavigator.Click(item.Go);
                A.Cue(Cue.Activate);
                A.Say(Loc.F("Bought {0} for {1} gold, {2} gold left", upgrade, price, Gold()), force: true);
                return true;
            }
            int total = Order(buy ? w.buyContent : w.sellContent, out string goods);
            if (goods.Length == 0)
            {
                A.Cue(Cue.Error);
                A.Say(buy ? Loc.T("Choose how much to buy first") : Loc.T("Choose how much to sell first"), force: true);
                return true;
            }
            if (buy && total > gold)
            {
                A.Cue(Cue.Error);
                A.Say(Loc.F("Not enough gold: costs {0}, you have {1}", total, gold), force: true);
                return true;
            }
            UINavigator.Click(item.Go);
            A.Cue(Cue.Activate);
            A.Say(buy ? Loc.F("Bought {0} for {1} gold, {2} gold left", goods, total, Gold()) : Loc.F("Sold {0} for {1} gold, you now have {2} gold", goods, total, Gold()), force: true);
            return true;
        }
    }
}

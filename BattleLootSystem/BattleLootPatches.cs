using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace RpgMod1.BattleLootSystem
{
    [HarmonyPatch(typeof(MapEvent), "OnBattleWon")]
    public class BattleLootPatches
    {
        // Оставляем только ОДИН метод Postfix
        [HarmonyPostfix]
        public static void Postfix(MapEvent __instance)
        {
            if (__instance == null || __instance.WinningSide == BattleSideEnum.None) return;

            var winnerSide = __instance.GetMapEventSide(__instance.WinningSide);
            var loserSide = __instance.GetMapEventSide(__instance.WinningSide.GetOppositeSide());

            PartyBase mainWinner = winnerSide.LeaderParty;
            if (mainWinner == null) return;

            // 1. Возврат своим
            foreach (var winnerPartyInfo in winnerSide.Parties)
            {
                var issuedRoster = BattleEquipmentTracker.GetIssuedRoster(__instance, winnerPartyInfo.Party.Id.ToString());
                if (issuedRoster != null)
                {
                    mainWinner.ItemRoster.Add(issuedRoster);
                }
            }

            // 2. Сбор с проигравших
            foreach (var loserPartyInfo in loserSide.Parties)
            {
                var issuedRoster = BattleEquipmentTracker.GetIssuedRoster(__instance, loserPartyInfo.Party.Id.ToString());
                if (issuedRoster != null)
                {
                    mainWinner.ItemRoster.Add(issuedRoster);
                }

                if (loserPartyInfo.Party.ItemRoster != null && loserPartyInfo.Party.ItemRoster.Count > 0)
                {
                    mainWinner.ItemRoster.Add(loserPartyInfo.Party.ItemRoster);
                    loserPartyInfo.Party.ItemRoster.Clear();
                }
            }

            BattleEquipmentTracker.ClearBattleData(__instance);
        }
    }
}
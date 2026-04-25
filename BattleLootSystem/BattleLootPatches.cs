using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RpgMod1.BattleLootSystem
{
    [HarmonyPatch(typeof(MapEvent))]
    public class BattleLootPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("LootDefeatedPartyItems")]
        [HarmonyPatch("LootDefeatedPartyCasualties")]
        public static bool Prefix() => false;

        [HarmonyPostfix]
        [HarmonyPatch("OnBattleWon")]
        public static void Postfix(MapEvent __instance)
        {
            if (__instance == null || __instance.WinningSide == BattleSideEnum.None) return;

            var winnerSide = __instance.GetMapEventSide(__instance.WinningSide);
            var loserSide = __instance.GetMapEventSide(__instance.WinningSide.GetOppositeSide());
            if (winnerSide == null || loserSide == null) return;

            // Находим объект MapEventParty игрока
            MapEventParty playerBattleParty = winnerSide.Parties.FirstOrDefault(x => x.Party == PartyBase.MainParty);

            // 1. Возврат своего
            foreach (var winnerPartyInfo in winnerSide.Parties)
            {
                // ИСПРАВЛЕНО: добавлено .ToString()
                var issued = BattleEquipmentTracker.GetIssuedRoster(__instance, winnerPartyInfo.Party.Id.ToString());
                if (issued != null && issued.Count > 0)
                {
                    if (winnerPartyInfo.Party == PartyBase.MainParty && MilitaryDepotBehavior.DepotParty != null)
                        MilitaryDepotBehavior.DepotParty.ItemRoster.Add(issued);
                    else
                        winnerPartyInfo.Party.ItemRoster.Add(issued);
                }
            }

            // 2. Сбор общего котла
            ItemRoster totalLootContainer = new ItemRoster();
            foreach (var loserPartyInfo in loserSide.Parties)
            {
                // ИСПРАВЛЕНО: добавлено .ToString()
                var issuedToLoser = BattleEquipmentTracker.GetIssuedRoster(__instance, loserPartyInfo.Party.Id.ToString());
                if (issuedToLoser != null) totalLootContainer.Add(issuedToLoser);

                if (loserPartyInfo.Party.ItemRoster != null)
                {
                    totalLootContainer.Add(loserPartyInfo.Party.ItemRoster);
                    loserPartyInfo.Party.ItemRoster.Clear();
                }
            }

            if (totalLootContainer.Count == 0)
            {
                BattleEquipmentTracker.ClearBattleData(__instance);
                return;
            }

            // 3. Расчет долей
            int totalWinnerTroops = winnerSide.Parties.Sum(p => p.Party.NumberOfAllMembers);
            if (totalWinnerTroops <= 0) return;

            var sortedWinners = winnerSide.Parties.OrderByDescending(p => p.Party.NumberOfAllMembers).ToList();

            // Очищаем ростер окна игрока
            playerBattleParty?.RosterToReceiveLootItems?.Clear();

            // 4. Распределение
            for (int i = 0; i < totalLootContainer.Count; i++)
            {
                ItemRosterElement element = totalLootContainer.GetElementCopyAtIndex(i);
                int remainingAmount = element.Amount;

                foreach (var winnerInfo in sortedWinners)
                {
                    if (remainingAmount <= 0) break;

                    int amountToGive = (int)Math.Round((double)element.Amount * winnerInfo.Party.NumberOfAllMembers / totalWinnerTroops);
                    
                    // ИСПРАВЛЕНО: Отдаем остаток последнему в цикле, если насчитали лишнего
                    if (winnerInfo == sortedWinners.Last() || amountToGive > remainingAmount) 
                        amountToGive = remainingAmount;

                    if (amountToGive > 0)
                    {
                        if (winnerInfo == playerBattleParty)
                            winnerInfo.RosterToReceiveLootItems.AddToCounts(element.EquipmentElement, amountToGive);
                        else
                            winnerInfo.Party.ItemRoster.AddToCounts(element.EquipmentElement, amountToGive);
                        
                        remainingAmount -= amountToGive;
                    }
                }

                // ДОПОЛНИТЕЛЬНО: Если из-за округления вниз остался 1 предмет, отдаем его лидеру (первому в списке)
                if (remainingAmount > 0)
                {
                    var leader = sortedWinners[0];
                    if (leader == playerBattleParty)
                        leader.RosterToReceiveLootItems.AddToCounts(element.EquipmentElement, remainingAmount);
                    else
                        leader.Party.ItemRoster.AddToCounts(element.EquipmentElement, remainingAmount);
                }
            }

            BattleEquipmentTracker.ClearBattleData(__instance);
        }
    }
}
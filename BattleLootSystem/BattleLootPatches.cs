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
    // Патч для блокировки ванильной генерации лута
    [HarmonyPatch(typeof(MapEvent))]
    public class VanillaLootBlocker
    {
        [HarmonyPrefix]
        [HarmonyPatch("LootDefeatedPartyItems")]
        [HarmonyPatch("LootDefeatedPartyCasualties")]
        static bool Prefix()
        {
            // Возвращаем false, чтобы оригинальные методы игры НЕ выполнялись
            return false;
        }
    }

    [HarmonyPatch(typeof(MapEvent), "OnBattleWon")]
    public class BattleLootPatches
    {
        [HarmonyPostfix]
        public static void Postfix(MapEvent __instance)
        {
            if (__instance == null || __instance.WinningSide == BattleSideEnum.None) return;

            var winnerSide = __instance.GetMapEventSide(__instance.WinningSide);
            var loserSide = __instance.GetMapEventSide(__instance.WinningSide.GetOppositeSide());

            // 1. СОБСТВЕННЫЙ ВОЗВРАТ (Победители забирают своё)
            foreach (var winnerPartyInfo in winnerSide.Parties)
            {
                //var issued = BattleEquipmentTracker.GetIssuedRoster(__instance, winnerPartyInfo.Party.Id.ToString());
                var issued = BattleEquipmentTracker.GetIssuedRoster(__instance, winnerPartyInfo.Party.Id);

                if (issued != null && issued.Count > 0)
                {


                    // Если это игрок - возвращаем вещи на СКЛАД
                    if (winnerPartyInfo.Party == PartyBase.MainParty && MilitaryDepotBehavior.DepotParty != null)
                    {
                        MilitaryDepotBehavior.DepotParty.ItemRoster.Add(issued);
                    }
                    else

                    {
                        winnerPartyInfo.Party.ItemRoster.Add(issued);
                    }
                }
            }

            // 2. СБОР ОБЩЕГО КОТЛА ТРОФЕЕВ
            ItemRoster totalLootContainer = new ItemRoster();
            foreach (var loserPartyInfo in loserSide.Parties)
            {
                // Забираем элитку из трекера
                // var issuedToLoser = BattleEquipmentTracker.GetIssuedRoster(__instance, loserPartyInfo.Party.Id.ToString());

                var issuedToLoser = BattleEquipmentTracker.GetIssuedRoster(__instance, loserPartyInfo.Party.Id);
                
                if (issuedToLoser != null) totalLootContainer.Add(issuedToLoser);

                // Забираем всё из инвентаря (коровы, зерно, шмотки)
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

            // 3. РАСЧЕТ ДОЛЕЙ (По количеству "мяса")
            int totalWinnerTroops = winnerSide.Parties.Sum(p => p.Party.NumberOfAllMembers);
            if (totalWinnerTroops <= 0) return;

            // Сортируем победителей по убыванию силы, чтобы остатки (округление) забирал сильнейший
            var sortedWinners = winnerSide.Parties
                .OrderByDescending(p => p.Party.NumberOfAllMembers)
                .ToList();

            // 4. ДЕЛЕЖКА ПИРОГА
            // Перебираем каждый тип предмета в общем котле
            for (int i = 0; i < totalLootContainer.Count; i++)
            {
                ItemRosterElement element = totalLootContainer.GetElementCopyAtIndex(i);
                int remainingAmount = element.Amount;

                foreach (var winnerInfo in sortedWinners)
                {
                    if (remainingAmount <= 0) break;

                    // Считаем долю: (Кол-во предметов * Юниты отряда) / Всего юнитов
                    // Используем double для точности, затем округляем
                    double share = (double)element.Amount * winnerInfo.Party.NumberOfAllMembers / totalWinnerTroops;
                    int amountToGive = (int)Math.Round(share);

                    // Если это последний (самый крупный) или единственный отряд, отдаем остаток
                    if (winnerInfo == sortedWinners.Last() || amountToGive > remainingAmount)
                    {
                        amountToGive = remainingAmount;
                    }

                    if (amountToGive > 0)
                    {
                        winnerInfo.Party.ItemRoster.AddToCounts(element.EquipmentElement, amountToGive);
                        remainingAmount -= amountToGive;
                    }
                }

                // Если после округлений что-то осталось (например, 1 меч на 3 равных отряда), 
                // отдаем самому крупному (он первый в списке)
                if (remainingAmount > 0)
                {
                    sortedWinners[0].Party.ItemRoster.AddToCounts(element.EquipmentElement, remainingAmount);
                }
            }

            // 5. ОЧИСТКА
            BattleEquipmentTracker.ClearBattleData(__instance);
        }
    }
}
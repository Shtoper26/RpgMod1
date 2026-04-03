using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1
{
    public static class MilitaryDepotCache
    {
        // План привязан к конкретному CharacterObject, чтобы при спавне выдать вещь нужному типу юнита
        // Используем Queue (очередь), чтобы выдавать по одному предмету из заранее подготовленного списка для каждого типа
        private static Dictionary<CharacterObject, Queue<Equipment>> AssignmentPlan = new Dictionary<CharacterObject, Queue<Equipment>>();

        public static void Clear()
        {
            AssignmentPlan.Clear();
        }

        public static void CreateBattlePlan(ItemRoster depotInventory, TroopRoster memberRoster)
        {
            Clear();
            if (depotInventory == null || memberRoster == null) return;

            ItemRoster simRoster = new ItemRoster(depotInventory);
            int issuedCount = 0; // Счетчик для итогового отчета

            // 1. Собираем всех солдат
            List<CharacterObject> allTroops = new List<CharacterObject>();
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var element = memberRoster.GetElementCopyAtIndex(i);
                if (element.Character.IsHero) continue;
                for (int n = 0; n < element.Number; n++) { allTroops.Add(element.Character); }
            }

            // 2. Сортируем (Ветераны первые)
            var sortedTroops = allTroops.OrderByDescending(t => t.Tier).ToList();

            // 3. Распределяем
            foreach (var character in sortedTroops)
            {
                CharacterObject firstTier = MilitaryDepotLogic.GetFirstTierCharacter(character);
                Equipment finalEquip = new Equipment();
                bool hasChanges = false;
                string itemsLog = ""; // Строка для сбора названий выданных вещей

                for (EquipmentIndex slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Cape; slot++)
                {
                    EquipmentElement best = (slot <= EquipmentIndex.Weapon3)
                        ? MilitaryDepotActions.ExtractBestWeaponForSlot(character.FirstBattleEquipment[slot], simRoster)
                        : MilitaryDepotActions.ExtractBestForSlotInSim(slot, simRoster);

                    if (!best.IsEmpty)
                    {
                        finalEquip[slot] = best;
                        hasChanges = true;
                        itemsLog += (itemsLog == "" ? "" : ", ") + best.Item.Name.ToString();
                    }
                    else if (firstTier != null)
                    {
                        finalEquip[slot] = firstTier.FirstBattleEquipment[slot];
                    }
                }

                if (hasChanges)
                {
                    if (!AssignmentPlan.ContainsKey(character)) AssignmentPlan[character] = new Queue<Equipment>();
                    AssignmentPlan[character].Enqueue(finalEquip);
                    issuedCount++;

                    // ВЫВОД ЛОГА: Теперь мы видим, что зарезервировано для конкретного типа юнита
                    // Чтобы не спамить 500 строк, выводим только если выдано что-то ценное
                    MilitaryDepotLogs.LogWeaponIssued(character.Name.ToString(), itemsLog);
                }
            }

            // Итоговое сообщение в чат
            MilitaryDepotLogs.LogTransferComplete(issuedCount);
        }

        // Метод для получения экипировки при спавне
        public static Equipment GetPredefinedEquipment(CharacterObject character)
        {
            if (AssignmentPlan.TryGetValue(character, out Queue<Equipment> queue) && queue.Count > 0)
            {
                return queue.Dequeue();
            }
            return null;
        }
    }
}
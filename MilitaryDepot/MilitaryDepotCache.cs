using RpgMod1.BattleLootSystem;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace RpgMod1
{
    public static class MilitaryDepotCache
    {
        private static Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>> GlobalPlan =
            new Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>>();

        private static Dictionary<MobileParty, Dictionary<CharacterObject, Equipment>> FallbackSamples =
            new Dictionary<MobileParty, Dictionary<CharacterObject, Equipment>>();

        public static void Clear()
        {
            GlobalPlan.Clear();
            FallbackSamples.Clear();
        }

        public static void CreateBattlePlan(MobileParty party, ItemRoster inventory)
        {
            if (party?.MemberRoster == null || inventory == null) return;

            ItemRoster simRoster = new ItemRoster(inventory);

            if (!GlobalPlan.ContainsKey(party))
                GlobalPlan[party] = new Dictionary<CharacterObject, Queue<Equipment>>();

            if (!FallbackSamples.ContainsKey(party))
                FallbackSamples[party] = new Dictionary<CharacterObject, Equipment>();

            // ПРАВИЛО 1: Сортировка по уровню
            var sortedTroops = party.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level)
                .ToList();

            // --- НОВОЕ: Считаем общее количество всадников для резервирования копий ---
            int remainingCavalryNeeds = sortedTroops.Where(t => t.Character.IsMounted).Sum(t => t.Number);

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero) continue;

                if (!GlobalPlan[party].ContainsKey(character))
                    GlobalPlan[party][character] = new Queue<Equipment>();

                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment finalEquip = new Equipment();
                    Equipment troopEquip = character.FirstBattleEquipment;

                    // ПУНКТ 6: Переносим лошадь из шаблона
                    finalEquip[EquipmentIndex.Horse] = troopEquip[EquipmentIndex.Horse];

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
{
    if (s == EquipmentIndex.Horse) continue;

    ItemObject maskItem = troopEquip[s].Item;
    EquipmentElement bestFromDepot = (s <= EquipmentIndex.Weapon3)
        ? MilitaryDepotActions.ExtractBestWeaponForSlot(maskItem, simRoster, character, finalEquip, remainingCavalryNeeds)
        : MilitaryDepotActions.ExtractBestArmorForSlot(s, simRoster, character);

    EquipmentElement finalElement = EquipmentElement.Invalid;
    
    if (referenceUnit != null)
    {
        ItemObject refItem = referenceUnit.FirstBattleEquipment[s].Item;
        float refPower = MilitaryDepotLogic.GetItemPower(refItem, s);
        float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item, s);

        // Если со склада пришло что-то уровня 2 или 3, и оно мощнее эталона
        if (!bestFromDepot.IsEmpty && depotPower >= refPower)
        {
            finalElement = bestFromDepot;
            inventory.AddToCounts(bestFromDepot, -1);
            var activeEvent = party.MapEvent ?? TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
            if (activeEvent != null) BattleEquipmentTracker.RegisterIssuedEquipment(activeEvent, party.Party.Id, bestFromDepot.Item, 1);
        }
        else
        {
            // Откат к эталону (Тир 1 или 0)
            if (refItem != null && !MilitaryDepotActions.IsDuplicateUniqueItem(refItem, finalEquip))
                finalElement = new EquipmentElement(refItem);
        }
    }

    // Запасной вариант из родного шаблона
    if (finalElement.IsEmpty && troopEquip[s].Item != null)
    {
        if (!MilitaryDepotActions.IsDuplicateUniqueItem(troopEquip[s].Item, finalEquip))
            finalElement = troopEquip[s];
    }

    finalEquip[s] = finalElement;
}

                    GlobalPlan[party][character].Enqueue(finalEquip);
                    if (!FallbackSamples[party].ContainsKey(character))
                        FallbackSamples[party][character] = finalEquip;

                    // Уменьшаем счетчик нужд кавалерии после обработки всадника
                    if (character.IsMounted) remainingCavalryNeeds--;
                }
            }
        }

        public static Equipment GetPredefinedEquipment(MobileParty party, CharacterObject character)
        {
            if (party == null || character == null || character.IsHero) return null;

            if (GlobalPlan.TryGetValue(party, out var partyPlans))
            {
                if (partyPlans.TryGetValue(character, out var queue) && queue.Count > 0)
                    return queue.Dequeue();

                if (FallbackSamples.TryGetValue(party, out var partySamples))
                {
                    if (partySamples.TryGetValue(character, out var sample))
                        return sample;
                }
            }

            CharacterObject reference = MilitaryDepotLogic.GetReferenceUnit(character);
            return reference?.FirstBattleEquipment;
        }

        public static EquipmentElement GetBaseHorseHarness(CharacterObject character)
        {
            CharacterObject reference = MilitaryDepotLogic.GetReferenceUnit(character);
            if (reference != null && reference.FirstBattleEquipment[EquipmentIndex.HorseHarness].Item != null)
                return reference.FirstBattleEquipment[EquipmentIndex.HorseHarness];
            
            return EquipmentElement.Invalid;
        }
    }
}
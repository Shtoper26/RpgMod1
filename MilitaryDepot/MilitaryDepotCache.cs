using RpgMod1.BattleLootSystem;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

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

            var sortedTroops = party.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level)
                .ToList();

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

                    // --- ИСПРАВЛЕНИЕ: ПРАВИЛЬНЫЙ ВЫБОР РАНДОМНОГО ШАБЛОНА ---
                    Equipment selectedRefTemplate = null;
                    if (referenceUnit != null)
                    {
                        // Преобразуем в список, чтобы избежать ошибки "группа методов"
                        var allTemplates = referenceUnit.BattleEquipments.ToList();
                        if (allTemplates.Count > 0)
                        {
                            selectedRefTemplate = allTemplates[MBRandom.RandomInt(allTemplates.Count)];
                        }
                    }

                    // Пункт 6: Коня всегда берем из родного шаблона
                    finalEquip[EquipmentIndex.Horse] = troopEquip[EquipmentIndex.Horse];

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = troopEquip[s].Item;
                        
                        // Подбор лучшего со склада (с учетом резерва для кавалерии и защиты от дублей)
                        EquipmentElement bestFromDepot = (s <= EquipmentIndex.Weapon3)
                            ? MilitaryDepotActions.ExtractBestWeaponForSlot(maskItem, simRoster, character, finalEquip, remainingCavalryNeeds)
                            : MilitaryDepotActions.ExtractBestArmorForSlot(s, simRoster, character);

                        EquipmentElement finalElement = EquipmentElement.Invalid;
                        
                        // Если есть рандомный шаблон эталона
                        if (selectedRefTemplate != null)
                        {
                            ItemObject refItem = selectedRefTemplate[s].Item;
                            
                            // Сравниваем "силу" складского предмета и предмета из рандомного шаблона
                            float refPower = MilitaryDepotLogic.GetItemPower(refItem, s);
                            float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item, s);

                            if (!bestFromDepot.IsEmpty && depotPower >= refPower)
                            {
                                finalElement = bestFromDepot;
                                
                                // УДАЛЯЕМ из инвентаря, чтобы не было дублей
                                inventory.AddToCounts(bestFromDepot, -1);
                                
                                var activeEvent = party.MapEvent ?? TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
                                if (activeEvent != null)
                                {
                                    BattleEquipmentTracker.RegisterIssuedEquipment(activeEvent, party.Party.Id, bestFromDepot.Item, 1);
                                }
                            }
                            else if (refItem != null)
                            {
                                // Если на складе нет или оно хуже - берем из выбранного шаблона (проверяя на дубли щитов/луков)
                                if (!MilitaryDepotActions.IsDuplicateUniqueItem(refItem, finalEquip))
                                {
                                    finalElement = new EquipmentElement(refItem);
                                }
                            }
                        }

                        // Если всё еще пусто (например, в шаблоне эталона в этом слоте ничего нет)
                        if (finalElement.IsEmpty && troopEquip[s].Item != null)
                        {
                            if (!MilitaryDepotActions.IsDuplicateUniqueItem(troopEquip[s].Item, finalEquip))
                            {
                                finalElement = troopEquip[s];
                            }
                        }

                        finalEquip[s] = finalElement;
                    }

                    GlobalPlan[party][character].Enqueue(finalEquip);
                    if (!FallbackSamples[party].ContainsKey(character))
                        FallbackSamples[party][character] = finalEquip;

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
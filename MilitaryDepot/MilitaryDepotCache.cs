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
            string partyId = party.Party.Id.ToString();

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

                    Equipment selectedRefTemplate = null;
                    if (referenceUnit != null)
                    {
                        var allTemplates = referenceUnit.BattleEquipments.ToList();
                        if (allTemplates.Count > 0)
                            selectedRefTemplate = allTemplates[MBRandom.RandomInt(allTemplates.Count)];
                    }

                    finalEquip[EquipmentIndex.Horse] = troopEquip[EquipmentIndex.Horse];

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = troopEquip[s].Item;
                        
                        EquipmentElement bestFromDepot = (s <= EquipmentIndex.Weapon3)
                            ? MilitaryDepotActions.ExtractBestWeaponForSlot(maskItem, simRoster, character, finalEquip, remainingCavalryNeeds)
                            : MilitaryDepotActions.ExtractBestArmorForSlot(s, simRoster, character);

                        EquipmentElement finalElement = EquipmentElement.Invalid;
                        
                        if (selectedRefTemplate != null)
                        {
                            ItemObject refItem = selectedRefTemplate[s].Item;
                            
                            float refPower = (refItem != null) ? MilitaryDepotLogic.GetItemPower(refItem, s) : -1f;
                            float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item, s);

                            // А. Если со склада лучше (или равно) чем в шаблоне - берем со склада
                            if (!bestFromDepot.IsEmpty && depotPower >= refPower)
                            {
                                finalElement = bestFromDepot;
                                inventory.AddToCounts(bestFromDepot, -1);
                                
                                var activeEvent = party.MapEvent ?? TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
                                if (activeEvent != null)
                                    BattleEquipmentTracker.RegisterIssuedEquipment(activeEvent, partyId, bestFromDepot.Item, 1);
                            }
                            // Б. Иначе берем из шаблона эталона (даже если юнит 5 тира, но склад пуст)
                            else if (refItem != null)
                            {
                                if (!MilitaryDepotActions.IsDuplicateUniqueItem(refItem, finalEquip))
                                {
                                    finalElement = new EquipmentElement(refItem);
                                    
                                    // НОВОЕ: Если юнит низкого тира, регистрируем его шаблонное оружие как трофей
                                    // Вместо: if (character.Tier <= 1 && refItem.IsWeapon)
                                     //if (character.Tier <= 1 && refItem.WeaponComponent != null)
                                     if (refItem.WeaponComponent != null)
                                    {
                                        var activeEvent = party.MapEvent ?? TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
                                        if (activeEvent != null)
                                            BattleEquipmentTracker.RegisterTemplateEquipment(activeEvent, partyId, refItem, 1);
                                    }
                                }
                            }
                        }

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
            return MilitaryDepotLogic.GetReferenceUnit(character)?.FirstBattleEquipment;
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
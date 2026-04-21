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

            // Копия для симуляции, чтобы распределять вещи между солдатами
            ItemRoster simRoster = new ItemRoster(inventory);

            if (!GlobalPlan.ContainsKey(party))
                GlobalPlan[party] = new Dictionary<CharacterObject, Queue<Equipment>>();

            if (!FallbackSamples.ContainsKey(party))
                FallbackSamples[party] = new Dictionary<CharacterObject, Equipment>();

            // ПУНКТ 1: Сортировка от элиты к рекрутам
            var sortedTroops = party.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level);

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                // ПУНКТ 9: Теперь обрабатываем и Тир 0/1 (убрали пропуск Tier <= 1)
                if (character == null || character.IsHero) continue;

                if (!GlobalPlan[party].ContainsKey(character))
                    GlobalPlan[party][character] = new Queue<Equipment>();

                // ПУНКТ 8-9: Получаем эталон (Тир 1 для профи, Тир 0 для новичков)
                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment finalEquip = new Equipment();
                    Equipment troopEquip = character.FirstBattleEquipment;

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        // ПУНКТ 6: Лошадей не выдаем
                        if (s == EquipmentIndex.Horse)
                        {
                            finalEquip[s] = troopEquip[s];
                            continue;
                        }

                        ItemObject maskItem = troopEquip[s].Item;
                        EquipmentElement bestFromDepot = EquipmentElement.Invalid;
                        
                        // Помогаем алгоритму понять, есть ли уже щит в руках
                        bool hasShield = HasShieldInWeapons(finalEquip);

                        // ПУНКТ 2-5, 10-14: Подбор лучшего с учетом всех новых правил
                        if (s <= EquipmentIndex.Weapon3)
                            bestFromDepot = MilitaryDepotActions.ExtractBestWeaponForSlot(maskItem, simRoster, character, hasShield);
                        else
                            bestFromDepot = MilitaryDepotActions.ExtractBestArmorForSlot(s, simRoster, character);

                        // ПУНКТ 8-9: Сравнение найденного на складе с эталоном линейки
                        EquipmentElement finalElement = EquipmentElement.Invalid;
                        if (referenceUnit != null)
                        {
                            ItemObject refItem = referenceUnit.FirstBattleEquipment[s].Item;
                            float refPower = MilitaryDepotLogic.GetItemPower(refItem);
                            float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item);

                            // Если складской предмет лучше или равен эталону - выдаем его и регистрируем
                            if (!bestFromDepot.IsEmpty && depotPower >= refPower)
                            {
                                finalElement = bestFromDepot;
                                
                                // УДАЛЯЕМ из реального инвентаря (склада/сумок), чтобы не было дублей при луте
                                inventory.AddToCounts(bestFromDepot, -1);

                                // РЕГИСТРИРУЕМ В ТРЕКЕРЕ для возврата после боя
                                var activeEvent = party.MapEvent ?? TaleWorlds.CampaignSystem.MapEvents.MapEvent.PlayerMapEvent;
                                if (activeEvent != null)
                                {
                                    BattleEquipmentTracker.RegisterIssuedEquipment(activeEvent, party.Party.Id, bestFromDepot.Item, 1);
                                }
                            }
                            else
                            {
                                // Иначе берем предмет из эталонного шаблона (Тир 1 или 0)
                                finalElement = new EquipmentElement(refItem);
                            }
                        }

                        // ПУНКТ 4: Если слот пустой и на складе/в эталоне ничего нет - оставляем как в шаблоне юнита
                        if (finalElement.IsEmpty)
                        {
                            finalElement = troopEquip[s];
                        }

                        finalEquip[s] = finalElement;
                    }

                    // Добавляем готовый комплект в очередь для миссии
                    GlobalPlan[party][character].Enqueue(finalEquip);

                    if (!FallbackSamples[party].ContainsKey(character))
                        FallbackSamples[party][character] = finalEquip;
                }
            }
        }

        public static Equipment GetPredefinedEquipment(MobileParty party, CharacterObject character)
        {
            if (party == null || character == null || character.IsHero) return null;

            if (GlobalPlan.TryGetValue(party, out var partyPlans))
            {
                if (partyPlans.TryGetValue(character, out var queue) && queue.Count > 0)
                {
                    return queue.Dequeue();
                }

                if (FallbackSamples.TryGetValue(party, out var partySamples))
                {
                    if (partySamples.TryGetValue(character, out var sample))
                        return sample;
                }
            }

            // ПУНКТ 8-9: Если плана нет, возвращаем экипировку эталона
            CharacterObject reference = MilitaryDepotLogic.GetReferenceUnit(character);
            return reference?.FirstBattleEquipment;
        }

        private static bool HasShieldInWeapons(Equipment equip)
        {
            for (int i = 0; i < 4; i++)
                if (equip[i].Item != null && equip[i].Item.ItemType == ItemObject.ItemTypeEnum.Shield) return true;
            return false;
        }

        public static EquipmentElement GetBaseHorseHarness(CharacterObject character)
        {
            // Используем логику отката к Тиру 1 для поиска базовой сбруи
            CharacterObject reference = MilitaryDepotLogic.GetReferenceUnit(character);
            if (reference != null && reference.FirstBattleEquipment[EquipmentIndex.HorseHarness].Item != null)
            {
                return reference.FirstBattleEquipment[EquipmentIndex.HorseHarness];
            }
            return EquipmentElement.Invalid;
        }
    }
}
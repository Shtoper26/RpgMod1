using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace RpgMod1
{
    public static class MilitaryDepotCache
    {
        // В начале файла MilitaryDepotCache.cs измени структуру словарей:

        // Теперь ключ — это MobileParty, а значение — словарь юнитов этого отряда
        private static Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>> GlobalPlan =
            new Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>>();

        // Аналогично для образцов (Fallback)
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
            // УДАЛЕНО: Clear(); <-- Больше не чистим здесь!

            // Инициализируем словари для конкретного отряда
            if (!GlobalPlan.ContainsKey(party))
                GlobalPlan[party] = new Dictionary<CharacterObject, Queue<Equipment>>();

            if (!FallbackSamples.ContainsKey(party))
                FallbackSamples[party] = new Dictionary<CharacterObject, Equipment>();

            var sortedTroops = party.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level);

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero || character.Tier <= 1) continue;

                if (!GlobalPlan[party].ContainsKey(character))
                    GlobalPlan[party][character] = new Queue<Equipment>();

                CharacterObject parentUnit = MilitaryDepotLogic.GetFirstTierCharacter(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment finalEquip = new Equipment();
                    Equipment mask = character.FirstBattleEquipment;

                    // 1. Расширяем границу до HorseHarness
                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        // 2. проверяем, не является ли слот Horse, и если да, то просто копируем его из шаблона юнита
                        if (s == EquipmentIndex.Horse)
                        {
                            finalEquip[s] = character.FirstBattleEquipment[s];
                            continue;
                        }


                        EquipmentElement best = (s <= EquipmentIndex.Weapon3)
                            ? MilitaryDepotActions.ExtractBestWeaponForSlot(mask[s].Item, simRoster)
                            : MilitaryDepotActions.ExtractBestForSlotInSim(s, simRoster);

                        if (!best.IsEmpty)
                        {
                            finalEquip[s] = best;
                        }
                        else
                        {
                            if (s == EquipmentIndex.HorseHarness)
                            {
                                // Используем наш безопасный поиск "первого седла в ветке"
                                finalEquip[s] = GetBaseHorseHarness(character);
                            }

                            else if (parentUnit != null)
                            {

                                finalEquip[s] = parentUnit.FirstBattleEquipment[s];

                            }
                        }

                        if (s == EquipmentIndex.HorseHarness && finalEquip[s].IsEmpty && !finalEquip[EquipmentIndex.Horse].IsEmpty)
                        {
                            finalEquip[s] = character.FirstBattleEquipment[s];
                        }
                    }

                    GlobalPlan[party][character].Enqueue(finalEquip);

                    if (!FallbackSamples[party].ContainsKey(character))
                        FallbackSamples[party][character] = finalEquip;
                }
            }
        }

        // ОБНОВЛЕННЫЙ МЕТОД ВЫДАЧИ: теперь нам нужно знать, чей это юнит
        public static Equipment GetPredefinedEquipment(MobileParty party, CharacterObject character)
        {
            if (party == null || character == null || character.IsHero || character.Tier <= 1) return null;

            // 1. Ищем планы конкретно этого отряда
            if (GlobalPlan.TryGetValue(party, out var partyPlans))
            {
                if (partyPlans.TryGetValue(character, out var queue) && queue.Count > 0)
                {
                    return queue.Dequeue();
                }

                // 2. Если очередь пуста, берем образец этого отряда
                if (FallbackSamples.TryGetValue(party, out var partySamples))
                {
                    if (partySamples.TryGetValue(character, out var sample))
                        return sample;
                }
            }

            // 3. Крайний случай (рекрут)
            CharacterObject parent = MilitaryDepotLogic.GetFirstTierCharacter(character);
            return parent?.FirstBattleEquipment;
        }
        public static EquipmentElement GetBaseHorseHarness(CharacterObject character)
        {
            CharacterObject current = character;
            // Поднимаемся вверх по дереву апгрейдов (к предкам)
            while (current != null)
            {
                // Проверяем, есть ли у этого предка в шаблоне лошадь
                if (current.FirstBattleEquipment != null &&
                    current.FirstBattleEquipment[EquipmentIndex.Horse].Item != null)
                {
                    // Возвращаем сбрую этого самого слабого кавалериста в ветке
                    return current.FirstBattleEquipment[EquipmentIndex.HorseHarness];
                }

                // Если предков больше нет (дошли до рекрута), выходим
                if (current.UpgradeTargets == null || current.UpgradeTargets.Length == 0) break;

                // В Bannerlord сложнее идти "вниз" к рекруту, поэтому проще 
                // использовать уже готовую логику твоего мода по поиску Parent, 
                // но с проверкой на наличие слота Horse.

                // Для примера используем твою логику поиска базового юнита, 
                // но с условием: stop if has horse.
                break;
            }
            return EquipmentElement.Invalid;
        }


    }


}
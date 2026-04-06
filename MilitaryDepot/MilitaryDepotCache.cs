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
        // Очередь планов: Ключ - Тип Юнита, Значение - Список готовых комплектов
        private static Dictionary<CharacterObject, Queue<Equipment>> GlobalPlan = new Dictionary<CharacterObject, Queue<Equipment>>();

        // Резервная копия (Золотой образец) на случай, если юнитов больше, чем в ростере
        private static Dictionary<CharacterObject, Equipment> FallbackSamples = new Dictionary<CharacterObject, Equipment>();

        public static void Clear()
        {
            GlobalPlan.Clear();
            FallbackSamples.Clear();
        }

        public static void CreateBattlePlan(MobileParty party, ItemRoster inventory)
        {
            if (party?.MemberRoster == null || inventory == null) return;

            ItemRoster simRoster = new ItemRoster(inventory);
            Clear();

            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                CharacterObject character = element.Character;
                // Пропускаем героев и рекрутов (0-1 тир)
                if (character == null || character.IsHero || character.Tier <= 1) continue;

                if (!GlobalPlan.ContainsKey(character))
                    GlobalPlan[character] = new Queue<Equipment>();

                // Находим "отца" (рекрута) для этого типа юнита
                CharacterObject parentUnit = MilitaryDepotLogic.GetFirstTierCharacter(character);

                // Создаем планы для всех солдат этого типа в отряде
                for (int n = 0; n < element.Number; n++)
                {
                    Equipment finalEquip = new Equipment();
                    // ВСЕГДА берем ПЕРВУЮ маску юнита (индекс 0), игнорируя вариации игры
                    Equipment mask = character.FirstBattleEquipment;

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.Cape; s++)
                    {
                        // Логика: Склад -> Рекрут
                        EquipmentElement best = (s <= EquipmentIndex.Weapon3)
                            ? MilitaryDepotActions.ExtractBestWeaponForSlot(mask[s].Item, simRoster)
                            : MilitaryDepotActions.ExtractBestForSlotInSim(s, simRoster);

                        if (!best.IsEmpty)
                            finalEquip[s] = best;
                        else if (parentUnit != null)
                            finalEquip[s] = parentUnit.FirstBattleEquipment[s];
                    }

                    GlobalPlan[character].Enqueue(finalEquip);

                    // Сохраняем первый созданный план как образец для "внезапных" юнитов
                    if (!FallbackSamples.ContainsKey(character))
                        FallbackSamples[character] = finalEquip;
                }
            }
        }

        public static Equipment GetPredefinedEquipment(CharacterObject character)
        {
            if (character == null) return null;

            // Если это юнит 2+ тира, мы ОБЯЗАНЫ выдать наш план
            if (character.Tier > 1 && !character.IsHero)
            {
                // 1. Пытаемся взять из очереди
                if (GlobalPlan.TryGetValue(character, out var queue) && queue.Count > 0)
                {
                    return queue.Dequeue();
                }

                // 2. Если очередь пуста (тот самый баг с количеством), выдаем образец
                if (FallbackSamples.TryGetValue(character, out var sample))
                {
                    return sample;
                }

                // 3. Если даже образца нет (юнит-призрак), принудительно делаем его рекрутом
                CharacterObject parent = MilitaryDepotLogic.GetFirstTierCharacter(character);
                if (parent != null) return parent.FirstBattleEquipment;
            }

            return null; // Рекруты и герои идут по ванили
        }
    }
}
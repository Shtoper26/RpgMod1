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
        // Кэш теперь: [Отряд] -> [Тип Юнита] -> [Очередь Экипировки]
        private static Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>> GlobalPlan =
            new Dictionary<MobileParty, Dictionary<CharacterObject, Queue<Equipment>>>();

        public static void Clear() => GlobalPlan.Clear();

        public static void CreateBattlePlan(MobileParty party, ItemRoster inventory)
        {
            if (party == null || inventory == null || party.MemberRoster == null) return;

            if (!GlobalPlan.ContainsKey(party))
                GlobalPlan[party] = new Dictionary<CharacterObject, Queue<Equipment>>();

            ItemRoster simRoster = new ItemRoster(inventory);
            List<CharacterObject> allTroops = new List<CharacterObject>();

            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character.IsHero) continue;
                for (int n = 0; n < element.Number; n++) { allTroops.Add(element.Character); }
            }

            // Сортировка по тиру (6 -> 0)
            var sortedTroops = allTroops.OrderByDescending(t => t.Tier).ToList();

            foreach (var character in sortedTroops)
            {
                CharacterObject firstTier = MilitaryDepotLogic.GetFirstTierCharacter(character);
                Equipment finalEquip = new Equipment();
                bool hasChanges = false;

                for (EquipmentIndex slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Cape; slot++)
                {
                    EquipmentElement best = (slot <= EquipmentIndex.Weapon3)
                        ? MilitaryDepotActions.ExtractBestWeaponForSlot(character.FirstBattleEquipment[slot], simRoster)
                        : MilitaryDepotActions.ExtractBestForSlotInSim(slot, simRoster);

                    if (!best.IsEmpty)
                    {
                        finalEquip[slot] = best;
                        hasChanges = true;
                    }
                    else if (firstTier != null)
                    {
                        finalEquip[slot] = firstTier.FirstBattleEquipment[slot];
                    }
                }

                if (hasChanges)
                {
                    if (!GlobalPlan[party].ContainsKey(character))
                        GlobalPlan[party][character] = new Queue<Equipment>();

                    GlobalPlan[party][character].Enqueue(finalEquip);
                }
            }
        }

        public static Equipment GetPredefinedEquipment(MobileParty party, CharacterObject character)
        {
            if (party != null && GlobalPlan.TryGetValue(party, out var partyPlan))
            {
                if (partyPlan.TryGetValue(character, out Queue<Equipment> queue) && queue.Count > 0)
                {
                    return queue.Dequeue();
                }
            }
            return null;
        }
    }
}
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace RpgMod1
{
    public static class MilitaryDepotActions
    {
        public static void OpenDepot()
        {
            if (MilitaryDepotBehavior.DepotParty == null)
            {
                MilitaryDepotBehavior.DepotParty = MobileParty.CreateParty("military_depot_party", null);
                MilitaryDepotBehavior.DepotParty.IsVisible = false;
            }
            UpdateNeededItemsList();
            InventoryScreenHelper.OpenScreenAsInventoryOf(PartyBase.MainParty, MilitaryDepotBehavior.DepotParty.Party, CharacterObject.PlayerCharacter, null, null, null);
        }

        public static void TransferUselessItems()
        {
            if (MilitaryDepotBehavior.DepotParty == null || PartyBase.MainParty == null) return;
            UpdateNeededItemsList();
            ItemRoster depot = MilitaryDepotBehavior.DepotParty.ItemRoster;
            ItemRoster player = PartyBase.MainParty.ItemRoster;
            int count = 0;
            for (int i = depot.Count - 1; i >= 0; i--)
            {
                ItemRosterElement el = depot[i];
                if (el.EquipmentElement.Item != null && !MilitaryDepotBehavior.NeededItems.Contains(el.EquipmentElement.Item))
                {
                    player.AddToCounts(el.EquipmentElement, el.Amount);
                    depot.Remove(el);
                    count++;
                }
            }
            MilitaryDepotLogs.LogTransferComplete(count);
        }

        

        public static void UpdateNeededItemsList()
        {
            MilitaryDepotBehavior.NeededItems.Clear();
            ItemRoster simRoster = new ItemRoster(MilitaryDepotBehavior.DepotParty.ItemRoster);
            var members = PartyBase.MainParty.MemberRoster;

            for (int i = 0; i < members.Count; i++)
            {
                var el = members.GetElementCopyAtIndex(i);
                if (el.Character == null || el.Character.IsHero || el.Character.Tier <= 1) continue;

                // ВАЖНО: Мы используем ту же логику "отката", что и в основном моде
                CharacterObject parentUnit = el.Character; // Для простоты поиска предка

                for (int n = 0; n < el.Number; n++)
                {
                    Equipment mask = el.Character.FirstBattleEquipment;

                    // ИСПРАВЛЕНИЕ 1: Расширяем цикл до HorseHarness
                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        // ИСПРАВЛЕНИЕ 2: Пропускаем саму лошадь (как и в основном цикле)
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = mask[s].Item;

                        // 1. Ищем лучшее на складе
                        EquipmentElement bestFromDepot = (s <= EquipmentIndex.Weapon3) ?
                            ExtractBestWeaponForSlot(maskItem, simRoster) : ExtractBestForSlotInSim(s, simRoster);

                        if (!bestFromDepot.IsEmpty)
                        {
                            MilitaryDepotBehavior.NeededItems.Add(bestFromDepot.Item);
                        }
                        else if (parentUnit != null)
                        {
                            // 2. Если на складе пусто, добавляем в "нужное" предмет предка
                            if (s == EquipmentIndex.HorseHarness)
                            {
                                // Используем наш безопасный метод для седла
                                EquipmentElement baseHarness = MilitaryDepotCache.GetBaseHorseHarness(parentUnit);
                                if (!baseHarness.IsEmpty) MilitaryDepotBehavior.NeededItems.Add(baseHarness.Item);
                            }
                            else
                            {
                                ItemObject parentItem = parentUnit.FirstBattleEquipment[s].Item;
                                if (parentItem != null) MilitaryDepotBehavior.NeededItems.Add(parentItem);
                            }
                        }
                    }
                }
            }
        }


        public static void PrepareTempLoot() { if (MilitaryDepotBehavior.DepotParty != null) MilitaryDepotBehavior.TempBattleLoot = new ItemRoster(MilitaryDepotBehavior.DepotParty.ItemRoster); }
        public static EquipmentElement ExtractBestForSlot(EquipmentIndex slot) { return ExtractBestForSlotInSim(slot, MilitaryDepotBehavior.TempBattleLoot); }

        public static EquipmentElement ExtractBestForSlotInSim(EquipmentIndex slot, ItemRoster roster)
        {
            if (roster == null) return EquipmentElement.Invalid;
            int bIdx = -1; float bPow = -1f;
            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject item = roster[i].EquipmentElement.Item;
                if (MilitaryDepotLogic.IsItemForArmorSlot(item, slot))
                {
                    float p = MilitaryDepotLogic.GetItemPower(item);
                    if (p > bPow) { bPow = p; bIdx = i; }
                }
            }
            if (bIdx != -1)
            {
                EquipmentElement best = roster[bIdx].EquipmentElement;
                roster.AddToCounts(best, -1);
                return best;
            }
            return EquipmentElement.Invalid;
        }

        public static EquipmentElement ExtractBestWeaponForSlot(ItemObject maskItem, ItemRoster roster)
        {
            // Если на складе пусто — сразу уходим на откат к "отцу"
            if (roster == null || roster.IsEmpty()) return EquipmentElement.Invalid;

            int bestIdx = -1;
            float bestPower = -1f; // Любой предмет лучше пустоты

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject repoItem = roster[i].EquipmentElement.Item;

                // ЖЕСТКАЯ МАСКА: Тип предмета на складе должен СТРОГО совпадать с типом в шаблоне тира N
                // (Например: Одноручное к Одноручному, Щит к Щиту)
                if (maskItem != null && repoItem.ItemType == maskItem.ItemType)
                {
                    float p = MilitaryDepotLogic.GetItemPower(repoItem);
                    if (p > bestPower)
                    {
                        bestPower = p;
                        bestIdx = i;
                    }
                }
            }

            if (bestIdx != -1)
            {
                EquipmentElement bestFound = roster[bestIdx].EquipmentElement;
                roster.AddToCounts(bestFound, -1);
                return bestFound;
            }
            return EquipmentElement.Invalid;
        }
    }
}

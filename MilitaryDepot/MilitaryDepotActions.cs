using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1
{
    public static class MilitaryDepotActions
    {
        // ... (Методы OpenDepot и TransferUselessItems без изменений) ...
        public static void OpenDepot()
        {
            if (MilitaryDepotBehavior.DepotParty == null)
            {
                foreach (var party in MobileParty.All)
                    if (party.StringId == "military_depot_party") { MilitaryDepotBehavior.DepotParty = party; break; }

                if (MilitaryDepotBehavior.DepotParty == null)
                {
                    MilitaryDepotBehavior.DepotParty = MilitaryDepotComponent.CreateDepotParty("military_depot_party", Hero.MainHero.Culture);
                    MilitaryDepotBehavior.DepotParty.IsVisible = false;
                    MilitaryDepotBehavior.DepotParty.ActualClan = Clan.PlayerClan;
                    MilitaryDepotBehavior.DepotParty.IsActive = true;
                }
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
            if (MilitaryDepotBehavior.DepotParty == null) return;

            ItemRoster simRoster = new ItemRoster(MilitaryDepotBehavior.DepotParty.ItemRoster);
            
            // ПРАВИЛО 1: Строгая сортировка по уровню (Тиру) для всех юнитов сразу
            var allTroops = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level)
                .ToList();

            // Считаем, сколько всадников (всех тиров) в отряде - это наш резерв для копий
            int remainingCavalryNeeds = allTroops.Where(t => t.Character.IsMounted).Sum(t => t.Number);

            foreach (var element in allTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero) continue;

                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment troopEquip = character.FirstBattleEquipment;

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        if (s == EquipmentIndex.Horse) continue; // ПУНКТ 6

                        ItemObject maskItem = troopEquip[s].Item;
                        EquipmentElement best = EquipmentElement.Invalid;

                        if (s <= EquipmentIndex.Weapon3)
                            // ПЕРЕДАЕМ число оставшихся всадников для резервирования копий
                            best = ExtractBestWeaponForSlot(maskItem, simRoster, character, troopEquip, remainingCavalryNeeds);
                        else
                            best = ExtractBestArmorForSlot(s, simRoster, character);

                        if (referenceUnit != null)
                        {
                            ItemObject refItem = referenceUnit.FirstBattleEquipment[s].Item;
                            if (refItem != null)
                            {
                                float refPower = MilitaryDepotLogic.GetItemPower(refItem, s);
                                float depotPower = best.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(best.Item, s);

                                if (refPower > depotPower)
                                {
                                    MilitaryDepotBehavior.NeededItems.Add(refItem);
                                    continue;
                                }
                            }
                        }
                        if (!best.IsEmpty) MilitaryDepotBehavior.NeededItems.Add(best.Item);
                    }
                    
                    // После обработки одного юнита, если он был всадником, уменьшаем счетчик нужд кавалерии
                    if (character.IsMounted) remainingCavalryNeeds--;
                }
            }
        }

        public static EquipmentElement ExtractBestArmorForSlot(EquipmentIndex slot, ItemRoster roster, CharacterObject character)
        {
            int bestIdx = -1; float maxPower = -1f;
            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject item = roster[i].EquipmentElement.Item;
                if (MilitaryDepotLogic.IsItemForArmorSlot(item, slot))
                {
                    float p = MilitaryDepotLogic.GetItemPower(item, slot);
                    if (p > maxPower) { maxPower = p; bestIdx = i; }
                }
            }
            if (bestIdx != -1)
            {
                EquipmentElement res = roster[bestIdx].EquipmentElement;
                roster.AddToCounts(res, -1);
                return res;
            }
            return EquipmentElement.Invalid;
        }

        public static EquipmentElement ExtractBestWeaponForSlot(ItemObject maskItem, ItemRoster roster, CharacterObject character, Equipment currentEquip, int cavalryReserve)
        {
            int bestIdx = -1; float maxPower = -1f;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject repoItem = roster[i].EquipmentElement.Item;
                if (repoItem == null || repoItem.PrimaryWeapon == null) continue;

                if (character.Tier > 0 && IsFarmTool(repoItem)) continue;
                if (!IsWeaponAllowedForUnit(repoItem, character)) continue;
                if (IsDuplicateUniqueItem(repoItem, currentEquip)) continue;

                // --- ЛОГИКА РЕЗЕРВИРОВАНИЯ КОПИЙ ДЛЯ КАВАЛЕРИИ ---
                if (!character.IsMounted && IsCavalryPolearm(repoItem))
                {
                    // Если количество таких копий на складе меньше или равно количеству всадников, 
                    // которые еще не получили снаряжение - пехотинец ПУСКАЕТ ИХ МИМО (Power = 0)
                    if (roster[i].Amount <= cavalryReserve) continue;
                }

                bool isMatch = false;
                if (maskItem != null && repoItem.ItemType == maskItem.ItemType) isMatch = true;
                else if (maskItem != null && maskItem.PrimaryWeapon != null && 
                         maskItem.PrimaryWeapon.RelevantSkill == repoItem.PrimaryWeapon.RelevantSkill) isMatch = true;
                else if (maskItem == null && repoItem.ItemType == ItemObject.ItemTypeEnum.Shield && !HasItemType(currentEquip, ItemObject.ItemTypeEnum.Shield)) isMatch = true;

                if (isMatch)
                {
                    float p = MilitaryDepotLogic.GetItemPower(repoItem, EquipmentIndex.Weapon0);
                    if (p > maxPower) { maxPower = p; bestIdx = i; }
                }
            }

            if (bestIdx != -1)
            {
                EquipmentElement res = roster[bestIdx].EquipmentElement;
                roster.AddToCounts(res, -1);
                return res;
            }
            return EquipmentElement.Invalid;
        }

        public static bool IsDuplicateUniqueItem(ItemObject item, Equipment currentEquip)
        {
            var type = item.ItemType;
            if (type == ItemObject.ItemTypeEnum.Shield || type == ItemObject.ItemTypeEnum.Bow || 
                type == ItemObject.ItemTypeEnum.Crossbow || type == ItemObject.ItemTypeEnum.Thrown)
                return HasItemType(currentEquip, type);
            return false;
        }

        private static bool HasItemType(Equipment equip, ItemObject.ItemTypeEnum type)
        {
            for (int i = 0; i < 4; i++)
                if (equip[i].Item != null && equip[i].Item.ItemType == type) return true;
            return false;
        }

        private static bool IsWeaponAllowedForUnit(ItemObject item, CharacterObject character)
        {
            var weapon = item.PrimaryWeapon;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bow && character.GetSkillValue(DefaultSkills.Bow) < item.Difficulty) return false;

            if (character.IsMounted)
            {
                if (item.ItemType == ItemObject.ItemTypeEnum.Bow && IsLongbow(item)) return false;
                if (!IsUsableMounted(item)) return false;
                if (item.ItemType == ItemObject.ItemTypeEnum.Polearm && weapon.ThrustDamage <= 0) return false;
            }
            return true;
        }

        private static bool IsCavalryPolearm(ItemObject item)
        {
            if (item.ItemType != ItemObject.ItemTypeEnum.Polearm) return false;
            return item.PrimaryWeapon.ThrustDamage > 0 && IsUsableMounted(item);
        }

        private static bool IsUsableMounted(ItemObject item)
        {
            string usage = item.PrimaryWeapon?.ItemUsage?.ToLower() ?? "";
            return !usage.Contains("longbow") && !usage.Contains("pike") && !usage.Contains("bracing");
        }

        private static bool IsLongbow(ItemObject item)
        {
            return (item.PrimaryWeapon?.ItemUsage?.ToLower() ?? "").Contains("longbow");
        }

        private static bool IsFarmTool(ItemObject item)
        {
            string id = item.StringId?.ToLower() ?? "";
            return id.Contains("scythe") || id.Contains("hoe") || id.Contains("sickle");
        }
    }
}
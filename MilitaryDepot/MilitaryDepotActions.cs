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
        public static void OpenDepot()
        {
            if (MilitaryDepotBehavior.DepotParty == null)
            {
                foreach (var party in MobileParty.All)
                {
                    if (party.StringId == "military_depot_party")
                    {
                        MilitaryDepotBehavior.DepotParty = party;
                        break;
                    }
                }

                if (MilitaryDepotBehavior.DepotParty == null)
                {
                    MilitaryDepotBehavior.DepotParty = MilitaryDepotComponent.CreateDepotParty("military_depot_party", Hero.MainHero.Culture);
                    MilitaryDepotBehavior.DepotParty.IsVisible = false;
                    MilitaryDepotBehavior.DepotParty.ActualClan = Clan.PlayerClan;
                    MilitaryDepotBehavior.DepotParty.IsActive = true;
                }
            }

            UpdateNeededItemsList();
            InventoryScreenHelper.OpenScreenAsInventoryOf(
                PartyBase.MainParty,
                MilitaryDepotBehavior.DepotParty.Party,
                CharacterObject.PlayerCharacter,
                null, null, null
            );
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
            
            var sortedTroops = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level)
                .ToList();

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero) continue;

                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment troopEquip = character.FirstBattleEquipment;

                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = troopEquip[s].Item;
                        EquipmentElement bestFromDepot = EquipmentElement.Invalid;
                        
                        if (s <= EquipmentIndex.Weapon3)
                            bestFromDepot = ExtractBestWeaponForSlot(maskItem, simRoster, character, troopEquip);
                        else
                            bestFromDepot = ExtractBestArmorForSlot(s, simRoster, character);

                        if (referenceUnit != null)
                        {
                            ItemObject refItem = referenceUnit.FirstBattleEquipment[s].Item;
                            if (refItem != null)
                            {
                                float refPower = MilitaryDepotLogic.GetItemPower(refItem);
                                float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item);

                                if (refPower > depotPower)
                                {
                                    MilitaryDepotBehavior.NeededItems.Add(refItem);
                                    continue;
                                }
                            }
                        }

                        if (!bestFromDepot.IsEmpty) MilitaryDepotBehavior.NeededItems.Add(bestFromDepot.Item);
                    }
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
                    float p = MilitaryDepotLogic.GetItemPower(item);
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

        public static EquipmentElement ExtractBestWeaponForSlot(ItemObject maskItem, ItemRoster roster, CharacterObject character, Equipment currentEquip)
        {
            int bestIdx = -1; float maxPower = -1f;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject repoItem = roster[i].EquipmentElement.Item;
                if (repoItem == null || repoItem.PrimaryWeapon == null) continue;

                if (character.Tier > 0 && IsFarmTool(repoItem)) continue;
                if (!IsWeaponAllowedForUnit(repoItem, character)) continue;

                // Проверка на дублирование щитов/луков
                if (IsDuplicateUniqueItem(repoItem, currentEquip)) continue;

                bool isMatch = false;

                if (maskItem != null && repoItem.ItemType == maskItem.ItemType)
                {
                    isMatch = true;
                }
                else if (maskItem != null && maskItem.PrimaryWeapon != null && 
                         maskItem.PrimaryWeapon.RelevantSkill == repoItem.PrimaryWeapon.RelevantSkill)
                {
                    isMatch = true;
                }
                else if (maskItem == null && repoItem.ItemType == ItemObject.ItemTypeEnum.Shield && !HasItemType(currentEquip, ItemObject.ItemTypeEnum.Shield))
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    float p = MilitaryDepotLogic.GetItemPower(repoItem);
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
            if (type == ItemObject.ItemTypeEnum.Shield || 
                type == ItemObject.ItemTypeEnum.Bow || 
                type == ItemObject.ItemTypeEnum.Crossbow ||
                type == ItemObject.ItemTypeEnum.Thrown)
            {
                return HasItemType(currentEquip, type);
            }
            return false;
        }

        private static bool HasItemType(Equipment equip, ItemObject.ItemTypeEnum type)
        {
            for (int i = 0; i < 4; i++)
            {
                if (equip[i].Item != null && equip[i].Item.ItemType == type) return true;
            }
            return false;
        }

        private static bool IsWeaponAllowedForUnit(ItemObject item, CharacterObject character)
        {
            var weapon = item.PrimaryWeapon;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bow && character.GetSkillValue(DefaultSkills.Bow) < item.Difficulty)
                return false;

            if (character.IsMounted)
            {
                string usage = weapon.ItemUsage?.ToLower() ?? "";
                if (item.ItemType == ItemObject.ItemTypeEnum.Bow && usage.Contains("longbow")) return false;
                if (usage.Contains("pike") || usage.Contains("bracing")) return false;
                if (item.ItemType == ItemObject.ItemTypeEnum.Polearm && weapon.ThrustDamage <= 0) return false;
            }
            return true;
        }

        private static bool IsFarmTool(ItemObject item)
        {
            string id = item.StringId?.ToLower() ?? "";
            return id.Contains("scythe") || id.Contains("hoe") || id.Contains("sickle");
        }
    }
}
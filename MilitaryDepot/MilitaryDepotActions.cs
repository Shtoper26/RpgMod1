using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using HarmonyLib;
using System.Reflection;

namespace RpgMod1
{
    // --- ПАТЧ ДЛЯ БЛОКИРОВКИ ПЕРКОВ (Гарантия обещания и Дающая рука) ---
    [HarmonyPatch]
    public static class MilitaryDepotXpBlocker
    {
        // Используем ручной поиск метода, так как класс поведения в игре помечен как internal
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("TaleWorlds.CampaignSystem.CampaignBehaviors.DiscardItemsCampaignBehavior:OnItemsDiscardedByPlayer");
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Проверяем текущее состояние игры. Если открыт инвентарь:
            if (Game.Current.GameStateManager.ActiveState is InventoryState inventoryState)
            {
                // Если "Другой стороной" (правой частью инвентаря) является наш склад
                if (inventoryState.InventoryLogic != null && 
                    MilitaryDepotBehavior.DepotParty != null &&
                    inventoryState.InventoryLogic.OtherParty == MilitaryDepotBehavior.DepotParty.Party)
                {
                    // Возвращаем false, чтобы метод OnItemsDiscardedByPlayer НЕ выполнился
                    // Таким образом, опыт за "выброшенные" (переданные на склад) вещи не начислится
                    return false;
                }
            }
            return true;
        }
    }

    public static class MilitaryDepotActions
    {
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
            
            // В 1.3.15 используем стандартный вызов
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

        // ... остальной ваш код UpdateNeededItemsList, ExtractBestArmorForSlot и т.д. без изменений ...
        public static void UpdateNeededItemsList()
        {
            MilitaryDepotBehavior.NeededItems.Clear();
            if (MilitaryDepotBehavior.DepotParty == null) return;

            ItemRoster simRoster = new ItemRoster(MilitaryDepotBehavior.DepotParty.ItemRoster);
            var sortedTroops = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level).ToList();

            int cavalryReserve = sortedTroops.Where(t => t.Character.IsMounted).Sum(t => t.Number);

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero) continue;

                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment mask = character.FirstBattleEquipment;
                    
                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = mask[s].Item;
                        EquipmentElement bestFromDepot = (s <= EquipmentIndex.Weapon3)
                            ? ExtractBestWeaponForSlot(maskItem, simRoster, character, mask, cavalryReserve)
                            : ExtractBestArmorForSlot(s, simRoster, character);

                        if (referenceUnit != null && referenceUnit.BattleEquipments != null)
                        {
                            foreach (var refTemplate in referenceUnit.BattleEquipments)
                            {
                                ItemObject refItem = refTemplate[s].Item;
                                if (refItem != null)
                                {
                                    float refPower = MilitaryDepotLogic.GetItemPower(refItem, s);
                                    float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item, s);

                                    if (!bestFromDepot.IsEmpty && depotPower >= refPower)
                                    {
                                        MilitaryDepotBehavior.NeededItems.Add(bestFromDepot.Item);
                                    }
                                }
                            }
                        }
                        
                        if (!bestFromDepot.IsEmpty)
                        {
                            MilitaryDepotBehavior.NeededItems.Add(bestFromDepot.Item);
                        }
                    }
                    if (character.IsMounted) cavalryReserve--;
                }
            }
        }
        
        // ... (ваши вспомогательные методы ExtractBestWeaponForSlot, HasItemType и др.)
        //// ... существующий код ...
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
            if (bestIdx != -1) { EquipmentElement res = roster[bestIdx].EquipmentElement; roster.AddToCounts(res, -1); return res; }
            return EquipmentElement.Invalid;
        }

        public static EquipmentElement ExtractBestWeaponForSlot(ItemObject maskItem, ItemRoster roster, CharacterObject character, Equipment currentEquip, int cavalryReserve)
        {
            int bestIdx = -1;
            int bestMatchLevel = 0; 
            float maxPower = -1f;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject repoItem = roster[i].EquipmentElement.Item;
                if (repoItem == null || repoItem.PrimaryWeapon == null) continue;

                if (character.Tier > 0 && IsFarmTool(repoItem)) continue;
                if (!IsWeaponAllowedForUnit(repoItem, character)) continue;

                bool isReplacement = maskItem != null && repoItem.ItemType == maskItem.ItemType;
                if (!isReplacement && IsDuplicateUniqueItem(repoItem, currentEquip)) continue;

                if (!character.IsMounted && IsCavalryPolearm(repoItem) && roster[i].Amount <= cavalryReserve) continue;

                int currentMatchLevel = 0;
                if (maskItem != null && maskItem.PrimaryWeapon != null)
                {
                    if (repoItem.PrimaryWeapon.WeaponClass == maskItem.PrimaryWeapon.WeaponClass)
                        currentMatchLevel = 3;
                    else if (maskItem.PrimaryWeapon.RelevantSkill == repoItem.PrimaryWeapon.RelevantSkill)
                    {
                        bool isGeneral = maskItem.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || 
                                        maskItem.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon;
                        if (isGeneral) currentMatchLevel = 2;
                    }
                }
                else if (repoItem.ItemType == ItemObject.ItemTypeEnum.Shield && !HasItemType(currentEquip, ItemObject.ItemTypeEnum.Shield))
                {
                    currentMatchLevel = 1;
                }

                if (currentMatchLevel > 0)
                {
                    float p = MilitaryDepotLogic.GetItemPower(repoItem, EquipmentIndex.Weapon0);
                    if (currentMatchLevel > bestMatchLevel)
                    {
                        bestMatchLevel = currentMatchLevel;
                        maxPower = p;
                        bestIdx = i;
                    }
                    else if (currentMatchLevel == bestMatchLevel && p > maxPower)
                    {
                        maxPower = p;
                        bestIdx = i;
                    }
                }
            }

            if (bestIdx != -1) { 
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
            {
                return HasItemType(currentEquip, type);
            }
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
                if (item.ItemType == ItemObject.ItemTypeEnum.Bow && (weapon.ItemUsage?.ToLower().Contains("longbow") ?? false)) return false;
                if (!IsUsableMounted(item)) return false;
                if (item.ItemType == ItemObject.ItemTypeEnum.Polearm && weapon.ThrustDamage <= 0) return false;
            }
            return true;
        }

        private static bool IsUsableMounted(ItemObject item)
        {
            string usage = item.PrimaryWeapon?.ItemUsage?.ToLower() ?? "";
            return !usage.Contains("longbow") && !usage.Contains("pike") && !usage.Contains("bracing");
        }

        private static bool IsCavalryPolearm(ItemObject item)
        {
            return item.ItemType == ItemObject.ItemTypeEnum.Polearm && item.PrimaryWeapon.ThrustDamage > 0 && IsUsableMounted(item);
        }

        private static bool IsFarmTool(ItemObject item)
        {
            string id = item.StringId?.ToLower() ?? "";
            return id.Contains("scythe") || id.Contains("hoe") || id.Contains("sickle");
        }
    }
}
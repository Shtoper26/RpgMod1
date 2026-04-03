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
                if (el.Character.IsHero) continue;
                for (int n = 0; n < el.Number; n++)
                {
                    Equipment temp = el.Character.FirstBattleEquipment;
                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.Cape; s++)
                    {
                        EquipmentElement best = (s <= EquipmentIndex.Weapon3) ?
                            ExtractBestWeaponForSlot(temp[s], simRoster) : ExtractBestForSlotInSim(s, simRoster);
                        if (!best.IsEmpty) MilitaryDepotBehavior.NeededItems.Add(best.Item);
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

        public static EquipmentElement ExtractBestWeaponForSlot(EquipmentElement current, ItemRoster roster)
        {
            if (roster == null || roster.IsEmpty() || current.IsEmpty) return EquipmentElement.Invalid;
            int bIdx = -1; float bPow = MilitaryDepotLogic.GetItemPower(current.Item);
            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject item = roster[i].EquipmentElement.Item;
                if (MilitaryDepotLogic.IsCompatibleWeapon(current.Item, item))
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
    }
}

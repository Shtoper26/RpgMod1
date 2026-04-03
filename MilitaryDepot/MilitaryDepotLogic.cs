using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace RpgMod1
{
    public static class MilitaryDepotLogic
    {
        public static float GetItemPower(ItemObject item)
        {
            if (item == null) return 0f;
            if (item.ArmorComponent != null) return item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor;
            if (item.PrimaryWeapon != null)
            {
                float d = Math.Max(item.PrimaryWeapon.ThrustDamage, item.PrimaryWeapon.SwingDamage);
                return (item.ItemType == ItemObject.ItemTypeEnum.Thrown) ? d * 1.5f : d;
            }
            return 0f;
        }

        public static bool IsCompatibleWeapon(ItemObject original, ItemObject potential)
        {
            if (original == null || potential == null) return false;
            if (original.ItemType == ItemObject.ItemTypeEnum.Shield) return potential.ItemType == ItemObject.ItemTypeEnum.Shield;
            if (original.ItemType == ItemObject.ItemTypeEnum.Bow) return potential.ItemType == ItemObject.ItemTypeEnum.Bow;
            if (original.ItemType == ItemObject.ItemTypeEnum.Crossbow) return potential.ItemType == ItemObject.ItemTypeEnum.Crossbow;
            // ... остальные проверки типа оружия ...
            return original.ItemType == potential.ItemType;
        }

        public static bool IsEquipment(ItemObject item)
        {
            return item.ItemType == ItemObject.ItemTypeEnum.BodyArmor || item.ItemType == ItemObject.ItemTypeEnum.LegArmor ||
                   item.ItemType == ItemObject.ItemTypeEnum.HeadArmor || item.ItemType == ItemObject.ItemTypeEnum.HandArmor ||
                   item.ItemType == ItemObject.ItemTypeEnum.Cape || item.ItemType == ItemObject.ItemTypeEnum.Shield ||
                   item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || item.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                   item.ItemType == ItemObject.ItemTypeEnum.Polearm || item.ItemType == ItemObject.ItemTypeEnum.Thrown ||
                   item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow ||
                   item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts;
        }

        public static CharacterObject GetFirstTierCharacter(CharacterObject character)
        {
            if (character == null) return null;
            CharacterObject current = character;
            bool foundParent = true;
            while (foundParent)
            {
                foundParent = false;
                foreach (var obj in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (obj.UpgradeTargets != null && obj.UpgradeTargets.Contains(current))
                    {
                        current = obj; foundParent = true; break;
                    }
                }
            }
            return current;
        }

        public static bool IsItemForArmorSlot(ItemObject item, EquipmentIndex slot)
        {
            if (item == null) return false;
            switch (slot)
            {
                case EquipmentIndex.Head: return item.ItemType == ItemObject.ItemTypeEnum.HeadArmor;
                case EquipmentIndex.Body: return item.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
                case EquipmentIndex.Leg: return item.ItemType == ItemObject.ItemTypeEnum.LegArmor;
                case EquipmentIndex.Gloves: return item.ItemType == ItemObject.ItemTypeEnum.HandArmor;
                case EquipmentIndex.Cape: return item.ItemType == ItemObject.ItemTypeEnum.Cape;
                default: return false;
            }
        }
    }
}
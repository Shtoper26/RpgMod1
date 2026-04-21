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
            if (item == null) return -1f;
            if (item.ArmorComponent != null) 
                return item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + 
                       item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor;
            
            if (item.WeaponComponent != null)
            {
                var weapon = item.WeaponComponent.PrimaryWeapon;
                float d = Math.Max(weapon.ThrustDamage, weapon.SwingDamage);
                if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return weapon.MaxDataValue * 0.1f;
                return (item.ItemType == ItemObject.ItemTypeEnum.Thrown) ? d * 1.5f : d;
            }
            return 0f;
        }

        // НОВАЯ ЛОГИКА (Пункты 8-9):
        public static CharacterObject GetReferenceUnit(CharacterObject character)
        {
            if (character == null) return null;
            // Если юнит уже Тир 0 или Тир 1 - он сам себе образец
            if (character.Tier <= 1) return character;

            CharacterObject current = character;
            bool foundParent = true;

            // Идем вверх по дереву, пока не достигнем Тира 1
            while (current.Tier > 1 && foundParent)
            {
                foundParent = false;
                foreach (var obj in MBObjectManager.Instance.GetObjectTypeList<CharacterObject>())
                {
                    if (obj.UpgradeTargets != null && obj.UpgradeTargets.Contains(current))
                    {
                        current = obj;
                        foundParent = true;
                        break;
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
                case EquipmentIndex.HorseHarness: return item.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
                default: return false;
            }
        }
    }
}
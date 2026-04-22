using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace RpgMod1
{
    public static class MilitaryDepotLogic
    {
        // Теперь метод принимает слот, для которого мы оцениваем предмет
        public static float GetItemPower(ItemObject item, EquipmentIndex slot = EquipmentIndex.None)
        {
            if (item == null) return -1f;

            if (item.ArmorComponent != null)
            {
                // ПУНКТ 2: Оцениваем только профильный параметр для конкретного слота
                switch (slot)
                {
                    case EquipmentIndex.Head: return item.ArmorComponent.HeadArmor;
                    case EquipmentIndex.Body: return item.ArmorComponent.BodyArmor; // Учитываем ТОЛЬКО Броню тела
                    case EquipmentIndex.Leg: return item.ArmorComponent.LegArmor;
                    case EquipmentIndex.Gloves: return item.ArmorComponent.ArmArmor;
                    case EquipmentIndex.Cape: return item.ArmorComponent.BodyArmor;
                    case EquipmentIndex.HorseHarness: return item.ArmorComponent.BodyArmor;
                    default:
                        // Если слот не указан, суммируем всё (для общего веса/ценности)
                        return item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + 
                               item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor;
                }
            }
            
            if (item.WeaponComponent != null)
            {
                var weapon = item.WeaponComponent.PrimaryWeapon;
                float d = Math.Max(weapon.ThrustDamage, weapon.SwingDamage);
                if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return weapon.MaxDataValue * 0.1f;
                return (item.ItemType == ItemObject.ItemTypeEnum.Thrown) ? d * 1.5f : d;
            }
            return 0f;
        }

        // ... остальные методы (GetReferenceUnit, IsItemForArmorSlot) остаются без изменений
        public static CharacterObject GetReferenceUnit(CharacterObject character)
        {
            if (character == null) return null;
            if (character.Tier <= 1) return character;
            CharacterObject current = character;
            bool foundParent = true;
            while (current.Tier > 1 && foundParent)
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
                case EquipmentIndex.HorseHarness: return item.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
                default: return false;
            }
        }
    }
}
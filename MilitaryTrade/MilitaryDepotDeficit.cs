using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster; // Важно для работы с TroopRoster
using TaleWorlds.Core;

namespace RpgMod1
{
    public static class MilitaryDepotDeficit
    {
        public class DeficitReport
        {
            public ItemObject.ItemTypeEnum ItemType;
            public int NeededCount;
            public bool ForMounted; // НОВОЕ: флаг, что предмет нужен именно кавалерии
        }

        public static List<DeficitReport> GetSquadDeficit(MobileParty mobileParty)
        {
            // Ключ словаря: (Тип предмета, Конный ли юнит)
            var deficitMap = new Dictionary<(ItemObject.ItemTypeEnum, bool), int>();

            foreach (var troopElement in mobileParty.MemberRoster.GetTroopRoster())
            {
                CharacterObject character = troopElement.Character;
                int count = troopElement.Number;

                // ВОТ ОНО: Проверяем, является ли юнит конным
                bool isMounted = character.IsMounted;

                // Проверяем все типы снаряжения, которые мы отслеживаем
                var typesToCheck = new List<ItemObject.ItemTypeEnum>
        {
            ItemObject.ItemTypeEnum.Shield,
            ItemObject.ItemTypeEnum.Bow,
            ItemObject.ItemTypeEnum.Crossbow,
            ItemObject.ItemTypeEnum.Polearm,
            ItemObject.ItemTypeEnum.OneHandedWeapon
        };

                foreach (var type in typesToCheck)
                {
                    // Если этому типу юнита положен такой предмет по шаблону
                    if (CharacterHasItemType(character, type))
                    {
                        // Считаем, сколько таких предметов уже есть в инвентаре отряда (упрощенно)
                        // В идеале тут должна быть проверка текущего наличия, 
                        // но для ИИ-логики мы берем целевое число минус имеющееся
                        int currentInStock = CountItemsInRoster(mobileParty.ItemRoster, type);
                        int needed = Math.Max(0, count - currentInStock);

                        if (needed > 0)
                        {
                            var key = (type, isMounted);
                            if (deficitMap.ContainsKey(key))
                                deficitMap[key] += needed;
                            else
                                deficitMap[key] = needed;
                        }
                    }
                }
            }



            // Преобразуем словарь в плоский список для модуля закупки
            return deficitMap.Select(x => new DeficitReport
            {
                ItemType = x.Key.Item1,
                ForMounted = x.Key.Item2,
                NeededCount = x.Value
            }).ToList();


        }
        private static int CountItemsInRoster(ItemRoster roster, ItemObject.ItemTypeEnum type)
        {
            int count = 0;
            foreach (var element in roster)
            {
                if (element.EquipmentElement.Item != null && element.EquipmentElement.Item.ItemType == type)
                    count += element.Amount;
            }
            return count;
        }

        private static List<DeficitReport> SortByPriority(List<DeficitReport> list)
        {
            return list.OrderBy(x => GetPriorityIndex(x.ItemType)).ToList();
        }

        private static int GetPriorityIndex(ItemObject.ItemTypeEnum type)
        {
            switch (type)
            {
                case ItemObject.ItemTypeEnum.Shield: return 1; // Щит - Приоритет №1
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm: return 2;
                case ItemObject.ItemTypeEnum.BodyArmor: return 3;
                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow: return 4;
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts: return 5;
                case ItemObject.ItemTypeEnum.HeadArmor: return 6;
                default: return 10;
            }
        }
        private static bool CharacterHasItemType(CharacterObject character, ItemObject.ItemTypeEnum itemType)
        {
            // Проверяем первый (основной) набор снаряжения юнита
            if (character.FirstBattleEquipment == null) return false;

            // Проходим по всем слотам оружия (от 0 до 3)
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var equipmentElement = character.FirstBattleEquipment[i];
                if (equipmentElement.Item != null && equipmentElement.Item.ItemType == itemType)
                {
                    return true;
                }
            }
            return false;
        }
    }
    
    }
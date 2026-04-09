using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace RpgMod1.MilitaryTrade
{
    public static class MilitaryDepotTradeLogic
    {
        public static void ProcessAiTrade(MobileParty mobileParty, Settlement settlement)
        {
            int townGold = settlement.SettlementComponent.Gold;
            // 15% резерв от численности отряда
            int reserveCount = (int)(mobileParty.MemberRoster.TotalManCount * 0.15f);
            if (reserveCount < 5) reserveCount = 5;

            Dictionary<ItemObject.ItemTypeEnum, int> reservedTracker = new Dictionary<ItemObject.ItemTypeEnum, int>();
            var itemElements = mobileParty.ItemRoster.ToList();

            foreach (var element in itemElements)
            {
                ItemObject item = element.EquipmentElement.Item;
                if (item.IsFood) continue;

                // Ванильный фильтр лошадей
                if (item.ItemType == ItemObject.ItemTypeEnum.Horse)
                {
                    if (item.HorseComponent.IsRideable && element.EquipmentElement.ItemModifier == null && !item.HorseComponent.IsPackAnimal)
                        continue;
                }

                if (IsEquipment(item))
                {
                    ItemObject.ItemTypeEnum type = item.ItemType;
                    if (!reservedTracker.ContainsKey(type)) reservedTracker[type] = 0;

                    if (reservedTracker[type] < reserveCount)
                    {
                        int needed = reserveCount - reservedTracker[type];
                        int toKeep = (element.Amount > needed) ? needed : element.Amount;
                        reservedTracker[type] += toKeep;

                        if (element.Amount > toKeep)
                            ExecuteSell(mobileParty, settlement, element, element.Amount - toKeep, ref townGold);

                        continue;
                    }
                }
                ExecuteSell(mobileParty, settlement, element, element.Amount, ref townGold);
            }
        }

        private static void ExecuteSell(MobileParty seller, Settlement settlement, ItemRosterElement element, int count, ref int gold)
        {
            if (count <= 0 || gold <= 0) return;
            int price = settlement.Town.GetItemPrice(element.EquipmentElement, seller, true);
            int amountToSell = (price * count <= gold) ? count : (gold / price);

            if (amountToSell > 0)
            {
                TaleWorlds.CampaignSystem.Actions.SellItemsAction.Apply(seller.Party, settlement.Party, element, amountToSell, settlement);
                gold -= (price * amountToSell);
            }
        }
        private static bool IsEquipment(ItemObject item)
        {
            if (item == null) return false;

            // Если у предмета есть компонент оружия или брони — это наше снаряжение
            // (Лошадей мы отфильтровали раньше, так что они сюда не попадут)
            return item.WeaponComponent != null || item.ArmorComponent != null;
        }
    }
}
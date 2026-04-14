using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1.MilitaryTrade
{
    // Класс-контейнер для патчей торговли
    public static class MilitaryDepotTradeLogic
    {
        [HarmonyPatch(typeof(SellItemsAction), "Apply")]
        public class SellItemsActionPatch
        {
            static bool Prefix(PartyBase receiverParty, PartyBase payerParty, ItemRosterElement subject, ref int number, Settlement currentSettlement)
            {
                // Нас интересуют только продажи от мобильных отрядов лордов
                if (receiverParty != null && receiverParty.IsMobile && receiverParty.MobileParty.IsLordParty)
                {
                    MobileParty lordParty = receiverParty.MobileParty;
                    ItemObject item = subject.EquipmentElement.Item;

                    bool isMilitary = IsMilitaryEquipment(item);

                    if (isMilitary)
                    {
                        // Считаем лимит: 100% состава + 15% запас
                        int totalTroops = lordParty.MemberRoster.TotalManCount;
                        int reserveCount = (int)(totalTroops * 1.15f);
                        if (reserveCount < 10) reserveCount = 10;

                        // Считаем, сколько предметов этого ТИПА уже есть у лорда
                        int currentTypeCount = lordParty.ItemRoster
                            .Where(x => x.EquipmentElement.Item.ItemType == item.ItemType)
                            .Sum(x => x.Amount);

                        // Если после продажи останется меньше резерва
                        if (currentTypeCount - number < reserveCount)
                        {
                            int allowedToSell = currentTypeCount - reserveCount;

                            if (allowedToSell <= 0)
                            {
                                // Полностью отменяем продажу предмета
                                return false;
                            }
                            else
                            {
                                // Продаем только то, что выходит за рамки 115%
                                number = allowedToSell;
                            }
                        }

                        // ЛОГ: Продажа военного снаряжения (Оранжевый)
                        if (number > 0 && currentSettlement != null)
                        {
                            int price = currentSettlement.Town?.GetItemPrice(subject.EquipmentElement, lordParty, true) ?? 0;
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[ТОРГОВЛЯ] {lordParty.Name} продал излишек: {number}x {item.Name} за {price * number} дин. в {currentSettlement.Name}",
                                Color.FromUint(0xFFF7941D)));
                        }
                    }
                    else
                    {
                        // ЛОГ: Продажа обычных товаров (Белый)
                        if (number > 0 && currentSettlement != null)
                        {
                            int price = currentSettlement.Town?.GetItemPrice(subject.EquipmentElement, lordParty, true) ?? 0;
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[РЫНОК] {lordParty.Name} продал товар: {number}x {item.Name} в {currentSettlement.Name}",
                                Color.FromUint(0xFFFFFFFF)));
                        }
                    }
                }
                return true; // Разрешаем выполнение оригинального метода (с учетом измененного number)
            }

            private static bool IsMilitaryEquipment(ItemObject item)
            {
                if (item == null) return false;
                // Оружие, Броня или Конская сбруя
                return item.WeaponComponent != null ||
                       item.ArmorComponent != null ||
                       item.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Core.ItemObject;

namespace RpgMod1
{
    public static class MilitaryDepotPurchase
    {
        private const float Tier1Max = 500f;
        private const float Tier2Max = 2000f;
        private const float Tier3Max = 5000f;

        public static void ExecutePurchaseLogic(MobileParty mobileParty, Settlement settlement)
        {
            // Получаем бюджет из твоего класса
            var finances = CalculateShoppingBudget.GetAiShoppingFinances(mobileParty);

            // 1. Сразу платим налог клану
            if (finances.tributeToClan > 0)
            {
                mobileParty.LeaderHero.ChangeHeroGold(-finances.tributeToClan);
                if (mobileParty.ActualClan.Leader != null)
                {
                    mobileParty.ActualClan.Leader.ChangeHeroGold(finances.tributeToClan);
                }
            }

            int currentBudget = finances.shoppingBudget;
            if (currentBudget <= 0) return;

            // 2. Получаем дефицит
            var deficitList = MilitaryDepotDeficit.GetSquadDeficit(mobileParty);
            if (deficitList.Count == 0) return;

            

            // ЛОГ: Начало шоппинга
            InformationManager.DisplayMessage(new InformationMessage(
                $"[MilitaryDepot] {mobileParty.LeaderHero.Name} зашел в {settlement.Name}. Бюджет: {currentBudget} золотых.",
                new Color(0.1f, 0.8f, 0.2f))); // Зеленый цвет

            var townItems = settlement.ItemRoster;

            foreach (var report in deficitList)
            {
                if (currentBudget <= 0) break;

                int maxSpendForThisLot = (int)(finances.shoppingBudget * 0.20f);
                int spentForThisLot = 0;

                var suitableItems = townItems
        .Where(x => x.EquipmentElement.Item != null &&
                    x.EquipmentElement.Item.ItemType == report.ItemType &&
                    x.Amount > 0)
        .Select(x => new {
            Element = x,
            Score = MilitaryDepotLogic.GetItemPower(x.EquipmentElement.Item) / Math.Max(1, x.EquipmentElement.Item.Value)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

                foreach (var entry in suitableItems)
                {
                    // Теперь проверяем: подходит ли этот конкретный лук/щит под нужды (пеший/конный)
                    if (!IsSuitableForItemTask(entry.Element.EquipmentElement.Item, report.ForMounted))
                        continue;

                    if (currentBudget <= 0 || spentForThisLot >= maxSpendForThisLot || report.NeededCount <= 0) break;

                    ItemObject item = entry.Element.EquipmentElement.Item;
                    int price = settlement.Town.GetItemPrice(item);

                    if (price > Tier1Max && report.NeededCount > (mobileParty.MemberRoster.TotalManCount / 2)) continue;
                    if (price > Tier3Max) continue;

                    // ВЫЗОВ МЕТОДА (который вызывал ошибку)
                    // Теперь мы вызываем новое имя метода и передаем информацию о типе юнита (ForMounted)
                    if (!IsSuitableForItemTask(item, report.ForMounted))
                        continue;

                    int amountToBuy = Math.Min(entry.Element.Amount, report.NeededCount);
                    amountToBuy = Math.Min(amountToBuy, (maxSpendForThisLot - spentForThisLot) / price);
                    amountToBuy = Math.Min(amountToBuy, currentBudget / price);

                    if (amountToBuy > 0)
                    {
                        int totalCost = amountToBuy * price;
                        int totalPrice = amountToBuy * price;

                        mobileParty.ItemRoster.AddToCounts(entry.Element.EquipmentElement, amountToBuy);
                        settlement.ItemRoster.AddToCounts(entry.Element.EquipmentElement, -amountToBuy);

                        mobileParty.LeaderHero.ChangeHeroGold(-totalCost);
                        if (settlement.Town != null) settlement.Town.ChangeGold(totalCost);

                        currentBudget -= totalCost;
                        spentForThisLot += totalCost;
                        report.NeededCount -= amountToBuy;
                        
                        // ЛОГ: Покупка
                        string forWho = report.ForMounted ? "Конница" : "Пехота";
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"  - Куплено: {item.Name} ({amountToBuy} шт) для [{forWho}]. Цена: {totalPrice}",
                            Color.White));


                        // НОВОЕ: Продаем старое и возвращаем деньги в текущий бюджет шоппинга
                        int moneyBack = HandleReinvestment(mobileParty, settlement, item, amountToBuy);
                        if (moneyBack > 0)
                        {
                            currentBudget += moneyBack;
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"    * Кэшбэк: Продано старья на {moneyBack}",
                                Color.FromUint(0xFFFFD700))); // Золотой
                        }
                        currentBudget += moneyBack; // Увеличиваем бюджет на сумму продажи!
                    }
                }
            }
        }

        // --- ВОТ ЭТОТ МЕТОД НУЖНО ДОБАВИТЬ ВНУТРЬ КЛАССА ---
        private static bool IsSuitableForItemTask(ItemObject item, bool forMounted)
        {
            if (item.PrimaryWeapon == null) return true;

            // Если закупаем для КАВАЛЕРИИ (Конные лучники, рыцари и т.д.)
            if (forMounted)
            {
                // 1. Проверка на "пешее" оружие (то, что нельзя на коне)
                // Используем твою находку из dnSpy: ItemUsage
                if (item.PrimaryWeapon.ItemUsage != null)
                {
                    string usage = item.PrimaryWeapon.ItemUsage.ToLower();

                    // Запрет на длинные луки и тяжелые арбалеты для всадников
                    if (usage.Contains("longbow") || usage.Contains("heavy_crossbow"))
                        return false;

                    // Запрет на пехотные пики (Pike) и двуручные копья, не предназначенные для коня
                    // Обычно в Bannerlord они помечаются как "pikes" или "bracing" в Usage
                    if (usage.Contains("pike") || usage.Contains("bracing"))
                        return false;
                }

                // 2. Специфика Древкового (Polearm)
                if (item.ItemType == ItemObject.ItemTypeEnum.Polearm)
                {
                    // ПРАВИЛО: Должен быть колющий урон. 
                    // Чисто рубящие (SwingDamage > 0 при ThrustDamage <= 0) — ИГНОРИРУЕМ.
                    if (item.PrimaryWeapon.ThrustDamage <= 0)
                        return false;

                    // Дополнительная проверка на флаг "пешее" через WeaponFlags (если они доступны)
                    // Но мы уже отсекли основное через "pike" выше.
                }
            }
            // Если закупаем для ПЕХОТЫ
            else
            {
                // Пехота может брать всё, включая длинные пики и огромные луки.
                return true;
            }

            return true;
        }
        private static int HandleReinvestment(MobileParty party, Settlement settlement, ItemObject newItem, int boughtCount)
        {
            int cashback = 0;
            // Если купили Tier 2 или выше, ищем Tier 1 на продажу
            float newItemPower = MilitaryDepotLogic.GetItemPower(newItem);

            // Ищем в инвентаре предметы того же типа, но слабее (Tier 1 или просто слабые)
            var oldItems = party.ItemRoster
                .Where(x => x.EquipmentElement.Item != null &&
                            x.EquipmentElement.Item.ItemType == newItem.ItemType &&
                            MilitaryDepotLogic.GetItemPower(x.EquipmentElement.Item) < newItemPower * 0.9f) // Значительно слабее
                .ToList();

            foreach (var element in oldItems)
            {
                if (boughtCount <= 0) break;

                int toSell = Math.Min(element.Amount, boughtCount);
                int sellPrice = settlement.Town.GetItemPrice(element.EquipmentElement.Item);
                int totalSellValue = toSell * sellPrice;

                // Продажа городу
                party.ItemRoster.AddToCounts(element.EquipmentElement, -toSell);
                settlement.ItemRoster.AddToCounts(element.EquipmentElement, toSell);

                // Деньги лорду
                party.LeaderHero.ChangeHeroGold(totalSellValue);
                settlement.Town.ChangeGold(-totalSellValue);

                cashback += totalSellValue;
                boughtCount -= toSell;
            }

            return cashback;
        }
    }
}
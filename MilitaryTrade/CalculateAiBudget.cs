using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace RpgMod1
{
    public static class CalculateShoppingBudget
    {

        /// <summary>
        /// Рассчитывает финансовые возможности лорда для закупки согласно протоколу 10.04.2026.
        /// </summary>
        public static (int shoppingBudget, int tributeToClan) GetAiShoppingFinances(MobileParty mobileParty)
        {
            if (mobileParty.LeaderHero == null || mobileParty.ActualClan == null)
                return (0, 0);

            Hero leader = mobileParty.LeaderHero;
            Clan clan = mobileParty.ActualClan;
            int currentGold = leader.Gold;
            int clanGold = clan.Gold;

            // 1. Установка динамического операционного лимита (10к, 15к, 20к)
            int operationalLimit = 10000;
            if (clanGold > 500000) operationalLimit = 20000;
            else if (clanGold > 100000) operationalLimit = 15000;

            // 2. Входящая помощь (Subsidies): < 2000 у лорда и > 50000 у клана
            if (currentGold < 2000 && clanGold > 50000)
            {
                int subsidyAmount = 5000;
                leader.Gold += subsidyAmount;

                // Снимаем у главы клана (или напрямую из казны клана, если это реализовано в API)
                if (clan.Leader != null && clan.Leader != leader)
                {
                    clan.Leader.Gold -= subsidyAmount;
                }

                currentGold = leader.Gold; // Обновляем локальный счетчик после субсидии

                // Лог для отладки
                // InformationManager.DisplayMessage(new InformationMessage($"[MilitaryDepot] {leader.Name} получил субсидию {subsidyAmount}", Color.FromUint(0xFFFFFF00)));
            }

            // 3. Расчет сверхлимита
            int overLimit = currentGold - operationalLimit;

            // Если лорд не достиг лимита — на шоппинг денег нет
            if (overLimit <= 0)
            {
                return (0, 0);
            }

            // 4. Распределение: 80% на снаряжение, 20% в общак клана
            int shoppingBudget = (int)(overLimit * 0.80f);
            int tributeToClan = (int)(overLimit * 0.20f);

            return (shoppingBudget, tributeToClan);
        }
    }
    
}
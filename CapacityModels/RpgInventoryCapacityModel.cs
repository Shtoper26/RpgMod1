using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace RpgMod1.CapacityModels
{
    public class RpgInventoryCapacityModel : DefaultInventoryCapacityModel
    {
        // Копируем сигнатуру один-в-один из твоего dnSpy
        public override ExplainedNumber CalculateInventoryCapacity(MobileParty mobileParty, bool isCurrentlyAtSea, bool includeDescriptions = false, int additionalTroops = 0, int additionalSpareMounts = 0, int additionalPackAnimals = 0, bool includeFollowers = false)
        {
            // Вызываем базу с тем же набором аргументов
            ExplainedNumber result = base.CalculateInventoryCapacity(mobileParty, isCurrentlyAtSea, includeDescriptions, additionalTroops, additionalSpareMounts, additionalPackAnimals, includeFollowers);

            if (mobileParty != null && mobileParty.MemberRoster != null)
            {
                // Считаем общее количество людей (здоровых)
                int totalMen = mobileParty.MemberRoster.TotalHealthyCount;

                // Твои +30 кг. Используем заголовок из TextObject
                result.Add(totalMen * 15f, new TextObject("{=!}Снаряжение воинов (RPG)"));
            }

            return result;
        }
    }
}
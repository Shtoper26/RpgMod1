using HarmonyLib;
using RpgMod1.MilitaryTrade; // Подключаем логику из соседнего файла
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace RpgMod1.MilitaryTrade
{
    [HarmonyPatch(typeof(PartiesSellLootCampaignBehavior), "OnSettlementEntered")]
    public class TradePatches
    {
        static bool Prefix(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            // Проверяем условия (те самые, что ты нашел в коде игры)
            if (mobileParty != null && settlement != null && settlement.IsTown &&
                !mobileParty.IsMainParty && mobileParty.IsLordParty &&
                !mobileParty.IsDisbanding && !FactionManager.IsAtWarAgainstFaction(mobileParty.MapFaction, settlement.MapFaction))
            {
                // Запускаем нашу логику из militarydepottrade.cs
                MilitaryDepotTradeLogic.ProcessAiTrade(mobileParty, settlement);

                // Возвращаем false, чтобы оригинальный OnSettlementEntered не продал всё остальное
                return false;
            }
            return true;
        }
    }
}
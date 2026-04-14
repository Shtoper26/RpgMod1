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
            
            if (mobileParty != null && settlement != null && settlement.IsTown &&
                mobileParty.IsLordParty && !mobileParty.IsMainParty)
            {
                
                return false;
            }
            return true;
        }
    }
}
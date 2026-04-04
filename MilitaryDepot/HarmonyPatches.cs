using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace RpgMod1
{
    // ПАТЧ 1: Торговый ИИ (Посещение городов)
    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "AiHourlyTick")]
    public class AiVisitSettlementBehaviorPatch
    {
        static bool Prefix(MobileParty mobileParty)
        {
            if (mobileParty == null || mobileParty.MapFaction == null) return false;
            return true;
        }
    }

    // ПАТЧ 2: Боевой ИИ (Атака/Погоня)
    [HarmonyPatch(typeof(AiEngagePartyBehavior), "AiHourlyTick")]
    public class AiEngagePartyBehaviorPatch
    {
        static bool Prefix(MobileParty mobileParty)
        {
            if (mobileParty == null || mobileParty.MapFaction == null) return false;
            return true;
        }
    }

    // ПАТЧ 3: ИИ Патрулирования (Защита территорий)
    [HarmonyPatch(typeof(AiPatrollingBehavior), "AiHourlyTick")]
    public class AiPatrollingBehaviorPatch
    {
        static bool Prefix(MobileParty mobileParty)
        {
            // Если у отряда нет фракции, он не может патрулировать территорию фракции.
            if (mobileParty == null || mobileParty.MapFaction == null) return false;
            return true;
        }
    }
}
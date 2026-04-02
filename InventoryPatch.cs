using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace RpgMod1
{
    // Мы патчим конкретный метод с 4 аргументами, как просила игра
    [HarmonyPatch(typeof(DefaultInventoryCapacityModel), "GetItemEffectiveWeight")]
    public class WeightPatch
    {
        [HarmonyPostfix]
        public static void Postfix(
            ref float __result,
            EquipmentElement equipmentElement,
            MobileParty mobileParty,
            bool isCurrentlyAtSea,
            ref TextObject description)
        {
            // Делаем предметы в 10 раз легче
            //__result *= 0.1f;
        }
    }
}
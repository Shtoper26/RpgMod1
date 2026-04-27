using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace RpgMod1.CapacityModels
{
    [HarmonyPatch(typeof(MobileParty), "TotalWeightCarried", MethodType.Getter)]
    public static class MilitaryDepotWeightLogic
    {
        public static void Postfix(MobileParty __instance, ref float __result)
        {
            // Если это отряд игрока и склад создан
            if (__instance.IsMainParty && MilitaryDepotBehavior.DepotParty != null)
            {
                float depotWeight = 0f;
                ItemRoster roster = MilitaryDepotBehavior.DepotParty.ItemRoster;

                // Считаем вес на складе вручную, так как в ItemRoster нет TotalWeight
                for (int i = 0; i < roster.Count; i++)
                {
                    var element = roster[i];
                    if (element.EquipmentElement.Item != null)
                    {
                        depotWeight += element.EquipmentElement.Item.Weight * element.Amount;
                    }
                }

                // Плюсуем к итоговому результату
                __result += depotWeight;
            }
        }
    }
}
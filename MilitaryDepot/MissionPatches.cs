using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RpgMod1
{
    [HarmonyPatch(typeof(Mission), "SpawnAgent")]
    public class MissionSpawnPatch
    {
        static void Prefix(AgentBuildData agentBuildData)
        {
            // Проверяем, что это обычный солдат игрока
            if (agentBuildData.AgentCharacter != null && agentBuildData.AgentTeam != null &&
                agentBuildData.AgentTeam.IsPlayerTeam && !agentBuildData.AgentCharacter.IsHero)
            {
                CharacterObject character = agentBuildData.AgentCharacter as CharacterObject;

                // Просто берем следующую зарезервированную экипировку для этого типа юнита
                Equipment customEquip = MilitaryDepotCache.GetPredefinedEquipment(character);

                if (customEquip != null)
                {
                    agentBuildData.Equipment(customEquip);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "AfterStart")]
    public class MissionStartPatch
    {
        static void Postfix()
        {
            if (MilitaryDepotBehavior.DepotParty != null && TaleWorlds.CampaignSystem.Party.PartyBase.MainParty != null)
            {
                // Формируем план распределения ПЕРЕД началом боя
                MilitaryDepotCache.CreateBattlePlan(
                    MilitaryDepotBehavior.DepotParty.ItemRoster,
                    TaleWorlds.CampaignSystem.Party.PartyBase.MainParty.MemberRoster
                );
            }
        }
    }
}
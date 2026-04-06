using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace RpgMod1
{
    [HarmonyPatch(typeof(Mission), "SpawnAgent")]
    public class MissionSpawnPatch
    {
        static void Prefix(AgentBuildData agentBuildData)
        {
            if (agentBuildData.AgentCharacter != null && !agentBuildData.AgentCharacter.IsHero)
            {
                // Пытаемся достать PartyBase через интерфейс IAgentOriginBase
                IAgentOriginBase origin = agentBuildData.AgentOrigin;
                if (origin == null) return;

                // В Bannerlord почти все отряды в миссии — это PartyAgentOrigin или SimpleAgentOrigin
                // Мы ищем тот, у которого есть свойство .Party
                PropertyInfo partyProperty = origin.GetType().GetProperty("Party");
                PartyBase partyBase = partyProperty?.GetValue(origin) as PartyBase;

                if (partyBase != null && partyBase.MobileParty != null)
                {
                    MobileParty mobileParty = partyBase.MobileParty;
                    CharacterObject character = agentBuildData.AgentCharacter as CharacterObject;

                    Equipment customEquip = MilitaryDepotCache.GetPredefinedEquipment(character);
                    if (customEquip != null)
                    {
                        agentBuildData.Equipment(customEquip);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "AfterStart")]
    public class MissionStartPatch
    {
        private static Mission _activeMission;
        static void Postfix(Mission __instance)
        {
            if (_activeMission == __instance) return; // И это
            _activeMission = __instance;

            MilitaryDepotCache.Clear(); 

            
            InformationManager.DisplayMessage(new InformationMessage("[Склад] Инициализация боя...", Color.FromUint(0xFFFFFF00)));

            // 1. Пытаемся взять глобальное событие
            MapEvent playerEvent = MapEvent.PlayerMapEvent;

            // 2. Если глобального события нет, берем всех участников из самой миссии (для тестов и стычек)
            if (playerEvent != null)
            {
                foreach (PartyBase partyBase in playerEvent.InvolvedParties)
                {
                    ProcessParty(partyBase);
                }
            }
            else
            {
                // Запасной вариант: берем лидера каждой команды в миссии
                foreach (Team team in __instance.Teams)
                {
                    foreach (Agent agent in team.ActiveAgents)
                    {
                        if (agent.IsHero && agent.Origin is PartyAgentOrigin partyOrigin)
                        {
                            ProcessParty(partyOrigin.Party);
                        }
                    }
                }
            }
        }

        private static void ProcessParty(PartyBase partyBase)
        {
            if (partyBase == null || partyBase.MobileParty == null) return;
            MobileParty mobileParty = partyBase.MobileParty;

            // ИСКЛЮЧАЕМ жителей деревень, чтобы они не выходили голыми (у них пустые инвентари)
            if (mobileParty.IsVillager) return;

            if (mobileParty.IsMainParty)
            {
                if (MilitaryDepotBehavior.DepotParty != null)
                {
                    MilitaryDepotCache.CreateBattlePlan(mobileParty, MilitaryDepotBehavior.DepotParty.ItemRoster);
                    InformationManager.DisplayMessage(new InformationMessage($"[Склад] План для Игрока создан.", Color.FromUint(0xFF00FF00)));
                }
            }
            else
            {
                MilitaryDepotCache.CreateBattlePlan(mobileParty, mobileParty.ItemRoster);
                InformationManager.DisplayMessage(new InformationMessage($"[Склад] План для {mobileParty.Name} создан.", Color.FromUint(0xFF00FF00)));
            }
        }
    }
}
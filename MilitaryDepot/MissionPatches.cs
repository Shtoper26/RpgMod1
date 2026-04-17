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
        // В классе MissionSpawnPatch:
        static void Prefix(AgentBuildData agentBuildData)
        {
            if (agentBuildData.AgentCharacter != null && !agentBuildData.AgentCharacter.IsHero)
            {
                IAgentOriginBase origin = agentBuildData.AgentOrigin;
                if (origin == null) return;

                PropertyInfo partyProperty = origin.GetType().GetProperty("Party");
                PartyBase partyBase = partyProperty?.GetValue(origin) as PartyBase;

                if (partyBase != null && partyBase.MobileParty != null)
                {
                    MobileParty mobileParty = partyBase.MobileParty;
                    CharacterObject character = agentBuildData.AgentCharacter as CharacterObject;

                    // ПЕРЕДАЕМ mobileParty, чтобы получить ЕГО план, а не общий
                    Equipment customEquip = MilitaryDepotCache.GetPredefinedEquipment(mobileParty, character);

                    if (customEquip != null)
                    {
                        agentBuildData.Equipment(customEquip);

                        // логи для отладки
                        // Собираем список всех предметов, которые сейчас надеты на юнита
                        System.Collections.Generic.List<string> itemsNames = new System.Collections.Generic.List<string>();

                        // Проверяем все 12 слотов (оружие, броня, конь)
                        for (int i = 0; i < 12; i++)
                        {
                            var element = customEquip[(EquipmentIndex)i];
                            if (element.Item != null)
                            {
                                // Добавляем название предмета в список
                                itemsNames.Add(element.Item.Name.ToString());
                            }
                        }

                        string partyName = mobileParty.Name.ToString();
                        string unitName = character.Name.ToString();
                        string allItems = string.Join(", ", itemsNames);

                        // Выводим детальный лог
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[Склад] {partyName} | {unitName} одет в: {allItems}",
                            mobileParty.IsMainParty ? Color.FromUint(0xFF00FF00) : Color.FromUint(0xFFFFFF00)));
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

            // Логи для отладки

            string source = mobileParty.IsMainParty ? "Военный склад" : "Инвентарь отряда";

            // Одна короткая строка: [Склад] Инициализация: Морские налетчики (Источник: Инвентарь отряда)
            InformationManager.DisplayMessage(new InformationMessage(
                $"[Склад] Инициализация: {mobileParty.Name} (Источник: {source})",
                Color.FromUint(0xFFFFFF00)));
        }
    }
}
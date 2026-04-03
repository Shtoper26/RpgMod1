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
            if (agentBuildData.AgentCharacter != null && agentBuildData.AgentTeam != null &&
                agentBuildData.AgentTeam.IsPlayerTeam && !agentBuildData.AgentCharacter.IsHero)
            {
                if (MilitaryDepotBehavior.TempBattleLoot == null)
                    MilitaryDepotActions.PrepareTempLoot();

                CharacterObject character = agentBuildData.AgentCharacter as CharacterObject;
                CharacterObject firstTier = MilitaryDepotLogic.GetFirstTierCharacter(character);

                // Если не нашли базового юнита, выходим (защита от вылета)
                if (firstTier == null) return;

                Equipment tier1Equip = firstTier.FirstBattleEquipment;
                Equipment currentTierEquip = character.FirstBattleEquipment;
                Equipment customEquip = new Equipment();
                bool isChanged = false;

                for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.Cape; i++)
                {
                    // Пытаемся достать лучшее со склада
                    EquipmentElement best = (i <= EquipmentIndex.Weapon3) ?
                        MilitaryDepotActions.ExtractBestWeaponForSlot(currentTierEquip[i], MilitaryDepotBehavior.TempBattleLoot) :
                        MilitaryDepotActions.ExtractBestForSlot(i);

                    if (!best.IsEmpty)
                    {
                        customEquip[i] = best;
                        isChanged = true;
                        MilitaryDepotLogs.LogWeaponIssued(character.Name.ToString(), best.Item.Name.ToString());
                    }
                    else
                    {
                        // Если на складе НЕТ ничего лучше, МЫ ОБЯЗАТЕЛЬНО берем вещь 1-го тира
                        // Это гарантирует, что юнит не выйдет в дефолтном (сильном) шмоте
                        customEquip[i] = tier1Equip[i];
                        isChanged = true; // Ставим true, чтобы принудительно обновить экипировку на базовую
                    }
                }

                // Применяем собранный комплект (лучшее + базовое)
                if (isChanged) agentBuildData.Equipment(customEquip);
            }
        }
    }
}
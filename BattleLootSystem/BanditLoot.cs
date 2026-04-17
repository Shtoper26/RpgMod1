using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System;

namespace RpgMod1.BattleLootSystem
{
    [HarmonyPatch(typeof(MobileParty), "CreateParty")]
    public class BanditLootSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MobileParty __result)
        {
            if (__result == null || __result.MemberRoster == null) return;
            if (__result.IsMainParty || __result.IsLordParty) return;

            float lootChance = 0f;
            string id = __result.StringId?.ToLower() ?? "";
            // Получаем культуру через Party (безопасный способ)
            
            string cultureId = __result.Party?.Culture?.StringId?.ToLower() ?? "";

            // 60% для элиты и патрулей
            bool isElite = id.Contains("sea_raiders") || cultureId.Contains("sea_raiders") || id.Contains("pirate");
            bool isPatrol = __result.PartyComponent is PatrolPartyComponent;

            // 30% для обычных бандитов и дезертиров
            bool isBandit = __result.IsBandit || id.Contains("deserter") || cultureId.Contains("deserter");

            if (isElite || isPatrol)
            {
                lootChance = 0.6f;
            }
            else if (isBandit)
            {
                lootChance = 0.3f;
            }

            if (lootChance > 0)
            {
                AddMassiveTemplateLoot(__result, lootChance);
            }
        }

        private static void AddMassiveTemplateLoot(MobileParty party, float chance)
        {
            if (party.MemberRoster.Count == 0 || party.ItemRoster == null) return;

            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                CharacterObject character = element.Character;

                if (character == null || character.IsHero) continue;

                int troopCount = element.Number;
                float exactCount = troopCount * chance;
                int luckyTroops = (int)exactCount;
                if (MBRandom.RandomFloat < (exactCount - luckyTroops)) luckyTroops++;

                if (luckyTroops <= 0) continue;

                Equipment equipment = character.FirstBattleEquipment;
                if (equipment == null) continue;

                for (int slot = 0; slot < 12; slot++)
                {
                    EquipmentElement eqElement = equipment[(EquipmentIndex)slot];
                    ItemObject item = eqElement.Item;

                    if (item != null)
                    {
                        // 1. Проверяем, является ли предмет живой лошадью
                        bool isLivingHorse = item.ItemType == ItemObject.ItemTypeEnum.Horse;
                        
                        // 2. Проверяем, является ли предмет оружием или броней (включая конскую броню)
                        bool isWeapon = item.WeaponComponent != null;
                        bool isArmor = item.ArmorComponent != null;
                        bool isHorseHarness = item.ItemType == ItemObject.ItemTypeEnum.HorseHarness;

                        // Условие: Добавляем только если это НЕ живая лошадь И это (Оружие ИЛИ Любая Броня)
                        if (!isLivingHorse && (isWeapon || isArmor || isHorseHarness))
                        {
                            party.ItemRoster.AddToCounts(eqElement, luckyTroops);
                        }
                    }
                }
            }
        }
    }
}
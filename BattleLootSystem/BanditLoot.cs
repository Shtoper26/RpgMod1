using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Helpers; 

namespace RpgMod1.BattleLootSystem
{
    public class BanditLootBehavior : CampaignBehaviorBase
    {
        private static HashSet<string> _processedParties = new HashSet<string>();

        public override void RegisterEvents()
        {
            // 1. Вход для обычных бандитов (срабатывает при создании)
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, TryAddLoot);
            // Очистка при загрузке
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (OnGameLoadedDelegate));
        }

        private void OnGameLoadedDelegate(CampaignGameStarter starter)
        {
            _processedParties.Clear();
        }

        public override void SyncData(IDataStore dataStore) { }

        public static void TryAddLoot(MobileParty party)
        {
            if (party == null || party.StringId == null) return;
            
            // Если отряд уже обработан — выходим
            if (_processedParties.Contains(party.StringId)) return;

            // Если в отряде еще нет людей (как у дезертиров в момент Created) — выходим, 
            // их подхватит патч SortRoster позже
            if (party.MemberRoster == null || party.MemberRoster.Count == 0) return;

            // БЛОКИРОВКА МИРНЫХ ЖИТЕЛЕЙ: Крестьяне, караваны и лорды не получают этот лут
            if (party.IsVillager || party.IsCaravan || party.IsLordParty || party.IsMainParty) return;

            string id = party.StringId.ToLower();
            string cultureId = party.Party?.Culture?.StringId?.ToLower() ?? "";

            // Определение категорий
            bool isDeserter = id.Contains("deserter") || cultureId.Contains("deserter");
            bool isPatrol = party.PartyComponent is PatrolPartyComponent;
            // Морские налетчики часто имеют культуру "nord"
            bool isEliteBandit = id.Contains("sea_raiders") || cultureId.Contains("sea_raiders") || 
                               id.Contains("pirate") || cultureId.Contains("nord");
            
            bool isNormalBandit = party.IsBandit || isEliteBandit;

            // Фильтр: только бандиты, дезертиры или патрули
            if (!isNormalBandit && !isDeserter && !isPatrol) return;
            
            if (party.IsMainParty || party.IsLordParty || id.Contains("military_depot_party")) return;

            // Шансы
            float lootChance = 0.3f;
            if (isEliteBandit || isPatrol)
            {
                lootChance = 0.6f;
            }

            // Добавляем в список обработанных ДО начисления, чтобы избежать рекурсии
            _processedParties.Add(party.StringId);
            
            AddPriorityLoot(party, lootChance);
        }

        private static void AddPriorityLoot(MobileParty party, float chance)
        {
            int totalTroops = party.MemberRoster.TotalManCount;
            float rawLootCount = totalTroops * chance;
            int totalSlots = (int)rawLootCount;
            
            if (MBRandom.RandomFloat < (rawLootCount - totalSlots)) totalSlots++;
            if (totalSlots <= 0) return;

            var highTierTroops = party.MemberRoster.GetTroopRoster()
                .Where(t => t.Character != null && !t.Character.IsHero && t.Character.Tier > 1)
                .OrderByDescending(t => t.Character.Tier)
                .ToList();

            if (highTierTroops.Count == 0) return;

            int remainingSlots = totalSlots;
            foreach (var element in highTierTroops)
            {
                if (remainingSlots <= 0) break;
                int countToGive = Math.Min(element.Number, remainingSlots);
                
                if (countToGive > 0)
                {
                    AddTemplateItems(party, element.Character, countToGive);
                    remainingSlots -= countToGive;
                }
            }
        }

        private static void AddTemplateItems(MobileParty party, CharacterObject character, int count)
        {
            Equipment equipment = character.FirstBattleEquipment;
            if (equipment == null) return;

            for (int i = 0; i < 12; i++)
            {
                ItemObject item = equipment[(EquipmentIndex)i].Item;
                if (item != null)
                {
                    bool isWeapon = item.WeaponComponent != null;
                    bool isArmor = item.ArmorComponent != null;
                    bool isHarness = item.ItemType == ItemObject.ItemTypeEnum.HorseHarness;
                    bool isNotHorse = item.ItemType != ItemObject.ItemTypeEnum.Horse;

                    if (isNotHorse && (isWeapon || isArmor || isHarness))
                    {
                        party.ItemRoster.AddToCounts(item, count);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PartyBaseHelper), "SortRoster")]
    public class BanditLootSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MobileParty mobileParty)
        {
            // Этот патч подхватит дезертиров и те отряды, которые не прошли через MobilePartyCreated с воинами
            BanditLootBehavior.TryAddLoot(mobileParty);
        }
    }
}
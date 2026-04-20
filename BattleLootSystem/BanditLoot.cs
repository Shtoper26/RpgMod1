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
using Helpers; // Для PartyBaseHelper

namespace RpgMod1.BattleLootSystem
{
    // Этот класс будет хранить логику и предотвращать повторное добавление лута
    public class BanditLootBehavior : CampaignBehaviorBase
    {
        // Список ID отрядов, которые уже получили лут, чтобы не добавлять его дважды
        private static HashSet<string> _processedParties = new HashSet<string>();

        public override void RegisterEvents()
        {
            // Очищаем список при загрузке игры
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (OnGameLoadedDelegate));
        }

        private void OnGameLoadedDelegate(CampaignGameStarter starter)
        {
            _processedParties.Clear();
        }

        public override void SyncData(IDataStore dataStore) { }

        // Метод для проверки и начисления лута
        public static void TryAddLoot(MobileParty party)
        {
            if (party == null || party.StringId == null) return;
            if (_processedParties.Contains(party.StringId)) return;

            // Проверки на тип отряда
            bool isDeserter = party.StringId.ToLower().Contains("deserter");
            bool isBandit = party.IsBandit || isDeserter;

            if (!isBandit) return;
            if (party.IsMainParty || party.IsLordParty) return;
            if (party.StringId.Contains("military_depot_party")) return;

            // Если ростер все еще пуст - выходим (попробуем в следующий раз)
            if (party.MemberRoster == null || party.MemberRoster.Count == 0) return;

            float lootChance = isDeserter ? 0.3f : 0.3f; // Базовый шанс
            string cultureId = party.Party?.Culture?.StringId?.ToLower() ?? "";

            // Элитные бандиты (Морские налетчики и т.д.)
            if (cultureId.Contains("sea_raiders") || party.StringId.ToLower().Contains("sea_raiders"))
                lootChance = 0.6f;

            _processedParties.Add(party.StringId);
            AddPriorityLoot(party, lootChance);
        }

        private static void AddPriorityLoot(MobileParty party, float chance)
        {
            int totalTroops = party.MemberRoster.TotalManCount;
            int totalSlots = (int)(totalTroops * chance);
            if (MBRandom.RandomFloat < (totalTroops * chance - totalSlots)) totalSlots++;

            if (totalSlots <= 0) return;

            // Сортировка по Тиру (от 6 до 2)
            var highTierTroops = party.MemberRoster.GetTroopRoster()
                .Where(t => t.Character != null && !t.Character.IsHero && t.Character.Tier > 1)
                .OrderByDescending(t => t.Character.Tier)
                .ToList();

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
                    if (item.WeaponComponent != null || item.ArmorComponent != null || item.ItemType == ItemObject.ItemTypeEnum.HorseHarness)
                    {
                        if (item.ItemType != ItemObject.ItemTypeEnum.Horse)
                        {
                            party.ItemRoster.AddToCounts(item, count);
                        }
                    }
                }
            }
        }
    }

    // Патч на SortRoster — это самый надежный способ поймать Дезертиров
    // так как он вызывается сразу ПОСЛЕ заполнения их отряда войсками
    [HarmonyPatch(typeof(PartyBaseHelper), "SortRoster")]
    public class BanditLootSpawnPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MobileParty mobileParty)
        {
            // Вызываем логику проверки и начисления
            BanditLootBehavior.TryAddLoot(mobileParty);
        }
    }
}
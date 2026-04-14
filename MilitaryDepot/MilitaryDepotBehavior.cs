using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1
{
    public class MilitaryDepotBehavior : CampaignBehaviorBase
    {
        public static MobileParty DepotParty;
        public static ItemRoster TempBattleLoot;
        public static HashSet<ItemObject> NeededItems = new HashSet<ItemObject>();

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<MobileParty>("_militaryDepotParty", ref DepotParty);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // Проверяем наличие события и склада
            if (mapEvent == null || DepotParty == null) return;

            // 1. В 1.3.x используем BattleSideEnum для сторон
            BattleSideEnum winnerSide = mapEvent.WinningSide;

            // Если победителя нет (побег или ничья) - выходим
            if (winnerSide == BattleSideEnum.None) return;

            // Определяем сторону проигравших
            BattleSideEnum loserSide = (winnerSide == BattleSideEnum.Attacker) ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

            // 2. Находим лидера победившей стороны
            PartyBase mainWinner = mapEvent.GetLeaderParty(winnerSide);
            if (mainWinner == null) return;

            // 3. Собираем список погибших с проигравшей стороны
            List<CharacterObject> casualties = new List<CharacterObject>();

            // Временный лог что контролировать сбор лута ИИ 
            //if (mainWinner.MobileParty != null && mainWinner.MobileParty.IsLordParty)
            //{
            //    InformationManager.DisplayMessage(new InformationMessage($"[Debug] Лорд {mainWinner.Name} собрал лут после боя."));
            //}
            if (mainWinner.MobileParty != null && !mainWinner.MobileParty.IsVillager)
            {
                string partyName = mainWinner.Name.ToString();
                string message;

                // Проверяем, является ли победитель лордом или игроком
                if (mainWinner.MobileParty.IsLordParty || mainWinner.MobileParty.IsMainParty)
                {
                    message = $"[Debug] Лорд {partyName} собрал лут после боя.";
                }
                else
                {
                    // Для бандитов, караванов и прочих
                    message = $"[Debug] Отряд {partyName} собрал лут после боя.";
                }

                // Вывод сообщения в игровой лог (желтым цветом для заметности)
                InformationManager.DisplayMessage(new InformationMessage(message, Color.FromUint(0xFFFFFF00)));
            }



            // Перебираем все отряды на проигравшей стороне
            foreach (MapEventParty loserParty in mapEvent.PartiesOnSide(loserSide))
            {
                PartyBase pBase = loserParty.Party;
                if (pBase == null || pBase.MemberRoster == null) continue;

                // Считаем потери: общее число минус здоровые
                int lostCount = pBase.MemberRoster.TotalManCount - pBase.MemberRoster.TotalHealthyCount;

                if (lostCount > 0 && pBase.MemberRoster.Count > 0)
                {
                    for (int i = 0; i < lostCount; i++)
                    {
                        // Берем случайного юнита из тех, кто был в бою
                        var element = pBase.MemberRoster.GetElementCopyAtIndex(MBRandom.RandomInt(pBase.MemberRoster.Count));
                        if (element.Character != null && !element.Character.IsHero)
                        {
                            casualties.Add(element.Character);
                        }
                    }
                }
            }

            // 4. Шанс выпадения предмета (50%)
            float lootChance = 1f;

            foreach (CharacterObject victim in casualties)
            {
                if (victim.FirstBattleEquipment == null) continue;

                for (EquipmentIndex slot = EquipmentIndex.Weapon0; slot <= EquipmentIndex.Cape; slot++)
                {
                    EquipmentElement item = victim.FirstBattleEquipment[slot];

                    if (item.Item != null && MBRandom.RandomFloat < lootChance)
                    {
                        
                        if (mainWinner == PartyBase.MainParty)                        
                        {
                            DepotParty.ItemRoster.AddToCounts(item.Item, 1);
                        }
                        // Если победил ИИ Лорд
                        else if (mainWinner.MobileParty != null && !mainWinner.MobileParty.IsVillager)
                        {
                            mainWinner.ItemRoster.AddToCounts(item.Item, 1);
                        }
                    }
                }
                 

                
            }

            TempBattleLoot = null;
        }
    }
}
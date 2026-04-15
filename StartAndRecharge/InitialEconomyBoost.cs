using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1.StartAndRecharge
{
    public class InitialEconomyBoost : CampaignBehaviorBase
    {
        // Регистрация событий игры
        public override void RegisterEvents()
        {
            // Используем событие загрузки кампании
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            // Используем событие старта новой кампании
            CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Здесь ничего сохранять не нужно
        }

        private void OnNewGameCreated()
        {
            // Срабатывает один раз при создании персонажа и выходе на карту
            ApplyInitialBoost();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            // Проверка для подстраховки: если игра только началась (день 1), но буст почему-то не прошел
            if (CampaignTime.Now.ToDays <= 1.1f)
            {
                ApplyInitialBoost();
            }
        }

        public void ApplyInitialBoost()
        {
            if (MobileParty.All == null) return;

            int totalPartiesBoosted = 0;
            int totalItemsAdded = 0;

            // Клонируем список, чтобы избежать ошибок изменения коллекции
            var allParties = MobileParty.All.ToList();

            foreach (MobileParty party in allParties)
            {
                if (party != null && party.IsLordParty && party.LeaderHero != null && party.MemberRoster != null)
                {
                    int itemsForThisParty = 0;

                    foreach (var troopElement in party.MemberRoster.GetTroopRoster())
                    {
                        CharacterObject character = troopElement.Character;

                        if (character == null || character.IsHero) continue;
                        

                        int troopCount = troopElement.Number;
                        Equipment equipment = character.FirstBattleEquipment;
                        if (equipment == null) continue;

                        // Цикл по слотам снаряжения
                        for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
                        {
                            if (i == EquipmentIndex.ArmorItemEndSlot) continue; // Пропуск слота коня (Index 4)

                            ItemObject item = equipment[i].Item;
                            if (item != null && !item.IsMountable)
                            {
                                party.ItemRoster.AddToCounts(equipment[i], troopCount);
                                itemsForThisParty += troopCount;
                                totalItemsAdded += troopCount;
                            }
                        }
                    }

                    if (itemsForThisParty > 0)
                    {
                        totalPartiesBoosted++;
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[EconomyBoost] Лорд {party.LeaderHero.Name} получил {itemsForThisParty} ед. снаряжения.",
                            Color.FromUint(0xFF42FF00)));
                    }
                }
            }

            if (totalPartiesBoosted > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[EconomyBoost] Глобальный впрыск завершен: {totalPartiesBoosted} отрядов, {totalItemsAdded} предметов.",
                    Color.FromUint(0xFF00FFFF)));
            }
        }
    }
}
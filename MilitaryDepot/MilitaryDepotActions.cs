using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1
{
    public static class MilitaryDepotActions
    {
        public static void OpenDepot()
        {
            if (MilitaryDepotBehavior.DepotParty == null)
            {
                foreach (var party in MobileParty.All)
                {
                    if (party.StringId == "military_depot_party")
                    {
                        MilitaryDepotBehavior.DepotParty = party;
                        break;
                    }
                }

                if (MilitaryDepotBehavior.DepotParty == null)
                {
                    MilitaryDepotBehavior.DepotParty = MilitaryDepotComponent.CreateDepotParty("military_depot_party", Hero.MainHero.Culture);
                    MilitaryDepotBehavior.DepotParty.IsVisible = false;
                    MilitaryDepotBehavior.DepotParty.ActualClan = Clan.PlayerClan;
                    MilitaryDepotBehavior.DepotParty.IsActive = true;
                }
            }

            UpdateNeededItemsList();
            InventoryScreenHelper.OpenScreenAsInventoryOf(
                PartyBase.MainParty,
                MilitaryDepotBehavior.DepotParty.Party,
                CharacterObject.PlayerCharacter,
                null, null, null
            );
        }

        public static void TransferUselessItems()
        {
            if (MilitaryDepotBehavior.DepotParty == null || PartyBase.MainParty == null) return;
            UpdateNeededItemsList();
            ItemRoster depot = MilitaryDepotBehavior.DepotParty.ItemRoster;
            ItemRoster player = PartyBase.MainParty.ItemRoster;
            int count = 0;
            for (int i = depot.Count - 1; i >= 0; i--)
            {
                ItemRosterElement el = depot[i];
                if (el.EquipmentElement.Item != null && !MilitaryDepotBehavior.NeededItems.Contains(el.EquipmentElement.Item))
                {
                    player.AddToCounts(el.EquipmentElement, el.Amount);
                    depot.Remove(el);
                    count++;
                }
            }
            MilitaryDepotLogs.LogTransferComplete(count);
        }

        public static void UpdateNeededItemsList()
        {
            MilitaryDepotBehavior.NeededItems.Clear();
            if (MilitaryDepotBehavior.DepotParty == null) return;

            // Создаем копию склада для симуляции выдачи
            ItemRoster simRoster = new ItemRoster(MilitaryDepotBehavior.DepotParty.ItemRoster);
            
            // ПРАВИЛО 1: Сначала выдача самым лучшим юнитам (высокий уровень/тир)
            var sortedTroops = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .OrderByDescending(t => t.Character.Level)
                .ToList();

            foreach (var element in sortedTroops)
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsHero) continue;

                // ПРАВИЛО 8-9: Находим эталон для отката (Тир 1 для профи, Тир 0 для новобранцев)
                CharacterObject referenceUnit = MilitaryDepotLogic.GetReferenceUnit(character);

                for (int n = 0; n < element.Number; n++)
                {
                    Equipment troopEquip = character.FirstBattleEquipment;

                    // Проверяем все слоты от Оружия 0 до Сбруи коня
                    for (EquipmentIndex s = EquipmentIndex.Weapon0; s <= EquipmentIndex.HorseHarness; s++)
                    {
                        // ПРАВИЛО 6: Лошадей не выдаем
                        if (s == EquipmentIndex.Horse) continue;

                        ItemObject maskItem = troopEquip[s].Item;
                        EquipmentElement bestFromDepot = EquipmentElement.Invalid;
                        
                        // ПРАВИЛО 5б: Проверяем наличие щита у юнита
                        bool hasShield = HasShieldInWeapons(troopEquip);

                        // ПРАВИЛО 3-5: Подбор снаряжения
                        if (s <= EquipmentIndex.Weapon3)
                        {
                            bestFromDepot = ExtractBestWeaponForSlot(maskItem, simRoster, character, hasShield);
                        }
                        else
                        {
                            // ПРАВИЛО 4: Выдача брони в пустые слоты шаблона
                            bestFromDepot = ExtractBestArmorForSlot(s, simRoster, character);
                        }

                        // ПРАВИЛО 8-9: Сравнение с эталоном линейки (Тир 1 или Тир 0)
                        if (referenceUnit != null)
                        {
                            ItemObject refItem = referenceUnit.FirstBattleEquipment[s].Item;
                            if (refItem != null)
                            {
                                float refPower = MilitaryDepotLogic.GetItemPower(refItem);
                                float depotPower = bestFromDepot.IsEmpty ? -1f : MilitaryDepotLogic.GetItemPower(bestFromDepot.Item);

                                // Если предмет эталона лучше того, что нашли на складе - берем эталон
                                if (refPower > depotPower)
                                {
                                    MilitaryDepotBehavior.NeededItems.Add(refItem);
                                    continue;
                                }
                            }
                        }

                        // ПРАВИЛО 2: Если на складе нашлось что-то лучше, добавляем в "Нужное"
                        if (!bestFromDepot.IsEmpty)
                        {
                            MilitaryDepotBehavior.NeededItems.Add(bestFromDepot.Item);
                        }
                    }
                }
            }
        }

        private static bool HasShieldInWeapons(Equipment equip)
        {
            for (int i = 0; i < 4; i++)
                if (equip[i].Item != null && equip[i].Item.ItemType == ItemObject.ItemTypeEnum.Shield) return true;
            return false;
        }

        public static EquipmentElement ExtractBestArmorForSlot(EquipmentIndex slot, ItemRoster roster, CharacterObject character)
        {
            int bestIdx = -1; float maxPower = -1f;
            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject item = roster[i].EquipmentElement.Item;
                if (MilitaryDepotLogic.IsItemForArmorSlot(item, slot))
                {
                    float p = MilitaryDepotLogic.GetItemPower(item);
                    if (p > maxPower) { maxPower = p; bestIdx = i; }
                }
            }
            if (bestIdx != -1)
            {
                EquipmentElement res = roster[bestIdx].EquipmentElement;
                roster.AddToCounts(res, -1);
                return res;
            }
            return EquipmentElement.Invalid;
        }

        public static EquipmentElement ExtractBestWeaponForSlot(ItemObject maskItem, ItemRoster roster, CharacterObject character, bool hasShield)
        {
            int bestIdx = -1; float maxPower = -1f;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject repoItem = roster[i].EquipmentElement.Item;
                if (repoItem == null || repoItem.PrimaryWeapon == null) continue;

                // ПРАВИЛО 10: Косы/мотыги не выдаем никому выше Тира 0
                if (character.Tier > 0 && IsFarmTool(repoItem)) continue;

                // ПРАВИЛО 11-14: Навыки, Кавалерия и Конные лучники
                if (!IsWeaponAllowedForUnit(repoItem, character)) continue;

                bool isMatch = false;

                // ПРАВИЛО 3 и 5а: Соответствие типу из шаблона (Лук к луку и т.д.)
                if (maskItem != null && repoItem.ItemType == maskItem.ItemType)
                {
                    isMatch = true;
                }
                // ПРАВИЛО 5а: Если нет точного типа, но совпадает навык (одноручное к одноручному)
                else if (maskItem != null && maskItem.PrimaryWeapon != null && 
                         maskItem.PrimaryWeapon.RelevantSkill == repoItem.PrimaryWeapon.RelevantSkill)
                {
                    isMatch = true;
                }
                // ПРАВИЛО 5б: Выдача щита в пустой слот
                else if (maskItem == null && !hasShield && repoItem.ItemType == ItemObject.ItemTypeEnum.Shield)
                {
                    isMatch = true;
                }

                if (isMatch)
                {
                    float p = MilitaryDepotLogic.GetItemPower(repoItem);
                    if (p > maxPower) { maxPower = p; bestIdx = i; }
                }
            }

            if (bestIdx != -1)
            {
                EquipmentElement res = roster[bestIdx].EquipmentElement;
                roster.AddToCounts(res, -1);
                return res;
            }
            return EquipmentElement.Invalid;
        }

        private static bool IsWeaponAllowedForUnit(ItemObject item, CharacterObject character)
        {
            var weapon = item.PrimaryWeapon;

            // ПРАВИЛО 11: Навык владения луком
            if (item.ItemType == ItemObject.ItemTypeEnum.Bow && character.GetSkillValue(DefaultSkills.Bow) < item.Difficulty)
                return false;

            if (character.IsMounted)
            {
                string usage = weapon.ItemUsage?.ToLower() ?? "";
                
                // ПРАВИЛО 14: Лук верхом (отсекаем Longbow)
                if (item.ItemType == ItemObject.ItemTypeEnum.Bow && usage.Contains("longbow")) return false;

                // ПРАВИЛО 13: Оружие, не предназначенное для коня (Пики)
                if (usage.Contains("pike") || usage.Contains("bracing")) return false;

                // ПРАВИЛО 12: Древковое для кавалерии ОБЯЗАТЕЛЬНО должно иметь колющий урон
                if (item.ItemType == ItemObject.ItemTypeEnum.Polearm && weapon.ThrustDamage <= 0) return false;
            }
            return true;
        }

        private static bool IsFarmTool(ItemObject item)
        {
            string id = item.StringId?.ToLower() ?? "";
            return id.Contains("scythe") || id.Contains("hoe") || id.Contains("sickle");
        }
    }
}
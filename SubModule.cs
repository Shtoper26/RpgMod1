using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RpgMod1
{
    // 1. ГЛАВНЫЙ КЛАСС (Загрузка и Кнопка)
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            // Инициализируем Harmony для работы патча переодевания
            new Harmony("com.rpgmod.supply").PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            CampaignGameStarter campaignStarter = gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new MilitaryDepotBehavior());
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            // Кнопка открытия Обоза (работает на паузе)
            if (Input.IsKeyPressed(InputKey.K))
            {
                if (Campaign.Current != null && GameStateManager.Current != null && GameStateManager.Current.ActiveState is MapState)
                {
                    MilitaryDepotBehavior.OpenDepot();
                }
            }
        }
    }

    // 2. ПАТЧ ПЕРЕОДЕВАНИЯ (Срабатывает при создании каждого солдата в бою)
    // ПАТЧ ДЛЯ СПАВНА
    [HarmonyPatch(typeof(Mission), "SpawnAgent")]
    public class MissionSpawnPatch
    {
        static void Prefix(AgentBuildData agentBuildData)
        {
            if (agentBuildData.AgentCharacter != null &&
                agentBuildData.AgentTeam != null &&
                agentBuildData.AgentTeam.IsPlayerTeam &&
                !agentBuildData.AgentCharacter.IsHero)
            {
                if (MilitaryDepotBehavior.TempBattleLoot == null)
                    MilitaryDepotBehavior.PrepareTempLoot();

                string unitName = agentBuildData.AgentCharacter.Name.ToString();

                // 1. Находим "Рекрута" (1-й тир этой ветки)
                CharacterObject firstTierCharacter = GetFirstTierCharacter(agentBuildData.AgentCharacter as CharacterObject);
                if (firstTierCharacter == null) return; // Если не нашли шаблон, выходим
                Equipment tier1Equip = firstTierCharacter.FirstBattleEquipment;

                // 2. Берем родное снаряжение текущего уровня юнита для сравнения
                Equipment currentTierEquip = agentBuildData.AgentCharacter.FirstBattleEquipment;

                Equipment customEquip = new Equipment(); // Создаем абсолютно новый набор

                // --- ЦИКЛ ПО ВСЕМ СЛОТАМ (Броня и Оружие) ---
                for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.Cape; i++)
                {
                    EquipmentElement bestFromDepot = EquipmentElement.Invalid;

                    // Проверяем, есть ли что-то в Обозе
                    if (i <= EquipmentIndex.Weapon3)
                        bestFromDepot = MilitaryDepotBehavior.ExtractBestWeaponForSlot(currentTierEquip[i], MilitaryDepotBehavior.TempBattleLoot);
                    else
                        bestFromDepot = MilitaryDepotBehavior.ExtractBestForSlot(i);

                    // ЛОГИКА: Если в Обозе нашли что-то (оно по коду уже лучше текущего), берем это
                    if (!bestFromDepot.IsEmpty)
                    {
                        customEquip[i] = bestFromDepot;
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{unitName}: Получено из Обоза -> {bestFromDepot.Item.Name}", new Color(0f, 1f, 0f)));
                    }
                    else
                    {
                        // ЕСЛИ НА СКЛАДЕ НЕТ: Принудительно ставим вещь 1-го тира
                        customEquip[i] = tier1Equip[i];
                    }
                }

                // Применяем новый "сборный" комплект
                agentBuildData.Equipment(customEquip);
            }
        }

        // Вспомогательный метод для поиска корня ветки юнита (1 тир)
        private static CharacterObject GetFirstTierCharacter(CharacterObject character)
        {
            if (character == null) return null;

            CharacterObject current = character;
            bool foundParent = true;

            // Цикл поиска предка (идем вверх по дереву улучшений)
            while (foundParent)
            {
                foundParent = false;
                // Проверяем всех персонажей в игре, чтобы найти того, кто улучшается в "current"
                foreach (CharacterObject candidate in CharacterObject.All)
                {
                    if (candidate.UpgradeTargets != null)
                    {
                        for (int i = 0; i < candidate.UpgradeTargets.Length; i++)
                        {
                            if (candidate.UpgradeTargets[i] == current)
                            {
                                current = candidate;
                                foundParent = true;
                                break;
                            }
                        }
                    }
                    if (foundParent) break;
                }
            }
            return current;
        }
    }

    public class MilitaryDepotBehavior : CampaignBehaviorBase
    {
        public static MobileParty DepotParty;
        public static ItemRoster TempBattleLoot;

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, new Action<MapEvent>(this.OnBattleEnded));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<MobileParty>("_militaryDepotParty", ref DepotParty);
        }

        public static void PrepareTempLoot()
        {
            if (DepotParty != null && !DepotParty.ItemRoster.IsEmpty())
                TempBattleLoot = new ItemRoster(DepotParty.ItemRoster);
            else
                TempBattleLoot = new ItemRoster();
        }

        private void OnBattleEnded(MapEvent mapEvent)
        {
            if (mapEvent != null && mapEvent.IsPlayerMapEvent)
            {
                CollectLoot();
            }
            TempBattleLoot = null;
        }

        // ВЫБОР ЛУЧШЕЙ БРОНИ
        public static EquipmentElement ExtractBestForSlot(EquipmentIndex slot)
        {
            if (TempBattleLoot == null || TempBattleLoot.IsEmpty()) return EquipmentElement.Invalid;

            int bestIndex = -1;
            float bestPower = -1f;

            for (int i = 0; i < TempBattleLoot.Count; i++)
            {
                ItemObject item = TempBattleLoot[i].EquipmentElement.Item;
                if (IsItemForArmorSlot(item, slot))
                {
                    float power = GetItemPower(item);
                    if (power > bestPower)
                    {
                        bestPower = power;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex != -1)
            {
                EquipmentElement bestEquip = TempBattleLoot[bestIndex].EquipmentElement;
                TempBattleLoot.AddToCounts(bestEquip, -1);
                return bestEquip;
            }
            return EquipmentElement.Invalid;
        }

        // ВЫБОР ЛУЧШЕГО ОРУЖИЯ
        public static EquipmentElement ExtractBestWeaponForSlot(EquipmentElement currentEquip, ItemRoster tempRoster)
        {
            if (tempRoster == null || tempRoster.IsEmpty() || currentEquip.IsEmpty)
                return EquipmentElement.Invalid;

            ItemObject currentItem = currentEquip.Item;
            int bestIndex = -1;
            float currentPower = GetItemPower(currentItem);
            float bestPower = currentPower;

            for (int i = 0; i < tempRoster.Count; i++)
            {
                ItemObject depotItem = tempRoster[i].EquipmentElement.Item;
                if (IsCompatibleWeapon(currentItem, depotItem))
                {
                    float power = GetItemPower(depotItem);
                    if (power > bestPower)
                    {
                        bestPower = power;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex != -1)
            {
                EquipmentElement bestEquip = tempRoster[bestIndex].EquipmentElement;
                tempRoster.AddToCounts(bestEquip, -1);
                return bestEquip;
            }
            return EquipmentElement.Invalid;
        }

        private static bool IsCompatibleWeapon(ItemObject original, ItemObject potential)
        {
            if (original == null || potential == null) return false;

            if (original.ItemType == ItemObject.ItemTypeEnum.Shield)
                return potential.ItemType == ItemObject.ItemTypeEnum.Shield;

            if (original.ItemType == ItemObject.ItemTypeEnum.Thrown) return potential.ItemType == ItemObject.ItemTypeEnum.Thrown;
            if (original.ItemType == ItemObject.ItemTypeEnum.Bow) return potential.ItemType == ItemObject.ItemTypeEnum.Bow;
            if (original.ItemType == ItemObject.ItemTypeEnum.Crossbow) return potential.ItemType == ItemObject.ItemTypeEnum.Crossbow;
            if (original.ItemType == ItemObject.ItemTypeEnum.Arrows || original.ItemType == ItemObject.ItemTypeEnum.Bolts)
                return potential.ItemType == original.ItemType;

            bool pot2H = potential.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon;
            bool pot1H = potential.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon;
            bool potPole = potential.ItemType == ItemObject.ItemTypeEnum.Polearm;

            if (original.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon) return pot2H || pot1H;
            if (original.ItemType == ItemObject.ItemTypeEnum.Polearm) return potPole || pot1H;
            if (original.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon) return pot1H;

            return false;
        }

        private static bool IsItemForArmorSlot(ItemObject item, EquipmentIndex slot)
        {
            if (item == null) return false;
            switch (slot)
            {
                case EquipmentIndex.Head: return item.ItemType == ItemObject.ItemTypeEnum.HeadArmor;
                case EquipmentIndex.Body: return item.ItemType == ItemObject.ItemTypeEnum.BodyArmor;
                case EquipmentIndex.Leg: return item.ItemType == ItemObject.ItemTypeEnum.LegArmor;
                case EquipmentIndex.Gloves: return item.ItemType == ItemObject.ItemTypeEnum.HandArmor;
                case EquipmentIndex.Cape: return item.ItemType == ItemObject.ItemTypeEnum.Cape;
                default: return false;
            }
        }

        private static float GetItemPower(ItemObject item)
        {
            if (item == null) return 0f;
            if (item.ItemType == ItemObject.ItemTypeEnum.HeadArmor || item.ItemType == ItemObject.ItemTypeEnum.BodyArmor ||
                item.ItemType == ItemObject.ItemTypeEnum.LegArmor || item.ItemType == ItemObject.ItemTypeEnum.HandArmor ||
                item.ItemType == ItemObject.ItemTypeEnum.Cape)
            {
                return item.ArmorComponent != null ? (item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor) : 0f;
            }
            if (item.PrimaryWeapon != null)
            {
                float dmg = Math.Max(item.PrimaryWeapon.ThrustDamage, item.PrimaryWeapon.SwingDamage);
                if (item.ItemType == ItemObject.ItemTypeEnum.Thrown) dmg *= 1.5f;
                return dmg;
            }
            return 0f;
        }

        public void CollectLoot()
        {
            if (PartyBase.MainParty == null || DepotParty == null) return;
            ItemRoster playerInv = PartyBase.MainParty.ItemRoster;
            for (int i = playerInv.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = playerInv[i];
                if (element.EquipmentElement.Item != null && IsEquipment(element.EquipmentElement.Item))
                {
                    DepotParty.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
                    playerInv.Remove(element);
                }
            }
        }

        public static void OpenDepot()
        {
            if (DepotParty == null)
            {
                DepotParty = new MobileParty();
                DepotParty.IsVisible = false;
                DepotParty.StringId = "military_depot_party";
            }
            InventoryScreenHelper.OpenScreenAsInventoryOf(PartyBase.MainParty, DepotParty.Party, CharacterObject.PlayerCharacter, null, null, null);
        }

        private bool IsEquipment(ItemObject item)
        {
            return item.ItemType == ItemObject.ItemTypeEnum.BodyArmor || item.ItemType == ItemObject.ItemTypeEnum.LegArmor ||
                   item.ItemType == ItemObject.ItemTypeEnum.HeadArmor || item.ItemType == ItemObject.ItemTypeEnum.HandArmor ||
                   item.ItemType == ItemObject.ItemTypeEnum.Cape || item.ItemType == ItemObject.ItemTypeEnum.Shield ||
                   item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon || item.ItemType == ItemObject.ItemTypeEnum.TwoHandedWeapon ||
                   item.ItemType == ItemObject.ItemTypeEnum.Polearm || item.ItemType == ItemObject.ItemTypeEnum.Thrown ||
                   item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow ||
                   item.ItemType == ItemObject.ItemTypeEnum.Arrows || item.ItemType == ItemObject.ItemTypeEnum.Bolts;
        }
    }
}

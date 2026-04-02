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

                // Берем текущее снаряжение (или дефолтное, если переопределения нет)
                Equipment currentEquip = agentBuildData.AgentOverridenSpawnEquipment;
                if (currentEquip == null)
                    currentEquip = agentBuildData.AgentCharacter.FirstBattleEquipment;

                if (currentEquip == null) return;

                Equipment customEquip = new Equipment(currentEquip); // Явное копирование
                bool isChanged = false;

                // 1. БРОНЯ
                for (EquipmentIndex i = EquipmentIndex.Head; i <= EquipmentIndex.Cape; i++)
                {
                    EquipmentElement bestArmor = MilitaryDepotBehavior.ExtractBestForSlot(i);
                    if (!bestArmor.IsEmpty)
                    {
                        customEquip[i] = bestArmor;
                        isChanged = true;
                    }
                }

                // 2. ОРУЖИЕ (Слоты 0-3)
                for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.Weapon3; i++)
                {
                    EquipmentElement currentItemElement = customEquip[i];
                    if (!currentItemElement.IsEmpty)
                    {
                        EquipmentElement bestWeapon = MilitaryDepotBehavior.ExtractBestWeaponForSlot(currentItemElement, MilitaryDepotBehavior.TempBattleLoot);
                        if (!bestWeapon.IsEmpty)
                        {
                            customEquip[i] = bestWeapon;
                            isChanged = true;
                        }
                    }
                }

                if (isChanged)
                {
                    agentBuildData.Equipment(customEquip);
                }
            }
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

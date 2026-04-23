using HarmonyLib;
using RpgMod1.StartAndRecharge;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace RpgMod1
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            // Оставляем один основной ID для Harmony
            var harmony = new Harmony("com.rpgmod1.militarydepot");
            harmony.PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new MilitaryDepotBehavior());
                starter.AddBehavior(new InitialEconomyBoost());
                starter.AddBehavior(new BattleLootSystem.BanditLootBehavior());
            }
            
            
        }

        // Изменили protected на public
        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);

            var behavior = Campaign.Current?.GetCampaignBehavior<MilitaryDepotBehavior>();
            if (behavior != null)
            {
                behavior.ClearInventory();
            }
        }


        protected override void OnApplicationTick(float dt)
        {
            if (Campaign.Current == null) return;
            if (Input.IsKeyPressed(InputKey.U)) MilitaryDepotActions.OpenDepot();
            if (Input.IsKeyPressed(InputKey.O)) MilitaryDepotActions.TransferUselessItems();
        }
    }
}
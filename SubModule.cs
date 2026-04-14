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
            }
            
            
        }
       

        protected override void OnApplicationTick(float dt)
        {
            if (Campaign.Current == null) return;
            if (Input.IsKeyPressed(InputKey.K)) MilitaryDepotActions.OpenDepot();
            if (Input.IsKeyPressed(InputKey.O)) MilitaryDepotActions.TransferUselessItems();
        }
    }
}
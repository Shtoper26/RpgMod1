using HarmonyLib;
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
            new Harmony("com.rpgmod.militarydepot").PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new MilitaryDepotBehavior());
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
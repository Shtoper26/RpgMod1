using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using RpgMod1.BattleLootSystem;
using System.Collections.Generic;

namespace RpgMod1.SimulationPatches
{
    [HarmonyPatch(typeof(DefaultCombatSimulationModel), "SimulateHit", new[] {
        typeof(CharacterObject), typeof(CharacterObject), typeof(PartyBase), typeof(PartyBase),
        typeof(float), typeof(MapEvent), typeof(float), typeof(float)
    })]
    public class AiVsAiSimulationPatch
    {
        public static void Postfix(CharacterObject strikerTroop, CharacterObject struckTroop, PartyBase strikerParty, PartyBase struckParty, float strikerAdvantage, MapEvent battle, float strikerSideMorale, float struckSideMorale, ref ExplainedNumber __result)
        {
            if (battle == null || battle.IsPlayerMapEvent) return;

            // Атакующий ИИ
            var atkIssued = BattleEquipmentTracker.GetIssuedRoster(battle, strikerParty.Id.ToString());
            if (atkIssued != null)
            {
                float totalAtk = 0f;
                foreach (var el in atkIssued)
                {
                    var item = el.EquipmentElement.Item;
                    if (item?.WeaponComponent != null)
                        totalAtk += (float)MathF.Max(item.PrimaryWeapon.ThrustDamage, item.PrimaryWeapon.SwingDamage) * el.Amount;
                }
                int troops = strikerParty.MemberRoster.TotalHealthyCount;
                if (troops > 0 && totalAtk > 0)
                    __result.AddFactor((totalAtk / troops) * 0.008f, new TextObject("{=!}ИИ Оружие"));
            }

            // Защищающийся ИИ
            var defIssued = BattleEquipmentTracker.GetIssuedRoster(battle, struckParty.Id.ToString());
            if (defIssued != null)
            {
                float totalDef = 0f;
                foreach (var el in defIssued)
                {
                    var item = el.EquipmentElement.Item;
                    if (item?.ArmorComponent != null)
                        totalDef += (float)(item.ArmorComponent.HeadArmor + item.ArmorComponent.BodyArmor + item.ArmorComponent.LegArmor + item.ArmorComponent.ArmArmor) * el.Amount;
                }
                int troops = struckParty.MemberRoster.TotalHealthyCount;
                if (troops > 0 && totalDef > 0)
                    __result.AddFactor(-((totalDef / troops) * 0.004f), new TextObject("{=!}ИИ Броня"));
            }
        }
    }
}
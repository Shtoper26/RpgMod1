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
    public class PlayerSimulationPatch
    {
        // ВАЖНО: ExplainedNumber __result теперь БЕЗ ref, так как это возвращаемое значение метода
        public static void Postfix(CharacterObject strikerTroop, CharacterObject struckTroop, PartyBase strikerParty, PartyBase struckParty, float strikerAdvantage, MapEvent battle, float strikerSideMorale, float struckSideMorale, ref ExplainedNumber __result)
        {
             if (battle == null || !battle.IsPlayerMapEvent) return;

             // АТАКА ИГРОКА
             if (strikerParty == PartyBase.MainParty)
             {
                 var issued = BattleEquipmentTracker.GetIssuedRoster(battle, strikerParty.Id.ToString());

                 if (issued != null)
                 {
                     float totalAtk = 0f;
                     foreach (var element in issued)
                     {
                         var item = element.EquipmentElement.Item;
                         if (item?.WeaponComponent != null)
                         {
                             var weapon = item.PrimaryWeapon;
                             if (weapon != null)
                             {
                                 float weaponDamage = (float)MathF.Max(weapon.ThrustDamage, weapon.SwingDamage);
                                 totalAtk += weaponDamage * element.Amount;
                             }
                         }
                     }

                     int troops = strikerParty.MemberRoster.TotalHealthyCount;
                     if (troops > 0 && totalAtk > 0)
                     {
                         float avgAtk = totalAtk / troops;

                         __result.AddFactor(avgAtk * 0.008f, new TextObject("{=!}Снабжение: Урон (Ср: " + avgAtk.ToString("F1") + ")"));
                     }
                 }
             }

             // ЗАЩИТА ИГРОКА
             if (struckParty == PartyBase.MainParty)
             {
                 var issued = BattleEquipmentTracker.GetIssuedRoster(battle, struckParty.Id.ToString());
                 if (issued != null)
                 {
                     float totalDef = 0f;
                     foreach (var element in issued)
                     {
                         var item = element.EquipmentElement.Item;
                         if (item?.ArmorComponent != null)
                         {
                             var armor = item.ArmorComponent;
                             float armorValue = (float)(armor.HeadArmor + armor.BodyArmor + armor.LegArmor + armor.ArmArmor);
                             totalDef += armorValue * element.Amount;
                         }
                     }

                     int troops = struckParty.MemberRoster.TotalHealthyCount;
                     if (troops > 0 && totalDef > 0)
                     {
                         float avgDef = totalDef / troops;

                         __result.AddFactor(-(avgDef * 0.004f), new TextObject("{=!}Снабжение: Броня (Ср: " + avgDef.ToString("F1") + ")"));
                     }
                 }
             }
            
        }
    }
}
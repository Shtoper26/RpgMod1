using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace RpgMod1
{
    public class MilitaryDepotBehavior : CampaignBehaviorBase
    {
        public static MobileParty DepotParty;
        public static ItemRoster TempBattleLoot;
        public static HashSet<ItemObject> NeededItems = new HashSet<ItemObject>();

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, (MapEvent ev) => {
                CollectLoot();
                TempBattleLoot = null;
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<MobileParty>("_militaryDepotParty", ref DepotParty);
        }

        public void CollectLoot()
        {
            if (PartyBase.MainParty == null || DepotParty == null) return;
            ItemRoster inv = PartyBase.MainParty.ItemRoster;
            for (int i = inv.Count - 1; i >= 0; i--)
            {
                ItemRosterElement el = inv[i];
                if (el.EquipmentElement.Item != null && MilitaryDepotLogic.IsEquipment(el.EquipmentElement.Item))
                {
                    DepotParty.ItemRoster.AddToCounts(el.EquipmentElement, el.Amount);
                    inv.Remove(el);
                }
            }
        }
    }
}
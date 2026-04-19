using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace RpgMod1
{

    public class MilitaryDepotComponent : TaleWorlds.CampaignSystem.Party.PartyComponents.PartyComponent
    {
        [SaveableField(1)]
        private readonly CultureObject _culture;

        // Исправление cs0534: Реализуем обязательные абстрактные свойства
        public override Hero PartyOwner => Hero.MainHero;
        public override Settlement HomeSettlement => Hero.MainHero.HomeSettlement;

        
        public CultureObject Culture => _culture;

        public override TaleWorlds.Localization.TextObject Name => new TaleWorlds.Localization.TextObject("Military Depot");

        public MilitaryDepotComponent(CultureObject culture)
        {
            _culture = culture;
        }

        // Исправление cs0534: Реализуем обязательный метод для баннера
        public override Banner GetDefaultComponentBanner()
        {
            return Clan.PlayerClan?.Banner;
        }

        public static MobileParty CreateDepotParty(string stringId, CultureObject culture)
        {
            return MobileParty.CreateParty(stringId, new MilitaryDepotComponent(culture));
        }
    }

    // Этот класс говорит игре: "Вот мой новый тип данных, его можно сохранять"
    public class MilitaryDepotSaveableTypeDefiner : SaveableTypeDefiner
    {
        // Используйте любое большое число, например 2 123 456
        public MilitaryDepotSaveableTypeDefiner() : base(2_123_456) { }

        protected override void DefineClassTypes()
        {
            // Регистрируем ваш компонент
            AddClassDefinition(typeof(MilitaryDepotComponent), 1);
        }
    }

    public class MilitaryDepotBehavior : CampaignBehaviorBase
    {
        public static MobileParty DepotParty;
        public static ItemRoster TempBattleLoot;
        public static HashSet<ItemObject> NeededItems = new HashSet<ItemObject>();

        public override void RegisterEvents()
        {
            // CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<MobileParty>("_militaryDepotParty", ref DepotParty);
        }

        public void ClearInventory()
        {
            // Обращаемся к статическому полю напрямую без 'this'
            if (DepotParty != null && DepotParty.ItemRoster != null)
            {
                DepotParty.ItemRoster.Clear();
            }
        }



        
    }
}
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1.BattleLootSystem
{
    public static class BattleEquipmentTracker
    {
        // Словарь: ID битвы -> (ID отряда -> Список выданных вещей)
        private static readonly Dictionary<string, Dictionary<string, ItemRoster>> _battleRegistry =
            new Dictionary<string, Dictionary<string, ItemRoster>>();

        /// <summary>
        /// Регистрирует выданный предмет в реестр конкретной битвы для конкретного отряда.
        /// </summary>
        public static void RegisterIssuedEquipment(MapEvent mapEvent, string partyId, ItemObject item, int count)
        {
            if (mapEvent == null || string.IsNullOrEmpty(partyId) || item == null || count <= 0) return;

            string eventId = mapEvent.Id.ToString();

            if (!_battleRegistry.ContainsKey(eventId))
                _battleRegistry[eventId] = new Dictionary<string, ItemRoster>();

            if (!_battleRegistry[eventId].ContainsKey(partyId))
                _battleRegistry[eventId][partyId] = new ItemRoster();

            _battleRegistry[eventId][partyId].AddToCounts(item, count);
        }

        /// <summary>
        /// Получает список всех выданных вещей для конкретного отряда в битве.
        /// </summary>
        public static ItemRoster GetIssuedRoster(MapEvent mapEvent, string partyId)
        {
            string eventId = mapEvent.Id.ToString();
            if (_battleRegistry.TryGetValue(eventId, out var parties) && parties.TryGetValue(partyId, out var roster))
            {
                return roster;
            }
            return null;
        }

        /// <summary>
        /// Очищает данные битвы после её завершения, чтобы не забивать оперативную память.
        /// </summary>
        public static void ClearBattleData(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            _battleRegistry.Remove(mapEvent.Id.ToString());
        }
    }
}
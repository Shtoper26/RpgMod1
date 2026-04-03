using TaleWorlds.Core;
using TaleWorlds.Library;

namespace RpgMod1
{
    public static class MilitaryDepotLogs
    {
        public static void LogWeaponIssued(string unitName, string itemName)
        {
            InformationManager.DisplayMessage(new InformationMessage($"[Склад] {unitName} вооружился: {itemName}", Color.FromUint(0xFF00FF00)));
        }

        public static void LogTransferComplete(int count)
        {
            InformationManager.DisplayMessage(new InformationMessage($"[Склад] Очистка завершена: {count} лишних предметов перенесено в ваш инвентарь.", Color.FromUint(0xFFFFFF00)));
        }
    }
}
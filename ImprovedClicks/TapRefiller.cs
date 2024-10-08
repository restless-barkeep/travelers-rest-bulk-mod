﻿using HarmonyLib;

namespace ImprovedClicks;

class TapRefiller : RestlessMods.SubModBase
{
    public new static void Awake()
    {
        BaseSetup(nameof(TapRefiller), true);

        // Add more here

        BaseFinish(typeof(TapRefiller));
    }

    [HarmonyPatch(typeof(DrinkDispenserUI), nameof(DrinkDispenserUI.CloseUI))]
    [HarmonyPrefix]
    private static bool DrinkDispenser(DrinkDispenserUI __instance)
    {
        if (__instance == null || !Plugin.ModTrigger(1)) return true;

        if (__instance.slotUI.noItemInstance) return true;

        var targetSlot = Traverse.Create(__instance.slotUI).Field("slot").GetValue<Slot>();
        
        var inventorySlotUIs = GameInventoryUI.Get(1).slotsUI;
        
        foreach (var inventorySlotUI in inventorySlotUIs)
        {
            if (inventorySlotUI == null) continue;
            var inventorySlot = Traverse.Create(inventorySlotUI).Field("slot").GetValue<Slot>();
            if (inventorySlot?.itemInstance == null || Traverse.Create(inventorySlot.itemInstance).Field("item").GetValue<Item>() == null) continue;
            
            if (targetSlot.itemInstance.Equals(inventorySlot.itemInstance))
                inventorySlotUI.OnSlotRightClick(1, inventorySlot);
        }

        return true;
    }
}
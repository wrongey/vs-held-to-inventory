using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace HeldToInventory;

public class HeldToInventoryModSystem : ModSystem
{
    private ICoreClientAPI CApi;
    private IInputAPI inputAPI;
    private bool keyPressed = false;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        if (api is ICoreClientAPI capi)
        {
            StartClientSide(capi);
        }
        else if (api is ICoreServerAPI sapi)
        {
            StartServerSide(sapi);
        }
    }

    private void StartClientSide(ICoreClientAPI capi)
    {
        CApi = capi;
        CApi.Network.RegisterChannel("autobackpackmod")
            .RegisterMessageType<InventoryUpdatePacket>();

        CApi.Input.RegisterHotKey(
            "moveToBackpack",
            "Move Held Item to Backpack",
            GlKeys.B,
            HotkeyType.CharacterControls
        );

        CApi.Input.SetHotKeyHandler("moveToBackpack", OnKeyPressed);
    }
    
    private void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Network.RegisterChannel("autobackpackmod")
            .RegisterMessageType<InventoryUpdatePacket>()
            .SetMessageHandler<InventoryUpdatePacket>(OnInventoryUpdatePacket);
    }

    private bool OnKeyPressed(KeyCombination key)
    {
        var player = CApi.World.Player;
        var activeSlot = player?.InventoryManager?.ActiveHotbarSlot;

        if (activeSlot?.Itemstack == null || activeSlot.Empty)
        {
            CApi.ShowChatMessage("No item in hand to move!");
            return true;
        }

        var backpackSlots = player.InventoryManager?.GetOwnInventory(GlobalConstants.backpackInvClassName);
        if (!backpackSlots?.Any(b => b.Empty) ?? true)
        {
            CApi.ShowChatMessage("No backpack slots available!");
            return true;
        }
        
        SendInventoryUpdatePacket();
        
        return true;
    }
    
    private void OnInventoryUpdatePacket(IServerPlayer player, InventoryUpdatePacket packet)
    {
        var activeSlot = player?.InventoryManager?.ActiveHotbarSlot;

        if (activeSlot?.Itemstack == null || activeSlot.Empty) { return; }

        var heldStack = activeSlot.Itemstack;
        var backpackSlots = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);

        if (!backpackSlots?.Any(b => b.Empty) ?? true) { return; }
        
        foreach (var backpackSlot in backpackSlots)
        {
            if (backpackSlot.Empty || backpackSlot.Itemstack == null) continue;
            
            if (backpackSlot.Itemstack.Collectible != heldStack.Collectible ||
                backpackSlot.Itemstack.StackSize >= backpackSlot.Itemstack.Collectible.MaxStackSize) continue;
            
            var transferableAmount = backpackSlot.Itemstack.Collectible.MaxStackSize - backpackSlot.Itemstack.StackSize;
            if (transferableAmount <= 0) continue;
            
            var amountToTransfer = Math.Min(heldStack.StackSize, transferableAmount);
            backpackSlot.Itemstack.StackSize += amountToTransfer;
            heldStack.StackSize -= amountToTransfer;

            backpackSlot.MarkDirty();
            activeSlot.MarkDirty();

            if (heldStack.StackSize != 0) continue;
            
            activeSlot.Itemstack = null;
            activeSlot.MarkDirty();

            return;
        }
        
        foreach (var backpackSlot in backpackSlots)
        {
            if (!backpackSlot.Empty) continue;
            
            backpackSlot.Itemstack = heldStack.Clone();
            activeSlot.Itemstack = null;

            activeSlot.MarkDirty();
            backpackSlot.MarkDirty();
            
            return;
        }
        
        
        player.InventoryManager.BroadcastHotbarSlot();
    }
    
    private void SendInventoryUpdatePacket()
    {
        CApi.Network.GetChannel("autobackpackmod").SendPacket(new InventoryUpdatePacket{});
    }
}
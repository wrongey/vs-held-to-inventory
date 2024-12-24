using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using ItemSlotOffhand = Vintagestory.API.Common.ItemSlotOffhand;

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
            .RegisterMessageType<InventoryUpdatePacket>()
            .RegisterMessageType<InventorySwapPacket>()
            .RegisterMessageType<OffhandSwapPacket>();

        CApi.Input.RegisterHotKey(
            "moveToBackpack",
            "Move Held Item to Backpack",
            GlKeys.B,
            HotkeyType.CharacterControls
        );
        
        CApi.Input.RegisterHotKey(
            "moveToOffhand",
            "Move Held Item to Offhand",
            GlKeys.V,
            HotkeyType.CharacterControls
        );

        CApi.Input.SetHotKeyHandler("moveToBackpack", OnBackpackKeyPressed);
        CApi.Input.SetHotKeyHandler("moveToOffhand", OnOffhandKeyPressed);
    }
    
    private void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Network.RegisterChannel("autobackpackmod")
            .RegisterMessageType<InventoryUpdatePacket>()
            .SetMessageHandler<InventoryUpdatePacket>(OnInventoryUpdatePacket)
            .RegisterMessageType<InventorySwapPacket>()
            .SetMessageHandler<InventorySwapPacket>(OnInventorySwapPacket)
            .RegisterMessageType<OffhandSwapPacket>()
            .SetMessageHandler<OffhandSwapPacket>(OnOffhandSwapPacket);
    }

    private bool OnOffhandKeyPressed(KeyCombination key)
    {
        var player = CApi.World.Player;
        var inventoryIsOpen = CApi.OpenedGuis.Any(g => g is GuiDialogInventory);
        var currentHoveredSlot = player?.InventoryManager?.CurrentHoveredSlot;
        
        CApi.Network.GetChannel("autobackpackmod").SendPacket(new OffhandSwapPacket
        {
            InventoryId = currentHoveredSlot?.Inventory.InventoryID,
            SlotId = currentHoveredSlot?.Inventory.GetSlotId(currentHoveredSlot) ?? 0,
            InventoryIsOpen = inventoryIsOpen
        });
        
        return true;
    }

    private bool OnBackpackKeyPressed(KeyCombination key)
    {
        var player = CApi.World.Player;
        var activeSlot = player?.InventoryManager?.ActiveHotbarSlot;
        var inventoryIsOpen = CApi.OpenedGuis.Any(g => g is GuiDialogInventory);
        
        if (inventoryIsOpen)
        {
            var currentHoveredSlot = player?.InventoryManager?.CurrentHoveredSlot;
            if (currentHoveredSlot != null)
            {
                CApi.Network.GetChannel("autobackpackmod").SendPacket(new InventorySwapPacket
                {
                    InventoryId = currentHoveredSlot.Inventory.InventoryID,
                    SlotId = currentHoveredSlot.Inventory.GetSlotId(currentHoveredSlot)
                });
                return true;
            }
        }

        if (activeSlot?.Itemstack == null || activeSlot.Empty)
        {
            CApi.ShowChatMessage("No item in hand to move!");
            return true;
        }
        
        CApi.Network.GetChannel("autobackpackmod").SendPacket(new InventoryUpdatePacket());
        
        return true;
    }

    private void OnOffhandSwapPacket(IServerPlayer player, OffhandSwapPacket packet)
    {
        var activeSlot = player?.InventoryManager?.ActiveHotbarSlot;
        var invManager = player?.InventoryManager;
        var offhandSlot = invManager?.GetHotbarInventory().FirstOrDefault(s => s is ItemSlotOffhand);
        
        if (packet.InventoryIsOpen)
        {
            invManager?.GetInventory(packet.InventoryId).TryFlipItems(packet.SlotId, offhandSlot);
        }
        else
        {
            activeSlot?.TryFlipWith(offhandSlot);
        }
    }

    private void OnInventorySwapPacket(IServerPlayer player, InventorySwapPacket packet)
    {
        var activeSlot = player?.InventoryManager?.ActiveHotbarSlot;

        player?.InventoryManager?.GetInventory(packet.InventoryId).TryFlipItems(packet.SlotId, activeSlot);
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
}
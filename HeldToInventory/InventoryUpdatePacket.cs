using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HeldToInventory;

[ProtoContract]
public class InventoryUpdatePacket
{
    [ProtoMember(1)]
    public int SourceSlotId { get; set; }
    [ProtoMember(2)]
    public int TargetSlotId { get; set; }
}

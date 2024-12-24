using System;
using ProtoBuf;

namespace HeldToInventory;

[ProtoContract]
public class InventorySwapPacket
{
    [ProtoMember(1)]
    public String InventoryId { get; set; }
    
    [ProtoMember(2)]
    public int SlotId { get; set; }
}

using System;
using ProtoBuf;

namespace HeldToInventory;

[ProtoContract]
public class OffhandSwapPacket
{
    [ProtoMember(1)]
    public String InventoryId { get; set; }
    
    [ProtoMember(2)]
    public int SlotId { get; set; }
    
    [ProtoMember(3)]
    public bool InventoryIsOpen { get; set; }
}

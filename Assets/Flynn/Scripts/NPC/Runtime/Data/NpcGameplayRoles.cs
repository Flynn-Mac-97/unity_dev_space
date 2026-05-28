using System;

[Flags]
public enum NpcGameplayRoles
{
    None        = 0,
    Merchant    = 1 << 0,
    ClueGiver   = 1 << 1,
    LoreKeeper  = 1 << 2,
    Technician  = 1 << 3,
    Storyteller = 1 << 4,
    Guardian    = 1 << 5,
    Scammer     = 1 << 6,
    Child       = 1 << 7,
    BrokenRobot = 1 << 8,
    Villager    = 1 << 9,
    Other       = 1 << 10,
}

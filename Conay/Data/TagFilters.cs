using System;

namespace Conay.Data;

public enum TagFilterState
{
    None,
    Include,
    Exclude
}

[Flags]
public enum TagMask
{
    None = 0,
    Modded = 1 << 0,
    Enhanced = 1 << 1,
    Roleplay = 1 << 2,
    MechPvP = 1 << 3,
    DicePvP = 1 << 4,
    Erotic = 1 << 5,
    BattleEye = 1 << 6,
}
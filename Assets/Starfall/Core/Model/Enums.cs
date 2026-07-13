namespace Starfall.Core.Model
{
    public enum TileState : byte
    {
        Normal = 0,
        Blocked = 1,
        Hazard = 2,
        Objective = 3,
    }

    public enum Phase : byte
    {
        Light = 0,
        Dark = 1,
    }

    public enum Owner : byte
    {
        Player = 0,
        Enemy = 1,
    }
}

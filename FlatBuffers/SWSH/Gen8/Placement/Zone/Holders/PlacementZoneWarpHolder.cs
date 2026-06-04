namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class PlacementZoneWarpHolder
{
    public override string ToString() => $"{Field00.NameAreaOther} via {PlacementZoneLabelProvider.CleanPath(Field00.NameModel)}";
}

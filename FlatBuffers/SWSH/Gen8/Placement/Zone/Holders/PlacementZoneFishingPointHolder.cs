namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class PlacementZoneFishingPointHolder
{
    public override string ToString()
    {
        var skip = Object.IterateForSlotsExceptLastN == 0 ? string.Empty : $" SkipLast{Object.IterateForSlotsExceptLastN}";
        return $"Fishing point {PlacementZoneLabelProvider.Object(Object.Identifier.HashObjectName)} {PlacementZoneSummaryUtil.At(Object.Identifier)}{skip}";
    }
}

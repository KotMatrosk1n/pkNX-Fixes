namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class PlacementZoneTrainerHolder
{
    public override string ToString()
    {
        var hashModel = Field00.Field00.HashModel;
        var model = PlacementZoneLabelProvider.Model(hashModel);
        var trainer = PlacementZoneLabelProvider.Trainer(TrainerID);
        return trainer.StartsWith("Unresolved trainer", System.StringComparison.Ordinal)
            ? $"{model}: {TrainerID:X16}"
            : $"{model}: {trainer}";
    }
}

namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class PlacementZoneParticleHolder
{
    public override string ToString() => PlacementZoneLabelProvider.CleanPath(Field00.ParticleFile);
}

public partial class PlacementZoneParticle;

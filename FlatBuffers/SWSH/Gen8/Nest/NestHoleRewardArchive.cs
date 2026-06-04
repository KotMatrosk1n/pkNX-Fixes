namespace pkNX.Structures.FlatBuffers.SWSH;

public partial class NestHoleRewardTable : INestHoleRewardTable
{
    public int EntryCount => Entries.Count;
    public IList<INestHoleReward> Rewards => Entries.Cast<INestHoleReward>().ToList();
}

public partial class NestHoleReward : INestHoleReward
{
    public uint Item => ItemID;
    public uint Star1Value { get => GetValue(0); set => SetValue(0, value); }
    public uint Star2Value { get => GetValue(1); set => SetValue(1, value); }
    public uint Star3Value { get => GetValue(2); set => SetValue(2, value); }
    public uint Star4Value { get => GetValue(3); set => SetValue(3, value); }
    public uint Star5Value { get => GetValue(4); set => SetValue(4, value); }

    public override string ToString() => $"{EntryID:0} - {ItemID:0000}";

    private uint GetValue(int index) => (uint)index < (uint)Values.Count ? Values[index] : 0;

    private void SetValue(int index, uint value)
    {
        if ((uint)index < (uint)Values.Count)
            Values[index] = value;
    }
}

public interface INestHoleReward
{
    uint Item { get; }
    IList<uint> Values { get; }
}

public interface INestHoleRewardTable
{
    ulong TableID { get; set; }
    IList<INestHoleReward> Rewards { get; }
}

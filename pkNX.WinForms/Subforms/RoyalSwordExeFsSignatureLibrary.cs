using pkNX.Containers;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace pkNX.WinForms;

internal static class RoyalSwordExeFsSignatureLibrary
{
    private const int RareCandyItemId = 50;
    private const int RoyalCandyItemId = 1128;

    public static RoyalSwordExeFsSignatureScan AnalyzeMain(byte[] layeredMain, byte[]? baseMain, RoyalCandyGameFlavor gameFlavor, int candidateId = RoyalCandyItemId)
    {
        if (!TryReadNso(layeredMain, out var layeredNso, out var layeredError))
        {
            var message = $"Layered exefs/main is not a readable NSO: {layeredError}";
            return new(false, message, [], null, true, [message], []);
        }

        var details = new List<string>();
        var matches = new List<RoyalSwordExeFsSignatureMatch>();
        var installedRoyalCandy = TryMatchRoyalCandy(layeredNso.DecompressedText, gameFlavor, candidateId, out var royalCandyMatch);
        if (royalCandyMatch is not null)
        {
            matches.Add(royalCandyMatch);
            details.Add($"Known ExeFS signature: {royalCandyMatch.Name} ({royalCandyMatch.Variant}).");
            details.AddRange(royalCandyMatch.Evidence.Select(z => "  " + z));
        }

        var segmentDiffs = CompareWithBase(layeredNso, baseMain, details);
        var hasComparableDiffs = segmentDiffs.Count != 0;
        var hasBaseChanges = segmentDiffs.Any(z => z.HasChanges);
        var hasUnknownChanges = matches.Count == 0 && (!hasComparableDiffs || hasBaseChanges);

        if (hasUnknownChanges)
        {
            details.Add(hasComparableDiffs
                ? "Unknown ExeFS edit: layered exefs/main differs from the base dump, but no registered signature matched."
                : "Unknown ExeFS edit: layered exefs/main exists, but the base main could not be compared and no registered signature matched.");
        }
        else if (matches.Count == 0 && hasComparableDiffs)
        {
            details.Add("Layered exefs/main matches the base dump; no ExeFS mod signature was detected.");
        }

        var summary = installedRoyalCandy is not null
            ? $"Known Royal Candy ExeFS signature: {installedRoyalCandy.Description}"
            : hasUnknownChanges
                ? "Unknown ExeFS mod detected."
                : "No known ExeFS mod signature detected.";

        return new(true, summary, matches, installedRoyalCandy, hasUnknownChanges, details, segmentDiffs);
    }

    private static IReadOnlyList<RoyalSwordExeFsSegmentDiff> CompareWithBase(NSO layeredNso, byte[]? baseMain, List<string> details)
    {
        if (baseMain is null)
        {
            details.Add("Base exefs/main was not found, so the layered executable could not be compared against the dump.");
            return [];
        }

        if (!TryReadNso(baseMain, out var baseNso, out var baseError))
        {
            details.Add($"Base exefs/main could not be read as NSO: {baseError}");
            return [];
        }

        var diffs = new[]
        {
            CompareSegment(".text", layeredNso.DecompressedText, baseNso.DecompressedText),
            CompareSegment(".ro", layeredNso.DecompressedRO, baseNso.DecompressedRO),
            CompareSegment(".data", layeredNso.DecompressedData, baseNso.DecompressedData),
        };

        foreach (var diff in diffs.Where(z => z.HasChanges))
        {
            details.Add($"{diff.Segment} differs from base: {diff.DifferenceCount:N0} byte(s), layered length 0x{diff.LayeredLength:X}, base length 0x{diff.BaseLength:X}.");
            details.Add($"  layered SHA-256 {diff.LayeredSha256}");
            details.Add($"  base SHA-256 {diff.BaseSha256}");
        }

        if (!diffs.Any(z => z.HasChanges))
            details.Add("Layered exefs/main segment hashes match the base dump.");

        return diffs;
    }

    private static RoyalSwordExeFsSegmentDiff CompareSegment(string segment, byte[] layered, byte[] baseline)
    {
        var min = Math.Min(layered.Length, baseline.Length);
        var differences = Math.Abs(layered.Length - baseline.Length);
        for (var i = 0; i < min; i++)
        {
            if (layered[i] != baseline[i])
                differences++;
        }

        return new(
            segment,
            differences,
            layered.Length,
            baseline.Length,
            Convert.ToHexString(NSO.Hash(layered)),
            Convert.ToHexString(NSO.Hash(baseline)));
    }

    private static bool TryReadNso(byte[] data, out NSO nso, out string error)
    {
        try
        {
            nso = new NSO(data);
            if (!nso.Header.Valid)
            {
                error = $"invalid NSO magic 0x{nso.Header.Magic:X8}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            nso = null!;
            error = ex.Message;
            return false;
        }
    }

    private static RoyalCandyInstallScan? TryMatchRoyalCandy(byte[] text, RoyalCandyGameFlavor gameFlavor, int candidateId, out RoyalSwordExeFsSignatureMatch? match)
    {
        var commonEvidence = new List<string>();
        AddEvidence(commonEvidence, HasRoyalCandyExpCandyBypass(text), "royal-candy.exp-bypass: text+0x7BC1BC and text+0x7BC1C4 compare Exp Candy index upper bound against 3.");
        AddEvidence(commonEvidence, HasRoyalCandyInfiniteUsePatch(text, candidateId), "royal-candy.non-consumption: text+0x7B1F20 branches to a cave that zeroes use quantity for item 1128.");
        AddEvidence(commonEvidence, HasRoyalCandyVirtualOwnershipPatch(text, candidateId), "royal-candy.virtual-ownership: text+0x1420EF0 dispatches item 1128 to an owned-item return.");
        AddEvidence(commonEvidence, HasRoyalCandyVirtualCountPatch(text, candidateId), "royal-candy.virtual-count: text+0x1421090 dispatches item 1128 to a positive virtual count return.");
        AddEvidence(commonEvidence, HasRoyalCandyUiRoutePatch(text, candidateId), "royal-candy.ui-route: text+0x7BC1F8 keeps Rare Candy and routes item 1128 into the same UI path.");

        var storyEvidence = new List<string>();
        AddEvidence(storyEvidence, HasRoyalCandyDynamicUseGatePatch(text, candidateId), "royal-candy.story-use-gate: text+0x7BB208 routes item 1128 through the runtime cap helper.");
        AddEvidence(storyEvidence, HasRoyalCandyDynamicQuantityMaxPatch(text, candidateId), "royal-candy.story-quantity: text+0x7BB3C4 routes item 1128 quantity through max(0, cap - level).");
        AddEvidence(storyEvidence, HasRoyalCandyInventoryClampBypass(text, candidateId), "royal-candy.story-clamp: text+0x7BAF3C bypasses the inventory-count clamp for item 1128.");

        if (commonEvidence.Count < 3 && storyEvidence.Count < 2)
        {
            match = null;
            return null;
        }

        var mode = storyEvidence.Count >= 2 ? RoyalCandyBuildMode.CustomLimits : RoyalCandyBuildMode.Unlimited;
        var details = new List<string>(commonEvidence);
        if (storyEvidence.Count != 0)
            details.AddRange(storyEvidence);

        var variant = mode == RoyalCandyBuildMode.CustomLimits ? "CustomLimits" : "Unlimited";
        var signatureId = mode == RoyalCandyBuildMode.CustomLimits
            ? "royal-candy.custom-limits.swsh-1.3.2"
            : "royal-candy.unlimited.swsh-1.3.2";
        var description = $"{variant} for {gameFlavor} (detected from registered ExeFS signature anchors)";

        match = new(signatureId, "Royal Candy", variant, details);
        return new(mode, gameFlavor, description, details);
    }

    private static void AddEvidence(ICollection<string> evidence, bool matched, string text)
    {
        if (matched)
            evidence.Add(text);
    }

    private static bool HasRoyalCandyExpCandyBypass(byte[] text)
    {
        const int firstRangeCompareOffset = 0x007BC1BC;
        const int secondRangeCompareOffset = 0x007BC1C4;
        const int expCandyIndexRegister = 9;
        return HasInstruction(text, firstRangeCompareOffset, EncodeCmpImmediate(expCandyIndexRegister, 3))
            && HasInstruction(text, secondRangeCompareOffset, EncodeCmpImmediate(expCandyIndexRegister, 3));
    }

    private static bool HasRoyalCandyInfiniteUsePatch(byte[] text, int candidateId)
    {
        const int quantityMoveOffset = 0x007B1F20;
        const int resumeOffset = quantityMoveOffset + 4;
        if (!TryDecodeBranchTarget(text, quantityMoveOffset, out var caveOffset))
            return false;

        return HasInstruction(text, caveOffset, EncodeCmpImmediate(22, candidateId))
            && HasInstruction(text, caveOffset + 4, EncodeConditionalSelect32(2, 31, 0, Arm64Condition.EQ))
            && TryDecodeBranchTarget(text, caveOffset + 8, out var decodedResume)
            && decodedResume == resumeOffset;
    }

    private static bool HasRoyalCandyVirtualOwnershipPatch(byte[] text, int candidateId)
    {
        const int itemOwnershipFunctionOffset = 0x01420EF0;
        if (!TryDecodeBranchTarget(text, itemOwnershipFunctionOffset, out var dispatchCaveOffset))
            return false;
        if (!TryDecodeConditionalBranchTarget(text, dispatchCaveOffset + 4, Arm64Condition.EQ, out var returnCaveOffset))
            return false;

        return HasInstruction(text, dispatchCaveOffset, EncodeCmpImmediate(1, candidateId))
            && HasInstruction(text, returnCaveOffset, EncodeMovzImmediate32(0, 1))
            && HasInstruction(text, returnCaveOffset + 4, EncodeRet());
    }

    private static bool HasRoyalCandyVirtualCountPatch(byte[] text, int candidateId)
    {
        const int itemCountFunctionOffset = 0x01421090;
        if (!TryDecodeBranchTarget(text, itemCountFunctionOffset, out var dispatchCaveOffset))
            return false;
        if (!TryDecodeConditionalBranchTarget(text, dispatchCaveOffset + 4, Arm64Condition.EQ, out var returnCaveOffset))
            return false;

        return HasInstruction(text, dispatchCaveOffset, EncodeCmpImmediate(1, candidateId))
            && HasMovzImmediate32(text, returnCaveOffset, 0, out var virtualCount)
            && virtualCount > 0
            && HasInstruction(text, returnCaveOffset + 4, EncodeRet());
    }

    private static bool HasRoyalCandyUiRoutePatch(byte[] text, int candidateId)
    {
        var check = new RareCandyUiCheck(0x007BC1F8, 8, 0x007BC200, 0x007BC2B4);
        var originalBranchOffset = check.CompareOffset + 4;
        if (!HasInstruction(text, check.CompareOffset, EncodeCmpImmediate(check.ItemRegister, RareCandyItemId)))
            return false;
        if (!TryDecodeConditionalBranchTarget(text, originalBranchOffset, Arm64Condition.NE, out var caveOffset))
            return false;
        if (caveOffset == check.FailOffset)
            return false;

        return HasInstruction(text, caveOffset, EncodeCmpImmediate(check.ItemRegister, candidateId))
            && TryDecodeConditionalBranchTarget(text, caveOffset + 4, Arm64Condition.EQ, out var passOffset)
            && passOffset == check.PassOffset
            && TryDecodeBranchTarget(text, caveOffset + 8, out var failOffset)
            && failOffset == check.FailOffset;
    }

    private static bool HasRoyalCandyDynamicUseGatePatch(byte[] text, int candidateId)
    {
        const int rareCandyBranchOffset = 0x007BB208;
        const int nonRareCandyOffset = 0x007BB26C;
        const int itemRegister = 20;
        const uint vanillaBranch = 0x54000321;
        if (HasInstruction(text, rareCandyBranchOffset, vanillaBranch))
            return false;
        if (!TryDecodeConditionalBranchTarget(text, rareCandyBranchOffset, Arm64Condition.NE, out var itemCheckCaveOffset))
            return false;

        return HasInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId))
            && TryDecodeConditionalBranchTarget(text, itemCheckCaveOffset + 4, Arm64Condition.NE, out var decodedNonRareCandy)
            && decodedNonRareCandy == nonRareCandyOffset
            && IsBranchInstruction(text, itemCheckCaveOffset + 8);
    }

    private static bool HasRoyalCandyDynamicQuantityMaxPatch(byte[] text, int candidateId)
    {
        const int rareCandyBranchOffset = 0x007BB3C4;
        const int nonRareCandyOffset = 0x007BB3EC;
        const int itemRegister = 19;
        const uint vanillaBranch = 0x54000141;
        if (HasInstruction(text, rareCandyBranchOffset, vanillaBranch))
            return false;
        if (!TryDecodeConditionalBranchTarget(text, rareCandyBranchOffset, Arm64Condition.NE, out var itemCheckCaveOffset))
            return false;

        return HasInstruction(text, itemCheckCaveOffset, EncodeCmpImmediate(itemRegister, candidateId))
            && TryDecodeConditionalBranchTarget(text, itemCheckCaveOffset + 4, Arm64Condition.NE, out var decodedNonRareCandy)
            && decodedNonRareCandy == nonRareCandyOffset
            && IsBranchInstruction(text, itemCheckCaveOffset + 8);
    }

    private static bool HasRoyalCandyInventoryClampBypass(byte[] text, int candidateId)
    {
        const int clampSelectOffset = 0x007BAF3C;
        const int resumeOffset = 0x007BAF40;
        const uint moveSelectedItemToX0 = 0xAA1703E0;
        if (!TryDecodeBranchTarget(text, clampSelectOffset, out var firstCaveOffset))
            return false;
        if (!TryDecodeBranchTarget(text, firstCaveOffset + 8, out var secondCaveOffset))
            return false;

        return HasInstruction(text, firstCaveOffset, moveSelectedItemToX0)
            && IsBranchLinkInstruction(text, firstCaveOffset + 4)
            && HasInstruction(text, secondCaveOffset, EncodeCmpImmediate(0, candidateId))
            && TryDecodeConditionalBranchTarget(text, secondCaveOffset + 4, Arm64Condition.EQ, out var decodedResume)
            && decodedResume == resumeOffset;
    }

    private static bool HasInstruction(byte[] text, int offset, uint expected) =>
        TryReadInstruction(text, offset, out var actual) && actual == expected;

    private static bool TryReadInstruction(byte[] text, int offset, out uint instruction)
    {
        if (offset < 0 || offset > text.Length - 4)
        {
            instruction = 0;
            return false;
        }

        instruction = BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(offset, 4));
        return true;
    }

    private static bool TryDecodeBranchTarget(byte[] text, int sourceOffset, out int targetOffset)
    {
        targetOffset = 0;
        if (!TryReadInstruction(text, sourceOffset, out var instruction) || (instruction & 0xFC000000) != 0x14000000)
            return false;

        targetOffset = sourceOffset + (SignExtend((int)(instruction & 0x03FFFFFF), 26) << 2);
        return (uint)targetOffset <= (uint)(text.Length - 4);
    }

    private static bool TryDecodeConditionalBranchTarget(byte[] text, int sourceOffset, Arm64Condition condition, out int targetOffset)
    {
        targetOffset = 0;
        if (!TryReadInstruction(text, sourceOffset, out var instruction) || (instruction & 0xFF000010) != 0x54000000)
            return false;
        if ((instruction & 0xF) != (uint)condition)
            return false;

        targetOffset = sourceOffset + (SignExtend((int)((instruction >> 5) & 0x7FFFF), 19) << 2);
        return (uint)targetOffset <= (uint)(text.Length - 4);
    }

    private static bool IsBranchInstruction(byte[] text, int offset) =>
        TryReadInstruction(text, offset, out var instruction) && (instruction & 0xFC000000) == 0x14000000;

    private static bool IsBranchLinkInstruction(byte[] text, int offset) =>
        TryReadInstruction(text, offset, out var instruction) && (instruction & 0xFC000000) == 0x94000000;

    private static bool HasMovzImmediate32(byte[] text, int offset, int register, out int immediate)
    {
        immediate = 0;
        if (!TryReadInstruction(text, offset, out var instruction))
            return false;
        if ((instruction & 0xFFE0001F) != (0x52800000u | (uint)(register & 0x1F)))
            return false;

        immediate = (int)((instruction >> 5) & 0xFFFF);
        return true;
    }

    private static int SignExtend(int value, int bits)
    {
        var shift = 32 - bits;
        return (value << shift) >> shift;
    }

    private static uint EncodeCmpImmediate(int register, int immediate) =>
        (uint)(0x7100001F | ((immediate & 0xFFF) << 10) | ((register & 0x1F) << 5));

    private static uint EncodeConditionalSelect32(int destinationRegister, int trueRegister, int falseRegister, Arm64Condition condition) =>
        (uint)(0x1A800000 | ((falseRegister & 0x1F) << 16) | ((int)condition << 12) | ((trueRegister & 0x1F) << 5) | (destinationRegister & 0x1F));

    private static uint EncodeMovzImmediate32(int register, int immediate) =>
        (uint)(0x52800000 | ((immediate & 0xFFFF) << 5) | (register & 0x1F));

    private static uint EncodeRet() => 0xD65F03C0;

    private sealed record RareCandyUiCheck(int CompareOffset, int ItemRegister, int PassOffset, int FailOffset);

    private enum Arm64Condition
    {
        EQ = 0,
        NE = 1,
    }
}

internal sealed record RoyalSwordExeFsSignatureScan(
    bool Valid,
    string Summary,
    IReadOnlyList<RoyalSwordExeFsSignatureMatch> Matches,
    RoyalCandyInstallScan? InstalledRoyalCandy,
    bool HasUnknownChanges,
    IReadOnlyList<string> Details,
    IReadOnlyList<RoyalSwordExeFsSegmentDiff> SegmentDiffs);

internal sealed record RoyalSwordExeFsSignatureMatch(
    string Id,
    string Name,
    string Variant,
    IReadOnlyList<string> Evidence);

internal sealed record RoyalSwordExeFsSegmentDiff(
    string Segment,
    int DifferenceCount,
    int LayeredLength,
    int BaseLength,
    string LayeredSha256,
    string BaseSha256)
{
    public bool HasChanges => DifferenceCount != 0 || LayeredLength != BaseLength;
}

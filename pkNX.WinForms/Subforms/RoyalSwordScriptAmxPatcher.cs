using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pkNX.WinForms;

internal static class RoyalSwordScriptAmxPatcher
{
    internal const string BagEventScriptPath = "bin/script/amx/main_event_0020.amx";
    private const ushort PawnMagic16 = 0xF1E2;
    private const ushort PawnMagic32 = 0xF1E0;
    private const ushort PawnMagic64 = 0xF1E1;
    private const short PawnFlagCompact = 0x0004;
    private const int OpProc = 46;
    private const int OpRetn = 48;
    private const int OpZeroPri = 89;
    private const int OpSysreqN = 135;
    private const int OpPushmPc = 188;

    public static IReadOnlyList<string> PatchBagEventRoyalCandyGrant(string inputPath, string outputRoot, int candidateId)
    {
        const string outputRelativePath = "romfs/bin/script/amx/main_event_0020.amx";
        const uint duplicatedNativeHash = 0x0473BE4E;
        const uint addItemNativeHash = 0x8D631FFE;
        const int freedNativeIndex = 70;
        const int duplicateNativeIndex = 76;
        const int duplicateNativeCallCell = 3686;
        const int originalNoOpGrantStubCell = 4991;
        const int grantStubCallerCell = 5020;
        const int grantStubCellCount = 8;

        if (candidateId is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(candidateId), "Royal Candy Bag-event grant item id must fit the AMX patch range.");

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Could not find Bag event AMX script.", inputPath);

        var data = File.ReadAllBytes(inputPath);
        var header = RoyalSwordAmxHeader.Read(data);
        var cellSize = GetPawnCellSize(header.Magic);
        if (cellSize != 8)
            throw new InvalidDataException($"Expected 64-bit AMX cells in {BagEventScriptPath}; found {cellSize * 8}-bit cells.");
        if ((header.Flags & PawnFlagCompact) == 0)
            throw new InvalidDataException($"{BagEventScriptPath} is not compact AMX; the Bag-event patcher expects the vanilla compact layout.");

        var nativeHashes = ReadNativeHashes(data, header);
        ExpectNative(nativeHashes, freedNativeIndex, duplicatedNativeHash, BagEventScriptPath);
        ExpectNative(nativeHashes, duplicateNativeIndex, duplicatedNativeHash, BagEventScriptPath);

        var expanded = ExpandAmxIfNeeded(data, header, cellSize);
        VerifyCompactRoundTrip(data, header, expanded, cellSize, BagEventScriptPath);

        var codeCells = ReadCells(expanded, header.Cod, header.Dat - header.Cod, cellSize);
        if (header.Publics != header.Natives)
            throw new InvalidDataException($"{BagEventScriptPath} has public entries; refusing to append the Bag grant without public-table analysis.");

        ExpectCell(codeCells, duplicateNativeCallCell, OpSysreqN, "duplicate native SYSREQ.N");
        ExpectCell(codeCells, duplicateNativeCallCell + 1, freedNativeIndex, "duplicate native index");
        ExpectCell(codeCells, duplicateNativeCallCell + 2, 8, "duplicate native parameter byte count");
        ExpectCell(codeCells, originalNoOpGrantStubCell, OpProc, "Bag-event original no-op PROC");
        ExpectCell(codeCells, originalNoOpGrantStubCell + 1, OpZeroPri, "Bag-event original no-op ZERO.pri");
        ExpectCell(codeCells, originalNoOpGrantStubCell + 2, OpRetn, "Bag-event original no-op RETN");
        ExpectLocalCall(codeCells, grantStubCallerCell, originalNoOpGrantStubCell, cellSize, "Bag-event no-op caller");

        var grantStubCell = codeCells.Length;
        ulong[] grantStub =
        [
            OpProc,
            PackAmxInstruction(OpPushmPc, 1, cellSize),
            PackAmxInstruction(OpPushmPc, candidateId, cellSize),
            OpSysreqN,
            freedNativeIndex,
            16,
            OpZeroPri,
            OpRetn,
        ];
        if (grantStub.Length != grantStubCellCount)
            throw new InvalidOperationException("Royal Candy AMX grant stub size changed unexpectedly.");

        var patchedHeader = header with
        {
            Dat = header.Dat + grantStubCellCount * cellSize,
            Hea = header.Hea + grantStubCellCount * cellSize,
            Stp = header.Stp + grantStubCellCount * cellSize,
        };
        var patchedExpanded = InsertAmxCodeCells(expanded, header, patchedHeader, grantStub, cellSize);

        codeCells = ReadCells(patchedExpanded, patchedHeader.Cod, patchedHeader.Dat - patchedHeader.Cod, cellSize);
        codeCells[duplicateNativeCallCell + 1] = duplicateNativeIndex;
        codeCells[grantStubCallerCell + 1] = unchecked((ulong)((grantStubCell - grantStubCallerCell) * cellSize));
        WriteCells(patchedExpanded, patchedHeader.Cod, codeCells, cellSize);

        var patchedPrefix = data[..header.Cod].ToArray();
        WriteAmxHeaderFields(patchedPrefix, patchedHeader);
        WriteAmxHeaderFields(patchedExpanded, patchedHeader);
        var freedNativeHashOffset = header.Natives + freedNativeIndex * header.DefSize + 8;
        BinaryPrimitives.WriteUInt32LittleEndian(patchedPrefix.AsSpan(freedNativeHashOffset), addItemNativeHash);
        BinaryPrimitives.WriteUInt32LittleEndian(patchedExpanded.AsSpan(freedNativeHashOffset), addItemNativeHash);

        var patched = BuildCompactAmx(patchedPrefix, patchedHeader, patchedExpanded, cellSize);
        BinaryPrimitives.WriteInt32LittleEndian(patchedExpanded.AsSpan(0), patched.Length);
        VerifyExpandedMemory(patched, patchedExpanded, BagEventScriptPath);

        var outputPath = Path.Combine(outputRoot, PathFromSlash(outputRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, patched);

        return
        [
            "Royal Candy Bag-event AMX patch",
            "==============================",
            "",
            "- main_event_0020.amx: reuses duplicate native import 70 as script add-item native 0x8D631FFE.",
            "- main_event_0020.amx: moves the only original import-70 call at cell 3686 to duplicate import 76, preserving its original native hash 0x0473BE4E.",
            $"- main_event_0020.amx: appends a new procedure at cell {grantStubCell}, then redirects the Bag-event no-op call at cell {grantStubCallerCell} to grant Royal Candy item id {candidateId} x1 before returning to vanilla script flow.",
            $"- main_event_0020.amx: header bounds moved from dat 0x{header.Dat:X}/hea 0x{header.Hea:X}/stp 0x{header.Stp:X} to dat 0x{patchedHeader.Dat:X}/hea 0x{patchedHeader.Hea:X}/stp 0x{patchedHeader.Stp:X}.",
        ];
    }

    private static byte[] InsertAmxCodeCells(byte[] expanded, RoyalSwordAmxHeader header, RoyalSwordAmxHeader patchedHeader, ulong[] cellsToAppend, int cellSize)
    {
        if (patchedHeader.Cod != header.Cod)
            throw new InvalidDataException("AMX code insertion cannot change COD.");

        var appendLength = cellsToAppend.Length * cellSize;
        if (patchedHeader.Dat != header.Dat + appendLength || patchedHeader.Hea != header.Hea + appendLength)
            throw new InvalidDataException("AMX patched header does not match the requested appended code length.");

        var result = new byte[patchedHeader.Hea];
        Array.Copy(expanded, 0, result, 0, header.Dat);
        WriteCells(result, header.Dat, cellsToAppend, cellSize);
        Array.Copy(expanded, header.Dat, result, patchedHeader.Dat, header.Hea - header.Dat);
        return result;
    }

    private static void WriteAmxHeaderFields(byte[] data, RoyalSwordAmxHeader header)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x00), header.Size);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x0C), header.Cod);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x10), header.Dat);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x14), header.Hea);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x18), header.Stp);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x1C), header.Cip);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x20), header.Publics);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x24), header.Natives);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x28), header.Libraries);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x2C), header.PubVars);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x30), header.Tags);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x34), header.NameTable);
    }

    private static int GetPawnCellSize(ushort magic) => magic switch
    {
        PawnMagic16 => 2,
        PawnMagic32 => 4,
        PawnMagic64 => 8,
        _ => throw new InvalidDataException($"Unknown AMX magic 0x{magic:X4}."),
    };

    private static uint[] ReadNativeHashes(byte[] data, RoyalSwordAmxHeader header)
    {
        if (header.DefSize <= 0 || header.Libraries < header.Natives)
            return [];

        var count = (header.Libraries - header.Natives) / header.DefSize;
        var hashes = new uint[count];
        for (var i = 0; i < count; i++)
        {
            var offset = header.Natives + i * header.DefSize;
            if (offset + header.DefSize > data.Length)
                break;

            hashes[i] = header.DefSize >= 12
                ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 8))
                : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        }

        return hashes;
    }

    private static byte[] ExpandAmxIfNeeded(byte[] data, RoyalSwordAmxHeader header, int cellSize)
    {
        if ((header.Flags & PawnFlagCompact) == 0)
            return data;

        if (header.Hea < header.Cod || header.Size < header.Cod || header.Size > data.Length)
            throw new InvalidDataException("AMX compact header has inconsistent code/data bounds.");

        var expanded = new byte[header.Hea];
        Array.Copy(data, expanded, Math.Min(header.Cod, data.Length));

        var src = header.Size - header.Cod;
        var dst = header.Hea - header.Cod;
        if (dst % cellSize != 0)
            throw new InvalidDataException($"Expanded AMX memory size 0x{dst:X} is not aligned to {cellSize}-byte cells.");

        while (src > 0)
        {
            ulong cell = 0;
            var shift = 0;
            var signSource = 0;
            do
            {
                src--;
                signSource = header.Cod + src;
                var current = data[signSource];
                cell |= (ulong)(current & 0x7F) << shift;
                shift += 7;
            } while (src > 0 && (data[header.Cod + src - 1] & 0x80) != 0);

            if ((data[signSource] & 0x40) != 0)
            {
                while (shift < cellSize * 8)
                {
                    cell |= 0xFFUL << shift;
                    shift += 8;
                }
            }

            dst -= cellSize;
            if (dst < 0)
                throw new InvalidDataException("AMX compact expansion produced more cells than the header allows.");

            WriteCell(expanded, header.Cod + dst, cell, cellSize);
        }

        if (dst != 0)
            throw new InvalidDataException($"AMX compact expansion stopped with 0x{dst:X} bytes unwritten.");

        return expanded;
    }

    private static void VerifyCompactRoundTrip(byte[] original, RoyalSwordAmxHeader header, byte[] expanded, int cellSize, string relativePath)
    {
        var rebuilt = BuildCompactAmx(original[..header.Cod], header, expanded, cellSize);
        VerifyExpandedMemory(rebuilt, expanded, relativePath);
    }

    private static byte[] BuildCompactAmx(byte[] prefix, RoyalSwordAmxHeader header, byte[] expanded, int cellSize)
    {
        if (prefix.Length != header.Cod)
            throw new InvalidDataException($"AMX compact prefix length 0x{prefix.Length:X} does not match COD 0x{header.Cod:X}.");
        if (expanded.Length < header.Hea)
            throw new InvalidDataException("Expanded AMX memory is shorter than HEA.");

        var compactBody = CompactAmxMemory(expanded, header, cellSize);
        var result = new byte[header.Cod + compactBody.Length];
        Array.Copy(prefix, result, prefix.Length);
        Array.Copy(compactBody, 0, result, header.Cod, compactBody.Length);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0), result.Length);
        return result;
    }

    private static byte[] CompactAmxMemory(byte[] expanded, RoyalSwordAmxHeader header, int cellSize)
    {
        var compact = new List<byte>(header.Size - header.Cod);
        for (var offset = header.Cod; offset < header.Hea; offset += cellSize)
        {
            var signed = SignedCellValue(ReadCell(expanded, offset, cellSize), cellSize);
            var chunks = new List<byte>();
            var value = signed;
            while (true)
            {
                var payload = (byte)(value & 0x7F);
                chunks.Add(payload);
                value >>= 7;
                var signBitSet = (payload & 0x40) != 0;
                if ((value == 0 && !signBitSet) || (value == -1 && signBitSet))
                    break;
            }

            for (var i = chunks.Count - 1; i >= 0; i--)
            {
                var current = chunks[i];
                if (i != 0)
                    current |= 0x80;
                compact.Add(current);
            }
        }

        return compact.ToArray();
    }

    private static void VerifyExpandedMemory(byte[] compactData, byte[] expectedExpanded, string relativePath)
    {
        var header = RoyalSwordAmxHeader.Read(compactData);
        var cellSize = GetPawnCellSize(header.Magic);
        var expanded = ExpandAmxIfNeeded(compactData, header, cellSize);
        if (!expanded.AsSpan(0, expectedExpanded.Length).SequenceEqual(expectedExpanded))
            throw new InvalidDataException($"AMX compact round trip for {relativePath} did not preserve expanded memory.");
    }

    private static ulong[] ReadCells(byte[] data, int offset, int length, int cellSize)
    {
        if (offset < 0 || length < 0 || offset + length > data.Length)
            throw new InvalidDataException($"AMX cell read is outside expanded data: offset 0x{offset:X}, length 0x{length:X}.");
        if (length % cellSize != 0)
            throw new InvalidDataException($"AMX cell span length 0x{length:X} is not aligned to {cellSize}-byte cells.");

        var cells = new ulong[length / cellSize];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = ReadCell(data, offset + i * cellSize, cellSize);
        return cells;
    }

    private static void WriteCells(byte[] data, int offset, ulong[] cells, int cellSize)
    {
        for (var i = 0; i < cells.Length; i++)
            WriteCell(data, offset + i * cellSize, cells[i], cellSize);
    }

    private static ulong ReadCell(byte[] data, int offset, int cellSize) => cellSize switch
    {
        2 => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)),
        4 => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)),
        8 => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset)),
        _ => throw new ArgumentOutOfRangeException(nameof(cellSize)),
    };

    private static void WriteCell(byte[] data, int offset, ulong value, int cellSize)
    {
        switch (cellSize)
        {
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), checked((ushort)value));
                break;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), checked((uint)value));
                break;
            case 8:
                BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cellSize));
        }
    }

    private static long SignedCellValue(ulong value, int cellSize) => cellSize switch
    {
        2 => unchecked((short)(ushort)value),
        4 => unchecked((int)(uint)value),
        8 => unchecked((long)value),
        _ => throw new ArgumentOutOfRangeException(nameof(cellSize)),
    };

    private static void ExpectNative(uint[] nativeHashes, int index, uint expectedHash, string relativePath)
    {
        if ((uint)index >= (uint)nativeHashes.Length)
            throw new InvalidDataException($"{relativePath} native index {index} is outside import table length {nativeHashes.Length}.");
        if (nativeHashes[index] != expectedHash)
            throw new InvalidDataException($"{relativePath} native index {index} is 0x{nativeHashes[index]:X8}; expected 0x{expectedHash:X8}.");
    }

    private static void ExpectCell(ulong[] cells, int index, long expected, string label)
    {
        if ((uint)index >= (uint)cells.Length)
            throw new InvalidDataException($"{label} cell {index} is outside code cell count {cells.Length}.");
        var actual = unchecked((long)cells[index]);
        if (actual != expected)
            throw new InvalidDataException($"{label} cell {index} is {actual} (0x{cells[index]:X16}); expected {expected}.");
    }

    private static void ExpectLocalCall(ulong[] cells, int callCell, int expectedTargetCell, int cellSize, string label)
    {
        ExpectCell(cells, callCell, 49, label);
        if ((uint)(callCell + 1) >= (uint)cells.Length)
            throw new InvalidDataException($"{label} call cell {callCell} has no relative operand.");

        var relativeBytes = SignedCellValue(cells[callCell + 1], cellSize);
        if (relativeBytes % cellSize != 0)
            throw new InvalidDataException($"{label} call cell {callCell} has unaligned relative target {relativeBytes}.");

        var targetCell = callCell + relativeBytes / cellSize;
        if (targetCell != expectedTargetCell)
            throw new InvalidDataException($"{label} call cell {callCell} targets {targetCell}; expected {expectedTargetCell}.");
    }

    private static ulong PackAmxInstruction(int opcode, long operand, int cellSize)
    {
        if (cellSize != 8)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Packed AMX instruction helper currently supports only 64-bit cells.");
        return ((ulong)unchecked((uint)operand) << 32) | (uint)opcode;
    }

    private static string PathFromSlash(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private sealed record RoyalSwordAmxHeader(
        int Size,
        ushort Magic,
        byte FileVersion,
        byte AmxVersion,
        short Flags,
        short DefSize,
        int Cod,
        int Dat,
        int Hea,
        int Stp,
        int Cip,
        int Publics,
        int Natives,
        int Libraries,
        int PubVars,
        int Tags,
        int NameTable)
    {
        internal static RoyalSwordAmxHeader Read(byte[] data)
        {
            if (data.Length < 0x38)
                throw new InvalidDataException("AMX file is too small for a standard header.");

            return new RoyalSwordAmxHeader(
                ReadI32(data, 0x00),
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x04)),
                data[0x06],
                data[0x07],
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(0x08)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(0x0A)),
                ReadI32(data, 0x0C),
                ReadI32(data, 0x10),
                ReadI32(data, 0x14),
                ReadI32(data, 0x18),
                ReadI32(data, 0x1C),
                ReadI32(data, 0x20),
                ReadI32(data, 0x24),
                ReadI32(data, 0x28),
                ReadI32(data, 0x2C),
                ReadI32(data, 0x30),
                ReadI32(data, 0x34));
        }

        private static int ReadI32(byte[] data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
    }
}

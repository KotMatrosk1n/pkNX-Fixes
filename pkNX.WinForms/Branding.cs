using System;
using pkNX.Structures;

namespace pkNX.WinForms;

internal static class Branding
{
    private const byte CreditMask = 0x5D;

    private static ReadOnlySpan<byte> CreditData =>
    [
        0x10, 0x32, 0x39, 0x34, 0x3B, 0x34, 0x38, 0x39, 0x7D, 0x3F, 0x24, 0x7D, 0x10, 0x3C, 0x29, 0x2F, 0x32,
        0x2E, 0x36, 0x34, 0x33,
    ];

    public static string WindowTitle(GameVersion? game = null)
    {
        var product = nameof(pkNX);
        var title = game is null ? product : $"{product} - {game}";
        return $"{title} | {GetCredit()}";
    }

    private static string GetCredit()
    {
        var data = CreditData;
        var chars = new char[data.Length];
        for (var i = 0; i < data.Length; i++)
            chars[i] = (char)(data[i] ^ CreditMask);
        return new string(chars);
    }
}

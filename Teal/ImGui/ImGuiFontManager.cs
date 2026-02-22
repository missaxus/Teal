using System.Collections.Generic;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Utilities;
using ImGuiNative = Hexa.NET.ImGui.ImGui;

namespace Teal.ImGui;

public static class ImGuiFontManager
{
    private static readonly Dictionary<string, ImFontPtr> Fonts = new();

    public static void AddFont(string key, byte[] font, float size = 12f)
    {
        if (!Fonts.ContainsKey(key))
        {
            Fonts.Add(key, LoadFontFromBytes(font, size));
        }
    }

    public static ImFontPtr GetFont(string key)
    {
        return Fonts.TryGetValue(key, out ImFontPtr value) ? value : ImFontPtr.Null;
    }

    private unsafe static ImFontPtr LoadFontFromBytes(byte[] font, float size)
    {
        ImFontPtr result = ImFontPtr.Null;
        try
        {
            ImGuiFontBuilder builder = new();
            FontBlob blob = new(font);
            uint* glyphRanges = ImGuiNative.GetIO().Fonts.GetGlyphRangesCyrillic();
            builder.AddFontFromMemoryTTF(blob.Data, blob.Length, size, glyphRanges);
            result = builder.Build();
        }
        catch
        {
        }

        return result;
    }
}



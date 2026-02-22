using HarmonyLib;
using Robust.Client.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using Robust.Shared.Maths;

namespace Teal.Systems;

public static class ScreenshotPatch
{
    private static bool _isScreenshotting = false;
    public static bool IsScreenshotting => _isScreenshotting;

    public static void Patch(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(IClyde), "ScreenshotAsync");
        var prefix = AccessTools.Method(typeof(ScreenshotPatch), nameof(Prefix));
        harmony.Patch(original, new HarmonyMethod(prefix));
    }

    public static bool Prefix(IClyde __instance, ScreenshotType type, UIBox2i? subRegion, ref Task<Image<Rgb24>> __result)
    {
        if (_isScreenshotting) return true;

        _isScreenshotting = true;
        __result = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(100);
                
                var tcs = new TaskCompletionSource<Image<Rgb24>>();
                __instance.Screenshot(type, img => tcs.SetResult(img), subRegion);
                return await tcs.Task;
            }
            finally
            {
                _isScreenshotting = false;
            }
        });

        return false;
    }
}



using System.IO;
using System.Reflection;
using System.Threading;
using Robust.Shared.ContentPack;
using Teal.ImGui;
public class EntryPoint : GameClient
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public override void PreInit()
    {
    }

    public override void Init()
    {
        var harmony = new HarmonyLib.Harmony("com.teal.client");
        Teal.Systems.ScreenshotPatch.Patch(harmony);
        Teal.Patches.FriendlyFireFilterPatch.Patch(harmony);

        ImGuiManager.Instance.Initialize();
        ImGuiManager.Instance.OnBuildFontAtlas += LoadFontAtlas;
        ImGuiManager.Instance.RegisterRender(new ImGuiOverlay());
    }

    public override void Shutdown()
    {
        _cancellationTokenSource.Cancel();
        ImGuiManager.Instance.OnBuildFontAtlas -= LoadFontAtlas;
        ImGuiManager.Instance.Dispose();
    }

    private bool LoadFontAtlas()
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        string name = "Teal.Resources.Font.ttf";
        using Stream? stream = executingAssembly.GetManifestResourceStream(name);
        if (stream == null)
        {
            return false;
        }

        byte[] font;
        using (MemoryStream memoryStream = new())
        {
            stream.CopyTo(memoryStream);
            font = memoryStream.ToArray();
        }

        ImGuiFontManager.AddFont("global", font, 24f);
        return true;
    }
}



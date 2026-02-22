using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;

namespace Teal;

public static class TealConfig
{
    private static readonly string ConfigPath = "teal_config.json";

    static TealConfig()
    {
        Load();
    }

    public static bool FullbrightEnabled { get; set; } = false;
    public static int MenuToggleKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.End;
    public static int FullbrightToggleKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.Delete;

    public static bool MeleeAimbotEnabled { get; set; } = true;
    public static bool MeleeOnlyPriority { get; set; } = false;
    public static bool MeleeTargetCritical { get; set; } = false;
    public static int MeleeAimbotLightHotkey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.MouseX1;
    public static int MeleeAimbotHeavyHotkey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.MouseX2;
    public static float MeleeAimbotFov { get; set; } = 180f;
    public static bool MeleeAimbotDrawFov { get; set; } = true;
    public static Vector4 MeleeAimbotFovColor { get; set; } = new(1f, 1f, 1f, 1f);
    public static bool GunAimbotEnabled { get; set; } = true;
    public static bool GunOnlyPriority { get; set; } = false;
    public static bool GunTargetCritical { get; set; } = false;
    public static bool GunVisibilityCheck { get; set; } = true;
    public static int GunAimbotKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.MouseX1;
    public static float GunAimbotFov { get; set; } = 180f;
    public static bool GunAimbotDrawFov { get; set; } = true;
    public static Vector4 GunAimbotFovColor { get; set; } = new(1f, 1f, 1f, 1f);
    public static bool GunAimbotPredict { get; set; } = true;
    public static int GunAimbotPriority { get; set; } = 0; 
    public static bool RotateToTarget { get; set; } = false;
    
    [JsonIgnore]
    public static Robust.Shared.GameObjects.EntityUid? TargetUid { get; set; }

    [JsonIgnore]
    public static Robust.Shared.GameObjects.EntityUid? GunTargetUid { get; set; }

    public static bool HudHealth { get; set; } = false;
    public static bool HudJob { get; set; } = false;
    public static bool HudAntag { get; set; } = false;
    public static bool HudSecurity { get; set; } = false;
    public static bool HudThirst { get; set; } = false;

    public static bool EspHealth { get; set; } = false;
    public static Vector4 EspHealthColor { get; set; } = new(0f, 1f, 0f, 1f);

    public static bool IgnoreFriends { get; set; } = true;
    public static bool ShowFriends { get; set; } = true;
    public static Vector4 EspFriendColor { get; set; } = new(1f, 0.5f, 0f, 1f);

    public static bool ShowCkey { get; set; } = false;
    public static Vector4 EspCkeyColor { get; set; } = new(1f, 1f, 0f, 1f);

    public static bool ShowName { get; set; } = false;
    public static Vector4 EspNameColor { get; set; } = new(1f, 1f, 1f, 1f);

    public static bool AntiAfkEnabled { get; set; } = false;

    public static float Zoom { get; set; } = 1f;
    public static int ZoomUpKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.UpArrow;
    public static int ZoomDownKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.DownArrow;

    public static bool AntiSlipEnabled { get; set; } = false;
    public static bool InsulatedEspEnabled { get; set; } = false;

    public static void Save()
    {
        try
        {
            var data = new ConfigData
            {
                FullbrightEnabled = FullbrightEnabled,
                MenuToggleKey = MenuToggleKey,
                FullbrightToggleKey = FullbrightToggleKey,
                MeleeAimbotEnabled = MeleeAimbotEnabled,
                MeleeOnlyPriority = MeleeOnlyPriority,
                MeleeAimbotLightHotkey = MeleeAimbotLightHotkey,
                MeleeAimbotHeavyHotkey = MeleeAimbotHeavyHotkey,
                MeleeAimbotFov = MeleeAimbotFov,
                MeleeAimbotDrawFov = MeleeAimbotDrawFov,

                GunAimbotEnabled = GunAimbotEnabled,
                GunOnlyPriority = GunOnlyPriority,
                GunTargetCritical = GunTargetCritical,
                GunVisibilityCheck = GunVisibilityCheck,
                GunAimbotKey = GunAimbotKey,
                GunAimbotFov = GunAimbotFov,
                GunAimbotDrawFov = GunAimbotDrawFov,

                GunFovColorR = GunAimbotFovColor.X,
                GunFovColorG = GunAimbotFovColor.Y,
                GunFovColorB = GunAimbotFovColor.Z,
                GunFovColorA = GunAimbotFovColor.W,
                
                FovColorR = MeleeAimbotFovColor.X,
                FovColorG = MeleeAimbotFovColor.Y,
                FovColorB = MeleeAimbotFovColor.Z,
                FovColorA = MeleeAimbotFovColor.W,
                HudHealth = HudHealth,
                HudJob = HudJob,
                HudAntag = HudAntag,
                HudSecurity = HudSecurity,
                HudThirst = HudThirst,
                EspHealth = EspHealth,

                EspColorR = EspHealthColor.X,
                EspColorG = EspHealthColor.Y,
                EspColorB = EspHealthColor.Z,
                EspColorA = EspHealthColor.W,

                IgnoreFriends = IgnoreFriends,
                ShowFriends = ShowFriends,
                FriendColorR = EspFriendColor.X,
                FriendColorG = EspFriendColor.Y,
                FriendColorB = EspFriendColor.Z,
                FriendColorA = EspFriendColor.W,

                ShowCkey = ShowCkey,
                CkeyColorR = EspCkeyColor.X,
                CkeyColorG = EspCkeyColor.Y,
                CkeyColorB = EspCkeyColor.Z,
                CkeyColorA = EspCkeyColor.W,

                ShowName = ShowName,
                NameColorR = EspNameColor.X,
                NameColorG = EspNameColor.Y,
                NameColorB = EspNameColor.Z,
                NameColorA = EspNameColor.W,

                AntiAfkEnabled = AntiAfkEnabled,
                RotateToTarget = RotateToTarget,
                Zoom = Zoom,
                ZoomUpKey = ZoomUpKey,
                ZoomDownKey = ZoomDownKey,
                AntiSlipEnabled = AntiSlipEnabled,
                InsulatedEspEnabled = InsulatedEspEnabled,
                GunAimbotPredict = GunAimbotPredict,
                GunAimbotPriority = GunAimbotPriority
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<ConfigData>(json);
            if (data == null) return;

            FullbrightEnabled = data.FullbrightEnabled;
            MenuToggleKey = data.MenuToggleKey;
            FullbrightToggleKey = data.FullbrightToggleKey;
            MeleeAimbotEnabled = data.MeleeAimbotEnabled;
            MeleeOnlyPriority = data.MeleeOnlyPriority;
            MeleeAimbotLightHotkey = data.MeleeAimbotLightHotkey;
            MeleeAimbotHeavyHotkey = data.MeleeAimbotHeavyHotkey;
            MeleeAimbotFov = data.MeleeAimbotFov;
            MeleeAimbotDrawFov = data.MeleeAimbotDrawFov;

            GunAimbotEnabled = data.GunAimbotEnabled;
            GunOnlyPriority = data.GunOnlyPriority;
            GunTargetCritical = data.GunTargetCritical;
            GunVisibilityCheck = data.GunVisibilityCheck;
            GunAimbotKey = data.GunAimbotKey;
            GunAimbotFov = data.GunAimbotFov;
            GunAimbotDrawFov = data.GunAimbotDrawFov;
            GunAimbotFovColor = new Vector4(data.GunFovColorR, data.GunFovColorG, data.GunFovColorB, data.GunFovColorA);
            
            MeleeAimbotFovColor = new Vector4(data.FovColorR, data.FovColorG, data.FovColorB, data.FovColorA);
            
            HudHealth = data.HudHealth;
            HudJob = data.HudJob;
            HudAntag = data.HudAntag;
            HudSecurity = data.HudSecurity;
            HudThirst = data.HudThirst;
            EspHealth = data.EspHealth;
            
            EspHealthColor = new Vector4(data.EspColorR, data.EspColorG, data.EspColorB, data.EspColorA);

            IgnoreFriends = data.IgnoreFriends;
            ShowFriends = data.ShowFriends;
            EspFriendColor = new Vector4(data.FriendColorR, data.FriendColorG, data.FriendColorB, data.FriendColorA);

            ShowCkey = data.ShowCkey;
            EspCkeyColor = new Vector4(data.CkeyColorR, data.CkeyColorG, data.CkeyColorB, data.CkeyColorA);

            ShowName = data.ShowName;
            EspNameColor = new Vector4(data.NameColorR, data.NameColorG, data.NameColorB, data.NameColorA);

            AntiAfkEnabled = data.AntiAfkEnabled;
            RotateToTarget = data.RotateToTarget;
            Zoom = data.Zoom;
            ZoomUpKey = data.ZoomUpKey;
            ZoomDownKey = data.ZoomDownKey;
            AntiSlipEnabled = data.AntiSlipEnabled;
            InsulatedEspEnabled = data.InsulatedEspEnabled;
            GunAimbotPredict = data.GunAimbotPredict;
            GunAimbotPriority = data.GunAimbotPriority;
        }
        catch { }
    }

    private class ConfigData
    {
        public bool FullbrightEnabled { get; set; }
        public int MenuToggleKey { get; set; }
        public int FullbrightToggleKey { get; set; }
        public bool MeleeAimbotEnabled { get; set; }
        public bool MeleeOnlyPriority { get; set; }
        public int MeleeAimbotLightHotkey { get; set; }
        public int MeleeAimbotHeavyHotkey { get; set; }
        public float MeleeAimbotFov { get; set; }
        public bool MeleeAimbotDrawFov { get; set; }

        public bool GunAimbotEnabled { get; set; }
        public bool GunOnlyPriority { get; set; }
        public bool GunTargetCritical { get; set; }
        public bool GunVisibilityCheck { get; set; }
        public int GunAimbotKey { get; set; }
        public float GunAimbotFov { get; set; }
        public bool GunAimbotDrawFov { get; set; }
        public float GunFovColorR { get; set; } = 1f;
        public float GunFovColorG { get; set; } = 1f;
        public float GunFovColorB { get; set; } = 1f;
        public float GunFovColorA { get; set; } = 0.5f;
        
        public float FovColorR { get; set; } = 1f;
        public float FovColorG { get; set; } = 1f;
        public float FovColorB { get; set; } = 1f;
        public float FovColorA { get; set; } = 0.5f;

        public bool HudHealth { get; set; }
        public bool HudJob { get; set; }
        public bool HudAntag { get; set; }
        public bool HudSecurity { get; set; }
        public bool HudThirst { get; set; }
        public bool EspHealth { get; set; }
        
        public float EspColorR { get; set; } = 0f;
        public float EspColorG { get; set; } = 1f;
        public float EspColorB { get; set; } = 0f;
        public float EspColorA { get; set; } = 1f;

        public bool IgnoreFriends { get; set; } = true;
        public bool ShowFriends { get; set; } = true;
        public float FriendColorR { get; set; } = 1f;
        public float FriendColorG { get; set; } = 0.5f;
        public float FriendColorB { get; set; } = 0f;
        public float FriendColorA { get; set; } = 1f;

        public bool IgnoreSameFaction { get; set; } = true;
        public bool ShowSameFaction { get; set; } = true;
        public float FactionColorR { get; set; } = 0f;
        public float FactionColorG { get; set; } = 0.5f;
        public float FactionColorB { get; set; } = 1f;
        public float FactionColorA { get; set; } = 1f;

        public bool ShowCkey { get; set; }
        public float CkeyColorR { get; set; } = 1f;
        public float CkeyColorG { get; set; } = 1f;
        public float CkeyColorB { get; set; } = 0f;
        public float CkeyColorA { get; set; } = 1f;

        public bool ShowName { get; set; }
        public float Zoom { get; set; } = 1f;
        public int ZoomUpKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.UpArrow;
        public int ZoomDownKey { get; set; } = (int)Hexa.NET.ImGui.ImGuiKey.DownArrow;
        public bool AntiSlipEnabled { get; set; }
        public bool InsulatedEspEnabled { get; set; }
        public bool GunAimbotPredict { get; set; } = true;
        public int GunAimbotPriority { get; set; } = 0;
        public float NameColorR { get; set; } = 1f;
        public float NameColorG { get; set; } = 1f;
        public float NameColorB { get; set; } = 1f;
        public float NameColorA { get; set; } = 1f;

        public bool AntiAfkEnabled { get; set; }
        public bool RotateToTarget { get; set; }
    }
}



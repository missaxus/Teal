using System.Numerics;
using Vector4 = System.Numerics.Vector4;
using Hexa.NET.ImGui;
using ImGuiNative = Hexa.NET.ImGui.ImGui;
using Robust.Shared.IoC;
using Robust.Shared.GameObjects;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Player;

namespace Teal.ImGui;
public sealed class ImGuiOverlay : IOverlay
{
    private enum MenuTab
    {
        Combat,
        Visuals,
        Misc,
        Settings
    }

    private enum BindingTarget
    {
        None,
        Menu,
        Fullbright,
        AimbotLight,
        AimbotHeavy,
        GunAimbot,
        ZoomUp,
        ZoomDown
    }

    public struct RenderEntityData
    {
        public EntityUid Uid;
        public Vector2 WorldPos;
        public Vector2 ScreenPos;
        public float Health;
        public float MaxHealth;
        public float Percentage;
        public bool IsCrit;
        public bool IsFriend;
        public string Name;
        public string Ckey;
    }

    public static List<RenderEntityData> RenderCache = new();
    public static readonly object CacheLock = new();

    private bool _menuOpen = true;
    private MenuTab _currentTab = MenuTab.Visuals;
    private BindingTarget _bindingTarget = BindingTarget.None;

    public void Render()
    {
        try
        {
            IPlayerManager playerManager;
            try
            {
                playerManager = IoCManager.Resolve<IPlayerManager>();
            }
            catch
            {
                return;
            }

            bool hasLocalEntity = playerManager.LocalEntity != null;

            if (_bindingTarget == BindingTarget.None && (IsKeyPressedSafe(TealConfig.MenuToggleKey) || ImGuiNative.IsKeyPressed(ImGuiKey.Insert)))
            {
                _menuOpen = !_menuOpen;
            }

            if (_bindingTarget == BindingTarget.None && IsKeyPressedSafe(TealConfig.FullbrightToggleKey))
            {
                TealConfig.FullbrightEnabled = !TealConfig.FullbrightEnabled;
            }

            if (_bindingTarget == BindingTarget.None)
            {
                if (ImGuiNative.IsKeyPressed((ImGuiKey)TealConfig.ZoomUpKey, true))
                {
                    TealConfig.Zoom += 0.5f;
                    if (TealConfig.Zoom > 30f) TealConfig.Zoom = 30f;
                    TealConfig.Save();
                }
                if (ImGuiNative.IsKeyPressed((ImGuiKey)TealConfig.ZoomDownKey, true))
                {
                    TealConfig.Zoom -= 0.5f;
                    if (TealConfig.Zoom < 0.5f) TealConfig.Zoom = 0.5f;
                    TealConfig.Save();
                }
            }

            if (hasLocalEntity)
            {
                try
                {
                    DrawHealthBars();
                }
                catch
                {
                }

                try
                {
                    DrawFovCircle();
                    DrawGunFovCircle();
                }
                catch
                {
                }
            }

            if (!_menuOpen)
            {
                return;
            }

        ApplyStyles();
        
        Vector2 displaySize = ImGuiNative.GetIO().DisplaySize;
        Vector2 windowSize = new(760f, 500f);
        ImGuiNative.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGuiNative.SetNextWindowPos(new Vector2((displaySize.X - windowSize.X) / 2f, (displaySize.Y - windowSize.Y) / 2f), ImGuiCond.FirstUseEver);
        
        ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar;
        
        if (ImGuiNative.Begin("Teal Menu", flags))
        {
            ImGuiNative.PushFont(ImGuiFontManager.GetFont("global"));

            DrawMainHeader();
            DrawSidebarTabs();

            ImGuiNative.SameLine();
            
            ImGuiNative.BeginChild(ImGuiNative.GetID("ContentArea"), new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None);
            {
                RenderTabContent();
            }
            ImGuiNative.EndChild();

            ImGuiNative.PopFont();
        }
        ImGuiNative.End();
        
        ImGuiNative.PopStyleColor(22);
        }
        catch (Exception) { }
    }

    private void DrawMainHeader()
    {
        Vector2 pos = ImGuiNative.GetCursorScreenPos();
        ImDrawListPtr drawList = ImGuiNative.GetWindowDrawList();
        
        drawList.AddRectFilled(pos, new Vector2(pos.X + ImGuiNative.GetWindowWidth(), pos.Y + 2), ImGuiNative.ColorConvertFloat4ToU32(new Vector4(1f, 0.18f, 0.18f, 1f)));
        
        ImGuiNative.SetCursorPosY(ImGuiNative.GetCursorPosY() + 5);
        ImGuiNative.SetCursorPosX(ImGuiNative.GetCursorPosX() + 6);
        ImGuiNative.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.18f, 0.18f, 1f));
        ImGuiNative.Text("Teal");
        ImGuiNative.PopStyleColor();
        ImGuiNative.SameLine();
        ImGuiNative.TextDisabled("| by haddari");
        
        ImGuiNative.Separator();
        ImGuiNative.Spacing();
    }

    private void DrawSidebarTabs()
    {
        float sidebarWidth = 140f;
        ImGuiNative.BeginChild(ImGuiNative.GetID("Sidebar"), new Vector2(sidebarWidth, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None);
        
        DrawTabButton("COMBAT", MenuTab.Combat);
        DrawTabButton("VISUALS", MenuTab.Visuals);
        DrawTabButton("MISC", MenuTab.Misc);
        DrawTabButton("SETTINGS", MenuTab.Settings);
        
        ImGuiNative.EndChild();
    }

    private static bool IsValidImGuiKey(int key)
    {
        return key >= (int)ImGuiKey.NamedKeyBegin && key < (int)ImGuiKey.NamedKeyEnd;
    }

    private static bool IsKeyPressedSafe(int key)
    {
        return IsValidImGuiKey(key) && ImGuiNative.IsKeyPressed((ImGuiKey)key);
    }

    private void DrawFovCircle()
    {
        if (!TealConfig.MeleeAimbotDrawFov) return;

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var localEnt = playerManager.LocalEntity;
        if (localEnt == null) return;

        if (!entityManager.TryGetComponent(localEnt.Value, out CombatModeComponent? combatMode) || !combatMode.IsInCombatMode)
            return;

        var meleeSystem = entityManager.System<SharedMeleeWeaponSystem>();
        if (!meleeSystem.TryGetWeapon(localEnt.Value, out _, out _))
            return;

        var mousePos = ImGuiNative.GetMousePos();
        var radius = TealConfig.MeleeAimbotFov;

        var drawList = ImGuiNative.GetBackgroundDrawList();
        var color = ImGuiNative.ColorConvertFloat4ToU32(TealConfig.MeleeAimbotFovColor);
        drawList.AddCircle(mousePos, radius, color, 120, 2.0f);

        if (TealConfig.TargetUid != null)
        {
            var targetUid = TealConfig.TargetUid.Value;
            if (!entityManager.EntityExists(targetUid)) return;

            var friendSystem = entityManager.System<Teal.Systems.FriendListSystem>();
            if (friendSystem.IsFriend(targetUid)) return;

            var transformSystem = entityManager.System<SharedTransformSystem>();
            var targetXform = entityManager.GetComponent<TransformComponent>(targetUid);
            var targetWorldPos = transformSystem.GetWorldPosition(targetXform);
            
            if (entityManager.TryGetComponent<SpriteComponent>(targetUid, out var targetSprite))
            {
                targetWorldPos += new Vector2(0, targetSprite.Bounds.Top / 2f);
            }
            
            var eyeManager = IoCManager.Resolve<IEyeManager>();
            var targetScreenPos = eyeManager.WorldToScreen(targetWorldPos);

            var localXform = entityManager.GetComponent<TransformComponent>(localEnt.Value);
            var localWorldPos = transformSystem.GetWorldPosition(localXform);
            var playerScreenPos = eyeManager.WorldToScreen(localWorldPos);

            drawList.AddLine(playerScreenPos, targetScreenPos, color, 1f);
        }
    }

    private void DrawGunFovCircle()
    {
        if (!TealConfig.GunAimbotDrawFov) return;

        var playerManager = IoCManager.Resolve<IPlayerManager>();
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var localEnt = playerManager.LocalEntity;
        if (localEnt == null) return;

        if (!entityManager.TryGetComponent(localEnt.Value, out CombatModeComponent? combatMode) || !combatMode.IsInCombatMode)
            return;

        var gunSystem = entityManager.System<SharedGunSystem>();
        if (!gunSystem.TryGetGun(localEnt.Value, out _, out _))
            return;

        var mousePos = ImGuiNative.GetMousePos();
        var radius = TealConfig.GunAimbotFov;

        var drawList = ImGuiNative.GetBackgroundDrawList();
        var color = ImGuiNative.ColorConvertFloat4ToU32(TealConfig.GunAimbotFovColor);
        drawList.AddCircle(mousePos, radius, color, 120, 2.0f);

        if (TealConfig.GunTargetUid != null)
        {
            var targetUid = TealConfig.GunTargetUid.Value;
            if (!entityManager.EntityExists(targetUid)) return;

            var friendSystem = entityManager.System<Teal.Systems.FriendListSystem>();
            if (friendSystem.IsFriend(targetUid)) return;

            var transformSystem = entityManager.System<SharedTransformSystem>();
            var targetXform = entityManager.GetComponent<TransformComponent>(targetUid);
            var targetWorldPos = transformSystem.GetWorldPosition(targetXform);
            
            if (entityManager.TryGetComponent<SpriteComponent>(targetUid, out var targetSprite))
            {
                targetWorldPos += new Vector2(0, targetSprite.Bounds.Top / 2f);
            }
            
            var eyeManager = IoCManager.Resolve<IEyeManager>();
            var targetScreenPos = eyeManager.WorldToScreen(targetWorldPos);

            var localXform = entityManager.GetComponent<TransformComponent>(localEnt.Value);
            var localWorldPos = transformSystem.GetWorldPosition(localXform);
            var playerScreenPos = eyeManager.WorldToScreen(localWorldPos);

            drawList.AddLine(playerScreenPos, targetScreenPos, color, 1f);
        }
    }

    private void DrawHealthBars()
    {
        if (!TealConfig.EspHealth && !TealConfig.ShowName && !TealConfig.ShowFriends) return;

        var drawList = ImGuiNative.GetBackgroundDrawList();

        IEntityManager entityManager;
        SharedTransformSystem transformSystem;
        IEyeManager eyeManager;
        try
        {
            entityManager = IoCManager.Resolve<IEntityManager>();
            transformSystem = entityManager.System<SharedTransformSystem>();
            eyeManager = IoCManager.Resolve<IEyeManager>();
        }
        catch { return; }

        lock (CacheLock)
        {
            foreach (var data in RenderCache)
            {
                if (!entityManager.EntityExists(data.Uid)) continue;

                var xform = entityManager.GetComponent<TransformComponent>(data.Uid);
                var worldPos = transformSystem.GetWorldPosition(xform);

                if (entityManager.TryGetComponent<SpriteComponent>(data.Uid, out var sprite))
                {
                    worldPos += new Vector2(0, sprite.Bounds.Top);
                }
                else
                {
                    worldPos += new Vector2(0, 0.4f);
                }

                var screenPos = eyeManager.WorldToScreen(worldPos);
                if (screenPos == Vector2.Zero) continue;

                Vector2 barSize = new(45f, 5f);
                Vector2 barPos = screenPos - new Vector2(barSize.X / 2f, barSize.Y);

                uint outlineColor = ImGuiNative.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
                uint bgColor = ImGuiNative.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.8f));
                
                float percentage = data.Percentage;
                Vector4 healthCol;
                if (data.IsCrit) healthCol = new Vector4(0.8f, 0f, 0f, 1f);
                else if (percentage > 0.5f) 
                    healthCol = Vector4.Lerp(new Vector4(1f, 1f, 0f, 1f), new Vector4(0f, 1f, 0f, 1f), (percentage - 0.5f) * 2f);
                else 
                    healthCol = Vector4.Lerp(new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 1f, 0f, 1f), percentage * 2f);

                uint fgColor = ImGuiNative.ColorConvertFloat4ToU32(healthCol);

                if (TealConfig.EspHealth && percentage > 0)
                {
                    drawList.AddRectFilled(barPos - new Vector2(1, 1), barPos + barSize + new Vector2(1, 1), outlineColor);
                    drawList.AddRectFilled(barPos, barPos + barSize, bgColor);
                    drawList.AddRectFilled(barPos, barPos + new Vector2(barSize.X * percentage, barSize.Y), fgColor);
                }

                float textY = barPos.Y - 20f;
                
                if (TealConfig.ShowName)
                {
                    uint nCol = ImGuiNative.ColorConvertFloat4ToU32(TealConfig.EspNameColor);
                    var nSize = ImGuiNative.CalcTextSize(data.Name);
                    drawList.AddText(new Vector2(barPos.X + barSize.X / 2f - nSize.X / 2f, textY), nCol, data.Name);
                    textY -= 20f;
                }

                if (TealConfig.ShowFriends && data.IsFriend)
                {
                    uint fCol = ImGuiNative.ColorConvertFloat4ToU32(TealConfig.EspFriendColor);
                    var fSize = ImGuiNative.CalcTextSize("Friend");
                    drawList.AddText(new Vector2(barPos.X + barSize.X / 2f - fSize.X / 2f, textY), fCol, "Friend");
                    textY -= 20f;
                }
            }
        }
    }

    private void DrawTabButton(string label, MenuTab tab)
    {
        bool active = _currentTab == tab;
        Vector2 size = new(ImGuiNative.GetContentRegionAvail().X, 35f);
        
        if (active)
        {
            ImGuiNative.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.12f, 0.12f, 1f));
            ImGuiNative.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.18f, 0.18f, 1f));
        }
        else
        {
            ImGuiNative.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.08f, 0.08f, 1f));
        }

        if (ImGuiNative.Button(label, size))
        {
            _currentTab = tab;
        }

        ImGuiNative.PopStyleColor(active ? 2 : 1);
        ImGuiNative.Spacing();
    }

    private void RenderTabContent()
    {
        switch (_currentTab)
        {
            case MenuTab.Combat:
                DrawGroup("General Filters", () => {
                    bool ignoreFriends = TealConfig.IgnoreFriends;
                    if (ImGuiNative.Checkbox("Ignore Friends", ref ignoreFriends))
                    {
                        TealConfig.IgnoreFriends = ignoreFriends;
                        TealConfig.Save();
                    }
                });

                DrawGroup("Melee Combat", () => {
                    bool enabled = TealConfig.MeleeAimbotEnabled;
                    if (ImGuiNative.Checkbox("Enable Melee Aimbot", ref enabled))
                    {
                        TealConfig.MeleeAimbotEnabled = enabled;
                        TealConfig.Save();
                    }

                    bool onlyPriority = TealConfig.MeleeOnlyPriority;
                    if (ImGuiNative.Checkbox("Only Priority Targets##melee", ref onlyPriority))
                    {
                        TealConfig.MeleeOnlyPriority = onlyPriority;
                        TealConfig.Save();
                    }

                    float fov = TealConfig.MeleeAimbotFov;
                    if (ImGuiNative.SliderFloat("Aimbot FOV", ref fov, 10f, 600f))
                    {
                        TealConfig.MeleeAimbotFov = fov;
                        TealConfig.Save();
                    }

                    bool drawFov = TealConfig.MeleeAimbotDrawFov;
                    if (ImGuiNative.Checkbox("Draw FOV Circle", ref drawFov))
                    {
                        TealConfig.MeleeAimbotDrawFov = drawFov;
                        TealConfig.Save();
                    }
                    if (drawFov)
                    {
                        Vector4 fovCol = TealConfig.MeleeAimbotFovColor;
                        if (ImGuiNative.ColorEdit4("FOV Circle Color", ref fovCol))
                        {
                            TealConfig.MeleeAimbotFovColor = fovCol;
                            TealConfig.Save();
                        }
                    }

                    float buttonWidth = 90f;
                    
                    ImGuiNative.Text("Light Attack Hotkey:");
                    ImGuiNative.SameLine();
                    int lightKey = TealConfig.MeleeAimbotLightHotkey;
                    DrawKeybindButton("##light_key", ref lightKey, BindingTarget.AimbotLight, buttonWidth);
                    if (TealConfig.MeleeAimbotLightHotkey != lightKey)
                    {
                        TealConfig.MeleeAimbotLightHotkey = lightKey;
                        TealConfig.Save();
                    }

                    ImGuiNative.Text("Heavy Attack Hotkey:");
                    ImGuiNative.SameLine();
                    int heavyKey = TealConfig.MeleeAimbotHeavyHotkey;
                    DrawKeybindButton("##heavy_key", ref heavyKey, BindingTarget.AimbotHeavy, buttonWidth);
                    if (TealConfig.MeleeAimbotHeavyHotkey != heavyKey)
                    {
                        TealConfig.MeleeAimbotHeavyHotkey = heavyKey;
                        TealConfig.Save();
                    }
                });
                
                ImGuiNative.Spacing();

                DrawGroup("General", () => {
                    bool rotate = TealConfig.RotateToTarget;
                    if (ImGuiNative.Checkbox("Rotate to Target", ref rotate))
                    {
                        TealConfig.RotateToTarget = rotate;
                        TealConfig.Save();
                    }
                });

                ImGuiNative.Spacing();

                DrawGroup("Gun Combat", () => {
                    bool enabled = TealConfig.GunAimbotEnabled;
                    if (ImGuiNative.Checkbox("Enable Gun Aimbot", ref enabled))
                    {
                        TealConfig.GunAimbotEnabled = enabled;
                        TealConfig.Save();
                    }

                    bool onlyPriority = TealConfig.GunOnlyPriority;
                    if (ImGuiNative.Checkbox("Only Priority Targets##gun", ref onlyPriority))
                    {
                        TealConfig.GunOnlyPriority = onlyPriority;
                        TealConfig.Save();
                    }

                    bool targetCrit = TealConfig.GunTargetCritical;
                    if (ImGuiNative.Checkbox("Target Critical Mobs##gun", ref targetCrit))
                    {
                        TealConfig.GunTargetCritical = targetCrit;
                        TealConfig.Save();
                    }

                    bool gunPredict = TealConfig.GunAimbotPredict;
                    if (ImGuiNative.Checkbox("Enable Target Prediction", ref gunPredict))
                    {
                        TealConfig.GunAimbotPredict = gunPredict;
                        TealConfig.Save();
                    }

                    string[] priorities = { "Mouse Distance", "Player Distance", "Lowest Health" };
                    int priority = TealConfig.GunAimbotPriority;
                    if (ImGuiNative.Combo("Target Priority", ref priority, priorities, priorities.Length))
                    {
                        TealConfig.GunAimbotPriority = priority;
                        TealConfig.Save();
                    }

                    float fov = TealConfig.GunAimbotFov;
                    if (ImGuiNative.SliderFloat("Aimbot FOV##gun", ref fov, 10f, 600f))
                    {
                        TealConfig.GunAimbotFov = fov;
                        TealConfig.Save();
                    }

                    bool drawFov = TealConfig.GunAimbotDrawFov;
                    if (ImGuiNative.Checkbox("Draw FOV Circle##gun", ref drawFov))
                    {
                        TealConfig.GunAimbotDrawFov = drawFov;
                        TealConfig.Save();
                    }
                    if (drawFov)
                    {
                        Vector4 fovCol = TealConfig.GunAimbotFovColor;
                        if (ImGuiNative.ColorEdit4("FOV Circle Color##gun", ref fovCol))
                        {
                            TealConfig.GunAimbotFovColor = fovCol;
                            TealConfig.Save();
                        }
                    }

                    float buttonWidth = 90f;
                    ImGuiNative.Text("Aimbot Hotkey:");
                    ImGuiNative.SameLine();
                    int key = TealConfig.GunAimbotKey;
                    DrawKeybindButton("##gun_key", ref key, BindingTarget.GunAimbot, buttonWidth);
                    if (TealConfig.GunAimbotKey != key)
                    {
                        TealConfig.GunAimbotKey = key;
                        TealConfig.Save();
                    }
                });
                break;

            case MenuTab.Visuals:
                DrawGroup("HUD", () => {
                    bool hHealth = TealConfig.HudHealth;
                    if (ImGuiNative.Checkbox("Health HUD", ref hHealth)) { TealConfig.HudHealth = hHealth; TealConfig.Save(); }
                    
                    bool hJob = TealConfig.HudJob;
                    if (ImGuiNative.Checkbox("Job HUD", ref hJob)) { TealConfig.HudJob = hJob; TealConfig.Save(); }
                    
                    bool hAntag = TealConfig.HudAntag;
                    if (ImGuiNative.Checkbox("Antag HUD", ref hAntag)) { TealConfig.HudAntag = hAntag; TealConfig.Save(); }
                    
                    bool hSecurity = TealConfig.HudSecurity;
                    if (ImGuiNative.Checkbox("Security HUD", ref hSecurity)) { TealConfig.HudSecurity = hSecurity; TealConfig.Save(); }
                    
                    bool espHP = TealConfig.EspHealth;
                    if (ImGuiNative.Checkbox("HP Bars", ref espHP)) { TealConfig.EspHealth = espHP; TealConfig.Save(); }
                    
                    bool hThirst = TealConfig.HudThirst;
                    if (ImGuiNative.Checkbox("Thirst HUD", ref hThirst)) { TealConfig.HudThirst = hThirst; TealConfig.Save(); }
                });

                ImGuiNative.Spacing();

                DrawGroup("Entity ESP", () => {
                    bool showName = TealConfig.ShowName;
                    if (ImGuiNative.Checkbox("Show Character Name", ref showName)) { TealConfig.ShowName = showName; TealConfig.Save(); }
                    if (showName)
                    {
                        Vector4 nColor = TealConfig.EspNameColor;
                        if (ImGuiNative.ColorEdit4("Name Color", ref nColor))
                        {
                            TealConfig.EspNameColor = nColor;
                            TealConfig.Save();
                        }
                    }

                    bool showCkey = TealConfig.ShowCkey;
                    if (ImGuiNative.Checkbox("Show Player CKey", ref showCkey)) { TealConfig.ShowCkey = showCkey; TealConfig.Save(); }
                    if (showCkey)
                    {
                        Vector4 cColor = TealConfig.EspCkeyColor;
                        if (ImGuiNative.ColorEdit4("CKey Color", ref cColor))
                        {
                            TealConfig.EspCkeyColor = cColor;
                            TealConfig.Save();
                        }
                    }

                    bool showFriends = TealConfig.ShowFriends;
                    if (ImGuiNative.Checkbox("Show Friends", ref showFriends)) { TealConfig.ShowFriends = showFriends; TealConfig.Save(); }
                    if (showFriends)
                    {
                        Vector4 fColor = TealConfig.EspFriendColor;
                        if (ImGuiNative.ColorEdit4("Friend Color", ref fColor))
                        {
                            TealConfig.EspFriendColor = fColor;
                            TealConfig.Save();
                        }
                    }
                });

                ImGuiNative.Spacing();

                DrawGroup("World Visuals", () => {
                    bool fullbright = TealConfig.FullbrightEnabled;
                    if (ImGuiNative.Checkbox("Enable Fullbright", ref fullbright))
                    {
                        TealConfig.FullbrightEnabled = fullbright;
                        TealConfig.Save();
                    }
                    float buttonWidth = 90f;
                    ImGuiNative.SameLine();
                    ImGuiNative.SetCursorPosX(ImGuiNative.GetCursorPosX() + 12f);
                    int fullbrightKey = TealConfig.FullbrightToggleKey;
                    DrawKeybindButton("##fullbright_key", ref fullbrightKey, BindingTarget.Fullbright, buttonWidth);
                    if (TealConfig.FullbrightToggleKey != fullbrightKey)
                    {
                        TealConfig.FullbrightToggleKey = fullbrightKey;
                        TealConfig.Save();
                    }

                    bool insulatedEsp = TealConfig.InsulatedEspEnabled;
                    if (ImGuiNative.Checkbox("Insulated ESP", ref insulatedEsp))
                    {
                        TealConfig.InsulatedEspEnabled = insulatedEsp;
                        TealConfig.Save();
                    }
                });

                ImGuiNative.Spacing();

                DrawGroup("Eye Control", () => {
                    float zoomVal = TealConfig.Zoom;
                    if (ImGuiNative.SliderFloat("Camera Zoom", ref zoomVal, 0.5f, 30f))
                    {
                        TealConfig.Zoom = zoomVal;
                        TealConfig.Save();
                    }
                    if (ImGuiNative.Button("Reset Zoom"))
                    {
                        TealConfig.Zoom = 1.0f;
                        TealConfig.Save();
                    }

                    ImGuiNative.Text("Zoom In Key:");
                    ImGuiNative.SameLine();
                    int upKey = TealConfig.ZoomUpKey;
                    DrawKeybindButton("##zoom_up", ref upKey, BindingTarget.ZoomUp);
                    if (upKey != TealConfig.ZoomUpKey) { TealConfig.ZoomUpKey = upKey; TealConfig.Save(); }

                    ImGuiNative.Text("Zoom Out Key:");
                    ImGuiNative.SameLine();
                    int downKey = TealConfig.ZoomDownKey;
                    DrawKeybindButton("##zoom_down", ref downKey, BindingTarget.ZoomDown);
                    if (downKey != TealConfig.ZoomDownKey) { TealConfig.ZoomDownKey = downKey; TealConfig.Save(); }
                });
                break;

            case MenuTab.Misc:
                DrawGroup("Utilities", () => {
                    bool antiAfk = TealConfig.AntiAfkEnabled;
                    if (ImGuiNative.Checkbox("Enable Anti-AFK", ref antiAfk))
                    {
                        TealConfig.AntiAfkEnabled = antiAfk;
                        TealConfig.Save();
                    }

                    bool antiSlip = TealConfig.AntiSlipEnabled;
                    if (ImGuiNative.Checkbox("Anti-Slip (No Soap)", ref antiSlip))
                    {
                        TealConfig.AntiSlipEnabled = antiSlip;
                        TealConfig.Save();
                    }
                });
                break;

            case MenuTab.Settings:
                DrawGroup("Menu Configuration", () => {
                    ImGuiNative.Text("Toggle Hotkey:");
                    ImGuiNative.SameLine();
                    int menuKey = TealConfig.MenuToggleKey;
                    DrawKeybindButton("##menu_key", ref menuKey, BindingTarget.Menu);
                    if (TealConfig.MenuToggleKey != menuKey)
                    {
                        TealConfig.MenuToggleKey = menuKey;
                        TealConfig.Save();
                    }
                });
                break;
        }
    }

    private void DrawGroup(string title, Action content)
    {
        ImGuiNative.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.18f, 0.18f, 1f));
        ImGuiNative.Text(title.ToUpper());
        ImGuiNative.PopStyleColor();
        ImGuiNative.Separator();
        ImGuiNative.Spacing();
        
        ImGuiNative.Indent(10f);
        content();
        ImGuiNative.Unindent(10f);
        
        ImGuiNative.Spacing();
        ImGuiNative.Spacing();
    }

    private void DrawKeybindButton(string id, ref int key, BindingTarget target, float width = 90f)
    {
        bool listening = _bindingTarget == target;
        string label = listening ? "WAITING" : GetKeyName(key);

        ImGuiNative.PushStyleColor(ImGuiCol.Button, new Vector4(0.05f, 0.05f, 0.06f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.08f, 0.08f, 0.09f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.12f, 0.12f, 0.13f, 1f));

        if (ImGuiNative.Button(label + id, new Vector2(width, 0f)))
        {
            _bindingTarget = target;
        }

        if (listening)
        {
            int captured = CaptureFirstPressedKey();
            if (captured == (int)ImGuiKey.Escape)
            {
                _bindingTarget = BindingTarget.None;
            }
            else if (captured != -1)
            {
                key = captured;
                _bindingTarget = BindingTarget.None;
            }
        }

        ImGuiNative.PopStyleColor(3);
    }

    private static string GetKeyName(int key)
    {
        return Enum.GetName((ImGuiKey)key) ?? key.ToString();
    }

    private static int CaptureFirstPressedKey()
    {
        for (ImGuiKey key = ImGuiKey.NamedKeyBegin; key < ImGuiKey.NamedKeyEnd; key++)
        {
            if (ImGuiNative.IsKeyPressed(key))
            {
                return (int)key;
            }
        }

        for (ImGuiKey key = ImGuiKey.MouseLeft; key <= ImGuiKey.MouseMiddle; key++)
        {
            if (ImGuiNative.IsKeyPressed(key))
            {
                return (int)key;
            }
        }

        return -1;
    }

    private static void ApplyStyles()
    {
        ImGuiStylePtr style = ImGuiNative.GetStyle();
        
        style.WindowRounding = 0f;
        style.FrameRounding = 0f;
        style.PopupRounding = 0f;
        style.ChildRounding = 0f;
        style.GrabRounding = 0f;
        style.ScrollbarRounding = 0f;
        
        style.WindowPadding = new Vector2(0f, 0f);
        style.FramePadding = new Vector2(8f, 4f);
        style.ItemSpacing = new Vector2(8f, 8f);
        style.WindowBorderSize = 1f;
        style.ChildBorderSize = 1f;

        Vector4 accent = new(1f, 0.18f, 0.18f, 1f);
        Vector4 bg = new(0.06f, 0.06f, 0.06f, 1f);
        Vector4 childBg = new(0.08f, 0.08f, 0.08f, 1f);
        Vector4 frameBg = new(0.12f, 0.12f, 0.12f, 1f);
        Vector4 text = new(0.9f, 0.9f, 0.9f, 1f);
        Vector4 border = new(0.18f, 0.18f, 0.20f, 1f);

        ImGuiNative.PushStyleColor(ImGuiCol.WindowBg, bg);
        ImGuiNative.PushStyleColor(ImGuiCol.ChildBg, childBg);
        ImGuiNative.PushStyleColor(ImGuiCol.FrameBg, frameBg);
        ImGuiNative.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.15f, 0.15f, 0.15f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.18f, 0.18f, 0.18f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.TitleBg, bg);
        ImGuiNative.PushStyleColor(ImGuiCol.TitleBgActive, bg);
        ImGuiNative.PushStyleColor(ImGuiCol.CheckMark, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.SliderGrab, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.SliderGrabActive, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.Button, frameBg);
        ImGuiNative.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.18f, 0.18f, 0.18f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.ButtonActive, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.Header, frameBg);
        ImGuiNative.PushStyleColor(ImGuiCol.HeaderHovered, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.HeaderActive, accent);
        ImGuiNative.PushStyleColor(ImGuiCol.Separator, border);
        ImGuiNative.PushStyleColor(ImGuiCol.Text, text);
        ImGuiNative.PushStyleColor(ImGuiCol.TextDisabled, new Vector4(0.5f, 0.5f, 0.5f, 1f));
        ImGuiNative.PushStyleColor(ImGuiCol.Border, border);
        ImGuiNative.PushStyleColor(ImGuiCol.BorderShadow, new Vector4(0, 0, 0, 0));
        ImGuiNative.PushStyleColor(ImGuiCol.PopupBg, bg);
    }
}



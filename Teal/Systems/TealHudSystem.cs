using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.Overlays;
using Content.Shared.Antag;

namespace Teal.Systems;

public sealed class TealHudSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;

    public override void Update(float frameTime)
    {
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;

        UpdateHud<ShowHealthIconsComponent>(localEnt.Value, TealConfig.HudHealth);
        UpdateHud<ShowJobIconsComponent>(localEnt.Value, TealConfig.HudJob);
        UpdateHud<ShowAntagIconsComponent>(localEnt.Value, TealConfig.HudAntag);
        UpdateHud<ShowCriminalRecordIconsComponent>(localEnt.Value, TealConfig.HudSecurity);
        UpdateHud<ShowThirstIconsComponent>(localEnt.Value, TealConfig.HudThirst);
    }

    private void UpdateHud<T>(EntityUid uid, bool enabled) where T : Component, new()
    {
        bool has = _entityManager.HasComponent<T>(uid);
        if (enabled && !has)
        {
            _entityManager.AddComponent<T>(uid);
        }
        else if (!enabled && has)
        {
            _entityManager.RemoveComponent<T>(uid);
        }
    }
}



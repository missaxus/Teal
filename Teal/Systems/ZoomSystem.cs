using Content.Shared.Movement.Components;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Numerics;

namespace Teal.Systems;

public sealed class ZoomSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;

    public override void Update(float frameTime)
    {
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;
        
        if (_entityManager.TryGetComponent<ContentEyeComponent>(localEnt.Value, out var contentEye) && 
            _entityManager.TryGetComponent<EyeComponent>(localEnt.Value, out var eye))
        {
            float targetZoom = TealConfig.Zoom;
            if (contentEye.TargetZoom != new Vector2(targetZoom))
            {
                contentEye.TargetZoom = new Vector2(targetZoom);
                Dirty(localEnt.Value, eye);
            }
        }
    }
}



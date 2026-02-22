using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC; 

namespace Teal.Systems;

public sealed class FullbrightSystem : EntitySystem
{
    [Dependency]
    private readonly IPlayerManager _playerManager = null!;

    [Dependency]
    private readonly IEntityManager _entityManager = null!;

    private EntityUid? _localEntity;

    public override void Update(float frameTime)
    {
        _localEntity = _playerManager.LocalEntity;
        if (_localEntity.HasValue && _entityManager.TryGetComponent(_localEntity.Value, out EyeComponent? eyeComponent) && eyeComponent != null)
        {
            eyeComponent.Eye.DrawLight = !TealConfig.FullbrightEnabled;
        }
    }
}



using System.Collections.Generic;
using Content.Shared.Electrocution;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;

namespace Teal.Systems;

public sealed class InsulatedEspSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private HashSet<EntityUid> _foundEntities = new();
    private int _timer;
    private const int SCAN_INTERVAL = 30; 

    private static readonly HashSet<string> SecretDoorPrototypes = new()
    {
        "ReinforcedRustSecretDoor",
        "ReinforcedSecretDoor",
        "SecretDoor",
        "SolidSecretDoor",
        "SolidRustSecretDoor"
    };

    public override void FrameUpdate(float frameTime)
    {
        if (!TealConfig.InsulatedEspEnabled)
        {
            if (_foundEntities.Count > 0) ResetEntityColors();
            return;
        }

        _timer++;
        if (_timer < SCAN_INTERVAL) return;
        _timer = 0;

        PerformScan();
    }

    private void PerformScan()
    {
        var viewBounds = _eyeManager.GetWorldViewport();
        
        
        foreach (var entity in _entityManager.GetEntities())
        {
            if (!_entityManager.TryGetComponent<TransformComponent>(entity, out var xform)) continue;
            
            if (!viewBounds.Contains(_transformSystem.GetWorldPosition(xform))) continue;

            bool isInsulated = _entityManager.TryGetComponent<InsulatedComponent>(entity, out var insulated);
            bool isSecretDoor = false;

            if (_entityManager.TryGetComponent<MetaDataComponent>(entity, out var meta))
            {
                var protoId = meta.EntityPrototype?.ID;
                isSecretDoor = protoId != null && SecretDoorPrototypes.Contains(protoId);
            }

            if (isInsulated || isSecretDoor)
            {
                Color color = Color.White;
                if (isInsulated && insulated != null)
                {
                    if (insulated.Coefficient <= 0f) color = Color.LimeGreen;
                    else if (insulated.Coefficient <= 0.5f) color = Color.Orange;
                    else color = Color.Red;
                }
                else if (isSecretDoor)
                {
                    color = Color.Cyan;
                }

                if (_entityManager.TryGetComponent<SpriteComponent>(entity, out var sprite))
                {
                    sprite.Color = color;
                    _foundEntities.Add(entity);
                }
            }
        }
    }

    private void ResetEntityColors()
    {
        foreach (var entity in _foundEntities)
        {
            if (_entityManager.TryGetComponent<SpriteComponent>(entity, out var sprite))
            {
                sprite.Color = Color.White;
            }
        }
        _foundEntities.Clear();
    }
}



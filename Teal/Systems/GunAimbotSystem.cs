using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Teal.ImGui;
using Hexa.NET.ImGui;
using ImGuiNative = Hexa.NET.ImGui.ImGui;

namespace Teal.Systems;

public sealed class GunAimbotSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IEyeManager _eyeManager = null!;
    [Dependency] private readonly SharedPhysicsSystem _physics = null!;
    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly EntityPrioritySystem _prioritySystem = null!;
    [Dependency] private readonly IGameTiming _gameTiming = null!;

    private static bool IsKeyDownSafe(int key)
    {
        return key >= (int)ImGuiKey.NamedKeyBegin && key < (int)ImGuiKey.NamedKeyEnd && ImGuiNative.IsKeyDown((ImGuiKey)key);
    }

    public override void Update(float frameTime)
    {
        if (!TealConfig.GunAimbotEnabled)
            return;
 
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;
 
        var gunSystem = _entityManager.System<SharedGunSystem>();
        if (!gunSystem.TryGetGun(localEnt.Value, out var gunUid, out var gun))
        {
            TealConfig.GunTargetUid = null;
            return;
        }
 
        var localXform = _entityManager.GetComponent<TransformComponent>(localEnt.Value);
        var target = GetBestTarget(localEnt.Value, localXform, gunUid);
 
        if (target == null)
        {
            TealConfig.GunTargetUid = null;
            return;
        }
 
        TealConfig.GunTargetUid = target.Value;
 
        var targetXform = _entityManager.GetComponent<TransformComponent>(target.Value);
        if (targetXform.MapID != localXform.MapID)
            return;
 
        var predictedWorldPos = PredictWorldPosition(localEnt.Value, gunUid, gun, target.Value);
 
        if (TealConfig.RotateToTarget)
        {
            var playerWorldPos = _transform.GetWorldPosition(localXform);
            var dir = predictedWorldPos - playerWorldPos;
            if (dir.LengthSquared() > 0.001f)
            {
                _transform.SetWorldRotation(localEnt.Value, Angle.FromWorldVec(dir));
            }
        }
 
        if (!IsKeyDownSafe(TealConfig.GunAimbotKey))
            return;
 
        if (gun.NextFire > _gameTiming.CurTime)
            return;

        var coordsParent = targetXform.GridUid ?? localXform.MapUid;
        if (coordsParent == null)
            return;
 
        var localPos = Vector2.Transform(predictedWorldPos, _transform.GetInvWorldMatrix(coordsParent.Value));
        var shootCoords = new EntityCoordinates(coordsParent.Value, localPos);
 
        RaiseNetworkEvent(new RequestShootEvent
        {
            Gun = _entityManager.GetNetEntity(gunUid),
            Coordinates = _entityManager.GetNetCoordinates(shootCoords),
            Target = _entityManager.GetNetEntity(target.Value)
        });
    }

    private EntityUid? GetBestTarget(EntityUid localEnt, TransformComponent localXform, EntityUid gunUid)
    {
        var targets = new List<EntityUid>();
        var mousePos = ImGuiNative.GetMousePos();
        var playerWorldPos = _transform.GetWorldPosition(localXform);
        var friendListSystem = _entityManager.System<FriendListSystem>();
 
        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (uid == localEnt) continue;
            if (mobState.CurrentState == MobState.Dead) continue;
            if (mobState.CurrentState == MobState.Critical && !TealConfig.GunTargetCritical) continue;
            if (xform.MapID != localXform.MapID) continue;
            if (friendListSystem.IsFriend(uid)) continue;
 
            var targetWorldPos = _transform.GetWorldPosition(xform);

            bool canHit = false;
            if (!TealConfig.GunAimbotPredict)
            {
                canHit = CanHitTargetWithHitScan(localEnt, gunUid, uid, targetWorldPos);
            }
            else
            {
                var predicted = PredictWorldPosition(localEnt, gunUid, _entityManager.GetComponent<GunComponent>(gunUid), uid);
                canHit = CanHitTargetWithHitScan(localEnt, gunUid, uid, predicted);
            }
            if (!canHit) continue;

            var screenPos = _eyeManager.WorldToScreen(targetWorldPos);
            float distToMouse = (screenPos - mousePos).Length();
            if (distToMouse > TealConfig.GunAimbotFov) continue;
 
            targets.Add(uid);
        }
 
        if (targets.Count == 0) return null;
 
        targets.Sort((a, b) =>
        {
            bool pA = _prioritySystem.IsPriority(a);
            bool pB = _prioritySystem.IsPriority(b);
            if (pA && !pB) return -1;
            if (!pA && pB) return 1;
 
            switch (TealConfig.GunAimbotPriority)
            {
                case 1:
                    var distPA = (_transform.GetWorldPosition(a) - playerWorldPos).LengthSquared();
                    var distPB = (_transform.GetWorldPosition(b) - playerWorldPos).LengthSquared();
                    return distPA.CompareTo(distPB);
                case 2: 
                    if (_entityManager.TryGetComponent<DamageableComponent>(a, out var dmgA) &&
                        _entityManager.TryGetComponent<DamageableComponent>(b, out var dmgB))
                    {
                        return dmgB.TotalDamage.CompareTo(dmgA.TotalDamage);
                    }
                    return 0;
                default: 
                    var distMA = (_eyeManager.WorldToScreen(_transform.GetWorldPosition(a)) - mousePos).LengthSquared();
                    var distMB = (_eyeManager.WorldToScreen(_transform.GetWorldPosition(b)) - mousePos).LengthSquared();
                    return distMA.CompareTo(distMB);
            }
        });
 
        var best = targets[0];
        if (TealConfig.GunOnlyPriority && !_prioritySystem.IsPriority(best))
            return null;
 
        return best;
    }

    private bool CanHitTargetWithHitScan(EntityUid userUid, EntityUid gunUid, EntityUid targetUid, Vector2 targetWorldPos)
    {
        var userPos = _transform.GetWorldPosition(userUid);
        var mapId = _transform.GetMapId(userUid);
 
        var dir = targetWorldPos - userPos;
        var length = dir.Length();
        if (length < 0.001f) return true;

        int mask = _entityManager.HasComponent<HitscanBatteryAmmoProviderComponent>(gunUid) ? 1 : 64;

        var ray = new CollisionRay(userPos, dir.Normalized(), mask);
        var results = _physics.IntersectRay(mapId, ray, length, userUid, false).ToList();
 
        if (results.Count == 0) return true;
        return results[0].HitEntity == targetUid;
    }

    private Vector2 PredictWorldPosition(EntityUid shooter, EntityUid gunUid, GunComponent gun, EntityUid target)
    {
        var shooterPos = _transform.GetWorldPosition(shooter);
        var targetPos = _transform.GetWorldPosition(target);

        if (!TealConfig.GunAimbotPredict)
            return targetPos;

        var speed = gun.ProjectileSpeedModified;
 
        if (_entityManager.HasComponent<HitscanBatteryAmmoProviderComponent>(gunUid) || speed > 100f)
            return targetPos;

        if (speed <= 0f) return targetPos;

        var targetVel = Vector2.Zero;
        if (_entityManager.TryGetComponent<PhysicsComponent>(target, out var targetPhysics))
        {
            var targetXform = _entityManager.GetComponent<TransformComponent>(target);
            targetVel = _physics.GetMapLinearVelocity(target, targetPhysics, targetXform);
        }

        if (targetVel.LengthSquared() < 0.001f)
            return targetPos;

        float t = 0f;
        Vector2 predictedPos = targetPos;
        for (int i = 0; i < 5; i++)
        {
            float dist = (predictedPos - shooterPos).Length();
            float newT = dist / speed;
            if (i > 0 && Math.Abs(newT - t) < 0.001f) break;
            t = newT;
            predictedPos = targetPos + targetVel * t;
        }

        return predictedPos;
    }
}



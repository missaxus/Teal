using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Teal.ImGui;
using Hexa.NET.ImGui;
using ImGuiNative = Hexa.NET.ImGui.ImGui;

namespace Teal.Systems;

public sealed class MeleeAimbotSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IEntityManager _entityManager = null!;
    [Dependency] private readonly IGameTiming _timing = null!;
    [Dependency] private readonly IEyeManager _eyeManager = null!;
    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly EntityPrioritySystem _prioritySystem = null!;

    private static System.Reflection.PropertyInfo? _damageProp;
    private static System.Reflection.MethodInfo? _getTotalMethod;
    private static System.Reflection.MethodInfo? _floatMethod;
    private static Type? _damageableType;
    private static Type? _damageSpecifierType;
    private static Type? _fixedPointType;

    private static float GetEntityHealth(object damageableComp)
    {
        try
        {
            if (_damageableType == null) _damageableType = damageableComp.GetType();
            if (_damageProp == null) _damageProp = _damageableType.GetProperty("Damage");
            if (_damageProp == null) return 0f;

            var dmgSpec = _damageProp.GetValue(damageableComp);
            if (dmgSpec == null) return 0f;

            if (_damageSpecifierType == null) _damageSpecifierType = dmgSpec.GetType();
            if (_getTotalMethod == null) _getTotalMethod = _damageSpecifierType.GetMethod("GetTotal");
            if (_getTotalMethod == null) return 0f;

            var total = _getTotalMethod.Invoke(dmgSpec, null);
            if (total == null) return 0f;

            if (_fixedPointType == null) _fixedPointType = total.GetType();
            if (_floatMethod == null) _floatMethod = _fixedPointType.GetMethod("Float", Type.EmptyTypes);

            if (_floatMethod != null) return (float)_floatMethod.Invoke(total, null)!;
            return Convert.ToSingle(total);
        }
        catch { return 0f; }
    }

    private static bool IsKeyDownSafe(int key)
    {
        return key >= (int)ImGuiKey.NamedKeyBegin && key < (int)ImGuiKey.NamedKeyEnd && ImGuiNative.IsKeyDown((ImGuiKey)key);
    }

    public override void Update(float frameTime)
    {
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;

        var xformLocal = _entityManager.GetComponent<TransformComponent>(localEnt.Value);
        var localPos = _transform.GetWorldPosition(xformLocal);
        var friendListSystem = _entityManager.System<FriendListSystem>();

        lock (ImGuiOverlay.CacheLock)
        {
            ImGuiOverlay.RenderCache.Clear();
            var espQuery = _entityManager.EntityQueryEnumerator<DamageableComponent, MobStateComponent>();
            while (espQuery.MoveNext(out var eUid, out var damage, out var state))
            {
                if (eUid == localEnt) continue;
                if (state.CurrentState == MobState.Dead) continue;
                
                if (!_entityManager.TryGetComponent<TransformComponent>(eUid, out var eXform)) continue;
                if (!_entityManager.TryGetComponent<MetaDataComponent>(eUid, out var meta)) continue;
                if (!_entityManager.TryGetComponent<MobThresholdsComponent>(eUid, out var thresholds)) continue;

                var worldPos = _transform.GetWorldPosition(eXform);
                if ((worldPos - localPos).Length() > 50f) continue;

                bool isCrit = state.CurrentState == MobState.Critical;
                float expectedHealth = GetEntityHealth(damage);
                float percentage = Math.Clamp(1f - (expectedHealth / 100f), 0f, 1f);

                ImGuiOverlay.RenderCache.Add(new ImGuiOverlay.RenderEntityData
                {
                    Uid = eUid,
                    Health = expectedHealth,
                    MaxHealth = 100f,
                    Percentage = percentage,
                    IsCrit = isCrit,
                    IsFriend = friendListSystem.IsFriend(eUid),
                    Name = meta.EntityName,
                    Ckey = ""
                });
            }
        }
        TealConfig.TargetUid = null;
        if (!TealConfig.MeleeAimbotEnabled)
            return;

        if (!_entityManager.TryGetComponent(localEnt.Value, out CombatModeComponent? combatMode) || !combatMode.IsInCombatMode)
            return;

        var meleeSystem = _entityManager.System<SharedMeleeWeaponSystem>();
        bool hasWeapon = meleeSystem.TryGetWeapon(localEnt.Value, out var weaponUid, out var weapon);
        float range = hasWeapon ? weapon!.Range : 2.5f;

        var mousePos = ImGuiNative.GetMousePos();
        var target = GetBestTarget(localEnt.Value, range, mousePos);
        
        TealConfig.TargetUid = target;

        if (target != null && TealConfig.RotateToTarget)
        {
            var targetXform = _entityManager.GetComponent<TransformComponent>(target.Value);
            var dir = _transform.GetWorldPosition(targetXform) - localPos;
            if (dir.LengthSquared() > 0.001f)
            {
                _transform.SetWorldRotation(localEnt.Value, Angle.FromWorldVec(dir));
            }
        }
        
        bool isLight = IsKeyDownSafe(TealConfig.MeleeAimbotLightHotkey);
        bool isHeavy = IsKeyDownSafe(TealConfig.MeleeAimbotHeavyHotkey);

        if ((!isLight && !isHeavy) || target == null)
            return;

        if (hasWeapon && weapon!.NextAttack > _timing.CurTime)
            return;

        if (!hasWeapon && _entityManager.TryGetComponent<MeleeWeaponComponent>(localEnt.Value, out var unarmed) && unarmed.NextAttack > _timing.CurTime)
            return;

        var targetUid = target.Value;
        var xform = _entityManager.GetComponent<TransformComponent>(targetUid);
        var targetNet = _entityManager.GetNetEntity(targetUid);
        var coordsNet = _entityManager.GetNetCoordinates(xform.Coordinates);

        if (isHeavy)
        {
            if (!hasWeapon || weaponUid == localEnt.Value)
            {
                var ev = new DisarmAttackEvent(targetNet, coordsNet);
                RaisePredictiveEvent(ev);
            }
            else
            {
                var entities = new List<NetEntity> { targetNet };
                var ev = new HeavyAttackEvent(_entityManager.GetNetEntity(weaponUid), entities, coordsNet);
                RaisePredictiveEvent(ev);
            }
        }
        else
        {
            var weaponNet = hasWeapon ? _entityManager.GetNetEntity(weaponUid) : _entityManager.GetNetEntity(localEnt.Value);
            var ev = new LightAttackEvent(targetNet, weaponNet, coordsNet);
            RaisePredictiveEvent(ev);
        }
    }

    private EntityUid? GetBestTarget(EntityUid localEnt, float worldRange, Vector2 mousePos)
    {
        var targets = new List<EntityUid>();
        var playerWorldPos = _transform.GetWorldPosition(localEnt);
        var friendListSystem = _entityManager.System<FriendListSystem>();

        var query = _entityManager.EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (uid == localEnt) continue;

            if (mobState.CurrentState == MobState.Dead || mobState.CurrentState == MobState.Critical) continue;

            if (friendListSystem.IsFriend(uid))
                continue;

            var targetPos = _transform.GetWorldPosition(xform);
            if ((targetPos - playerWorldPos).Length() > worldRange)
                continue;

            var screenPos = _eyeManager.WorldToScreen(targetPos);
            if ((screenPos - mousePos).Length() > TealConfig.MeleeAimbotFov)
                continue;

            targets.Add(uid);
        }

        if (targets.Count == 0) return null;

        targets.Sort((a, b) =>
        {
            bool pA = _prioritySystem.IsPriority(a);
            bool pB = _prioritySystem.IsPriority(b);
            if (pA && !pB) return -1;
            if (!pA && pB) return 1;

            if (TealConfig.MeleeOnlyPriority && !pA && !pB) return 0;

            if (_entityManager.TryGetComponent<DamageableComponent>(a, out var dmgA) && 
                _entityManager.TryGetComponent<DamageableComponent>(b, out var dmgB))
            {
                var hA = GetEntityHealth(dmgA);
                var hB = GetEntityHealth(dmgB);
                if (Math.Abs(hA - hB) > 0.01f) return hB.CompareTo(hA);
            }

            var distA = (_eyeManager.WorldToScreen(_transform.GetWorldPosition(a)) - mousePos).Length();
            var distB = (_eyeManager.WorldToScreen(_transform.GetWorldPosition(b)) - mousePos).Length();
            return distA.CompareTo(distB);
        });

        var best = targets[0];
        if (TealConfig.MeleeOnlyPriority && !_prioritySystem.IsPriority(best))
            return null;

        return best;
    }
}



using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Weapons.Melee;
using HarmonyLib;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Teal.Patches;

public static class FriendlyFireFilterPatch
{
    private static Teal.Systems.FriendListSystem? _friendListSystem;
    private static SharedPhysicsSystem? _physicsSystem;
    private static IEntityManager? _entityManager;

    public static void Patch(Harmony harmony)
    {
        var original = AccessTools.Method(typeof(SharedMeleeWeaponSystem), "ArcRayCast");
        var prefix = AccessTools.Method(typeof(FriendlyFireFilterPatch), nameof(Prefix));
        if (original != null && prefix != null)
        {
            harmony.Patch(original, new HarmonyMethod(prefix));
        }
    }

    public static bool Prefix(SharedMeleeWeaponSystem __instance, Vector2 position, Angle angle, Angle arcWidth, float range, MapId mapId, EntityUid ignore, ref HashSet<EntityUid> __result)
    {
        if (!TealConfig.IgnoreFriends)
        {
            return true;
        }

        if (_entityManager == null)
            _entityManager = IoCManager.Resolve<IEntityManager>();

        if (_friendListSystem == null)
            _friendListSystem = _entityManager.System<Teal.Systems.FriendListSystem>();

        if (_physicsSystem == null)
            _physicsSystem = _entityManager.System<SharedPhysicsSystem>();

        Angle angle2 = arcWidth;
        int val = 1 + 35 * (int)Math.Ceiling(angle2.Theta / (Math.PI * 2.0));
        val = Math.Max(1, val);
        double num = (double)angle2 / (double)val;
        Angle angle3 = angle - (double)angle2 / 2.0;

        HashSet<EntityUid> hashSet = new HashSet<EntityUid>();

        for (int i = 0; i < val; i++)
        {
            Angle angle4 = angle3 + num * (double)i;
            List<RayCastResults> list = _physicsSystem.IntersectRay(mapId, new CollisionRay(position, angle4.ToWorldVec(), 31), range, ignore, returnOnFirstHit: false).ToList();
            
            if (list.Count != 0)
            {
                EntityUid hitEntity = list[0].HitEntity;
                bool isFriend = _friendListSystem.IsFriend(hitEntity);

                if (!isFriend)
                {
                    hashSet.Add(hitEntity);
                }
            }
        }
        
        __result = hashSet;
        return false;
    }
}



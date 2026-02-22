using System.Collections.Generic;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Teal.Systems;

public sealed class EntityPrioritySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;

    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _priorities = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(AddPriorityVerb);
    }

    public bool IsPriority(EntityUid entity)
    {
        ICommonSession? localSession = _playerManager.LocalSession;
        if (localSession == null)
            return false;

        return _priorities.TryGetValue(localSession, out var p) && p.Contains(entity);
    }

    private void AddPriorityVerb(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (ev.User == ev.Target || !HasComp<MobStateComponent>(ev.Target))
            return;

        if (!_playerManager.LocalEntity.HasValue || ev.User != _playerManager.LocalEntity.Value)
            return;

        ICommonSession? localSession = _playerManager.LocalSession;
        if (localSession == null)
            return;

        if (!_priorities.TryGetValue(localSession, out var priorities))
        {
            priorities = new HashSet<EntityUid>();
            _priorities[localSession] = priorities;
        }

        bool isPriority = priorities.Contains(ev.Target);
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                if (!priorities.Add(ev.Target))
                {
                    priorities.Remove(ev.Target);
                }
            },
            Text = isPriority ? "Remove Priority" : "Make Priority",
            Priority = 200
        };
        ev.Verbs.Add(verb);
    }
}



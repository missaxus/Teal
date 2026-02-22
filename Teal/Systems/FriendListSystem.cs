using System.Collections.Generic;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Teal.Systems;

public sealed class FriendListSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;

    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _friendsBySession = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(AddFriendVerb);
    }

    public bool IsFriend(EntityUid entity)
    {
        var localSession = _playerManager.LocalSession;
        if (localSession == null)
            return false;

        if (!_friendsBySession.TryGetValue(localSession, out var friends))
            return false;

        return friends.Contains(entity);
    }

    private void AddFriendVerb(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (ev.User == ev.Target || !HasComp<MobStateComponent>(ev.Target) || !_playerManager.LocalEntity.HasValue || ev.User != _playerManager.LocalEntity.Value)
            return;

        var localSession = _playerManager.LocalSession;
        if (localSession == null)
            return;

        if (!_friendsBySession.TryGetValue(localSession, out var friends))
        {
            friends = new HashSet<EntityUid>();
            _friendsBySession[localSession] = friends;
        }

        string text = friends.Contains(ev.Target) ? "Remove Friend" : "Add Friend";
        var item = new AlternativeVerb
        {
            Act = () =>
            {
                if (!friends.Add(ev.Target))
                {
                    friends.Remove(ev.Target);
                }
            },
            Text = text,
            Priority = 200
        };
        ev.Verbs.Add(item);
    }
}



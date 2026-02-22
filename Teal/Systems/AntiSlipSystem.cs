using System.Linq;
using Content.Shared.Movement.Components;
using Content.Shared.Slippery;
using Content.Shared.StepTrigger.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Teal.Systems;

public sealed class AntiSlipSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private bool _isWalking;
    private bool _wasWalking;

    public override void FrameUpdate(float frameTime)
    {
        if (!TealConfig.AntiSlipEnabled) return;

        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;

        bool shouldWalk = ShouldPressWalk(localEnt.Value);
        _isWalking = shouldWalk;

        if (_isWalking != _wasWalking)
        {
            PressWalk(_isWalking ? BoundKeyState.Down : BoundKeyState.Up);
            _wasWalking = _isWalking;
        }
    }

    private void PressWalk(BoundKeyState state)
    {
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null) return;

        var moverCoordinates = _transformSystem.GetMoverCoordinates(localEnt.Value);
        var screenCoordinates = _eyeManager.CoordinatesToScreen(moverCoordinates);
        var inputFunctionId = _inputManager.NetworkBindMap.KeyFunctionID(EngineKeyFunctions.Walk);

        var message = new ClientFullInputCmdMessage(_gameTiming.CurTick, _gameTiming.TickFraction, inputFunctionId)
        {
            State = state,
            Coordinates = moverCoordinates,
            ScreenCoordinates = screenCoordinates,
            Uid = EntityUid.Invalid
        };

        var inputSys = _entityManager.System<InputSystem>();
        inputSys.HandleInputCommand(_playerManager.LocalSession, EngineKeyFunctions.Walk, message);
    }

    private bool ShouldPressWalk(EntityUid player)
    {
        bool hasShoes = _containerSystem.TryGetContainer(player, "shoes", out var container) && container.ContainedEntities.Count > 0;

        foreach (var entity in _entityLookup.GetEntitiesInRange(player, 1f, LookupFlags.Uncontained))
        {
            if (!TryComp<StepTriggerComponent>(entity, out var step) || !step.Active) continue;

            var (walkSpeed, sprintSpeed) = GetPlayerSpeed(player);
            
            
            if (sprintSpeed < step.RequiredTriggeredSpeed || walkSpeed > step.RequiredTriggeredSpeed || !HasComp<SlipperyComponent>(entity))
                continue;

            if (TryComp<MetaDataComponent>(entity, out var meta))
            {
                var proto = meta.EntityPrototype;
                if (proto != null && proto.ID.Contains("ShardGlass"))
                {
                    if (!hasShoes) return true;
                    continue;
                }
            }

            return true;
        }
        return false;
    }

    private (float, float) GetPlayerSpeed(EntityUid player)
    {
        if (TryComp<MovementSpeedModifierComponent>(player, out var comp))
        {
            return (comp.CurrentWalkSpeed, comp.CurrentSprintSpeed);
        }
        return (2.5f, 4.5f);
    }
}



using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using System;

namespace Teal.Systems;

public sealed class AntiAfkSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly IInputManager _inputManager = null!;
    [Dependency] private readonly IGameTiming _timing = null!;

    private readonly Random _random = new();
    private TimeSpan _nextAfkAction;
    private TimeSpan _releaseAt;
    private KeyFunctionId? _heldMoveFunctionId;

    private void SendInput(KeyFunctionId functionId, BoundKeyState state)
    {
        var msg = new FullInputCmdMessage(_timing.CurTick, _timing.TickFraction, 0, functionId, state, NetCoordinates.Invalid, _inputManager.MouseScreenPosition);
        RaisePredictiveEvent(msg);
    }

    public override void Update(float frameTime)
    {
        if (!TealConfig.AntiAfkEnabled)
            return;

        if (_playerManager.LocalEntity == null)
            return;

        if (_heldMoveFunctionId.HasValue)
        {
            if (_timing.CurTime >= _releaseAt)
            {
                SendInput(_heldMoveFunctionId.Value, BoundKeyState.Up);
                _heldMoveFunctionId = null;
            }
            return;
        }

        if (_timing.CurTime < _nextAfkAction)
            return;

        _nextAfkAction = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(6, 12));

        var moveKeys = new[]
        {
            EngineKeyFunctions.MoveUp,
            EngineKeyFunctions.MoveDown,
            EngineKeyFunctions.MoveLeft,
            EngineKeyFunctions.MoveRight
        };
        var moveKey = moveKeys[_random.Next(moveKeys.Length)];
        var inputFunctionId = _inputManager.NetworkBindMap.KeyFunctionID(moveKey);

        _heldMoveFunctionId = inputFunctionId;
        _releaseAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextDouble() * 0.35 + 0.15);

        SendInput(inputFunctionId, BoundKeyState.Down);
    }
}



// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Maps;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityDashSwitchHooks : IDisposable
    {
        private static readonly FieldInfo StartY = typeof(OriginalDashSwitchState)
            .GetField("startY", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityDashSwitchHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                // Fixed original Load order: Update IL first, constructor detour second.
                hooks.Add(new ILHook(typeof(OriginalDashSwitchState).GetMethod(nameof(OriginalDashSwitchState.Update)),
                    AdjustUpdate));
                hooks.Add(new Hook(typeof(OriginalDashSwitchState).GetConstructor([
                    typeof(Vector2), typeof(OriginalDashSwitchSide), typeof(bool), typeof(bool), typeof(MapEntityId),
                    typeof(string), typeof(ISourceEngineContext), typeof(ISourceSolidWorld)
                ]), (ConstructorHook)Construct));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(OriginalDashSwitchState dashSwitch)
            => dashSwitch.Scene is ISourceEngineContextScene scene &&
               ReferenceEquals(scene.EngineContext, runtime.Engine) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private void AdjustUpdate(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdfld<OriginalDashSwitchState>("side"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<OriginalDashSwitchSide, OriginalDashSwitchState, OriginalDashSwitchSide>>((side,
                dashSwitch) => Inverted(dashSwitch) && side == OriginalDashSwitchSide.Up
                ? OriginalDashSwitchSide.Down
                : side);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, OriginalDashSwitchState, Vector2>>((direction, dashSwitch)
                => Inverted(dashSwitch) ? new Vector2(direction.X, -direction.Y) : direction);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(2f));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalDashSwitchState, float>>((distance, dashSwitch)
                => Inverted(dashSwitch) ? -distance : distance);
        }

        private void Construct(ConstructorOriginal original, OriginalDashSwitchState dashSwitch, Vector2 position,
            OriginalDashSwitchSide side, bool persistent, bool allGates, MapEntityId id, string spriteName,
            ISourceEngineContext engine, ISourceSolidWorld world)
        {
            original(dashSwitch, position, side, persistent, allGates, id, spriteName, engine, world);
            if (ReferenceEquals(engine, runtime.Engine) && side == OriginalDashSwitchSide.Up)
                StartY.SetValue(dashSwitch, dashSwitch.Y);
        }

        public void Dispose()
        {
            // Fixed original Unload order: Update IL first, constructor detour second.
            for (var index = 0; index < hooks.Count; index++) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void ConstructorOriginal(OriginalDashSwitchState dashSwitch, Vector2 position,
            OriginalDashSwitchSide side, bool persistent, bool allGates, MapEntityId id, string spriteName,
            ISourceEngineContext engine, ISourceSolidWorld world);

        private delegate void ConstructorHook(ConstructorOriginal original, OriginalDashSwitchState dashSwitch,
            Vector2 position, OriginalDashSwitchSide side, bool persistent, bool allGates, MapEntityId id,
            string spriteName, ISourceEngineContext engine, ISourceSolidWorld world);
    }
}

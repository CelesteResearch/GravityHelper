// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerInputHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<bool> useAbsolute;
        private readonly Func<bool> useAbsoluteFeather;
        private readonly Action<RoomPlayerSession> processVvvvvv;
        private readonly Func<bool> suppressGrab;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerInputHooks(GravityRuntimeContext runtime, Func<bool> useAbsolute,
            Func<bool> useAbsoluteFeather, Action<RoomPlayerSession> processVvvvvv, Func<bool> suppressGrab)
        {
            this.runtime = runtime;
            this.useAbsolute = useAbsolute;
            this.useAbsoluteFeather = useAbsoluteFeather;
            this.processVvvvvv = processVvvvvv;
            this.suppressGrab = suppressGrab;
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Update)),
                (PlayerUpdateHook)UpdatePlayer));
            hooks.Add(new ILHook(
                typeof(OriginalGameInputProcess).GetMethod(nameof(OriginalGameInputProcess.GetAimVector)),
                AdjustAimWindow));
            hooks.Add(new Hook(
                typeof(OriginalGameInputProcess).GetProperty(nameof(OriginalGameInputProcess.GrabCheck)).GetMethod,
                (GrabCheckHook)GetGrabCheck));
        }

        private bool Owns(RoomPlayerSession player)
            => player.Scene is ISourceEngineContextScene scene && ReferenceEquals(scene.EngineContext, runtime.Engine);

        private bool OwnsInput(OriginalGameInputProcess input)
            => runtime.PlayerComponent?.Entity is RoomPlayerSession player && ReferenceEquals(player.Input, input);

        private void UpdatePlayer(PlayerUpdate original, RoomPlayerSession player)
        {
            if (!Owns(player))
            {
                original(player);
                return;
            }

            var input = player.Input;
            Vector2 feather = input.Feather.Value;
            Vector2 aim = input.Aim.Value;
            int moveY = input.MoveY.Value;
            int gliderMoveY = input.GliderMoveY.Value;
            var level = player.Scene as OriginalLevelSceneRoot;
            if (!level.InCutscene && !level.Paused) processVvvvvv(player);
            bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
            bool absolute = useAbsolute();
            bool absoluteFeather = useAbsoluteFeather();
            if (inverted)
            {
                if (absolute)
                {
                    input.Aim.Value = new Vector2(aim.X, -aim.Y);
                    input.MoveY.Value = -moveY;
                    input.GliderMoveY.Value = -gliderMoveY;
                }

                if (absoluteFeather) input.Feather.Value = new Vector2(feather.X, -feather.Y);
            }

            original(player);
            // Restore the state observed before original Update, even if that update changed gravity or settings.
            if (inverted)
            {
                if (absoluteFeather) input.Feather.Value = feather;
                if (absolute)
                {
                    input.GliderMoveY.Value = gliderMoveY;
                    input.MoveY.Value = moveY;
                    input.Aim.Value = aim;
                }
            }
        }

        private void AdjustAimWindow(ILContext il)
        {
            var cursor = new ILCursor(il);
            int angleLocal = -1;
            cursor.GotoNext(MoveType.After, i => i.MatchCall(typeof(Math), nameof(Math.Atan2)),
                i => i.MatchConvR4(), i => i.MatchStloc(out angleLocal));
            cursor.GotoNext(MoveType.After, i => i.MatchLdloc(angleLocal));
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalGameInputProcess, float>>((angle, input) =>
                OwnsInput(input) && useAbsolute() && (runtime.PlayerComponent?.ShouldInvert ?? false) ? -angle : angle);
        }

        private bool GetGrabCheck(GrabCheck original, OriginalGameInputProcess input)
            => OwnsInput(input) && suppressGrab() ? false : original(input);

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void PlayerUpdate(RoomPlayerSession player);

        private delegate void PlayerUpdateHook(PlayerUpdate original, RoomPlayerSession player);

        private delegate bool GrabCheck(OriginalGameInputProcess input);

        private delegate bool GrabCheckHook(GrabCheck original, OriginalGameInputProcess input);
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Holdables.TheoCrystal;
using Celeste.Scene.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityTheoCrystalHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityTheoCrystalHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new ILHook(typeof(TheoCrystalState).GetMethod(nameof(TheoCrystalState.Update)), Update));
                hooks.Add(new ILHook(
                    typeof(TheoCrystalState).GetMethod("OnCollideV",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), CollideV));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(TheoCrystalState self) => ReferenceEquals(self.Hold.EngineContext, runtime.Engine) &&
                                                        GravityEntityInversion.ShouldInvert(self);

        private void CollideV(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<Vector2>(nameof(Vector2.Y))))
                throw new InvalidOperationException("Theo original first Speed.Y was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, TheoCrystalState, float>>((value, self) => Inverted(self) ? -value : value);
        }

        private void Update(ILContext il)
        {
            var start = new ILCursor(il);
            if (!start.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchCall<SceneEntityState>("get_Center"),
                    i => i.MatchLdfld<Vector2>(nameof(Vector2.X))))
                throw new InvalidOperationException("Theo original bounds entry was not found.");
            var end = start.Clone();
            if (!end.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchCall<SceneEntityState>("get_X"),
                    i => i.MatchLdarg(0), i => i.MatchLdfld<TheoCrystalState>("Level")))
                throw new InvalidOperationException("Theo original bounds continuation was not found.");
            var originalEntry = start.Next;
            var continuation = end.Next;
            start.Emit(OpCodes.Ldarg_0);
            start.EmitDelegate<Func<TheoCrystalState, bool>>(Inverted);
            start.Emit(OpCodes.Brfalse, originalEntry);
            start.Emit(OpCodes.Ldarg_0);
            start.EmitDelegate<Action<TheoCrystalState>>(CheckInvertedBounds);
            start.Emit(OpCodes.Br, continuation);
        }

        private void CheckInvertedBounds(TheoCrystalState self)
        {
            var bounds = ((OriginalLevelSceneRoot)self.Scene).Bounds;
            if (self.Center.X > bounds.Right)
            {
                self.MoveH(32f * runtime.Engine.DeltaTime);
                if (self.Left - 8f > bounds.Right) self.RemoveSelf();
            }
            else if (self.Left < bounds.Left)
            {
                self.Left = bounds.Left;
                self.Speed.X *= -.4f;
            }
            else if (self.Bottom > bounds.Bottom + 4)
            {
                self.Bottom = bounds.Bottom - 4;
                self.Speed.Y = 0;
            }
            else if (self.Top < bounds.Top &&
                     ((IOriginalGameplayProcess)runtime.Engine).SaveContext.CurrentSave.Assists.Invincible)
            {
                self.Top = bounds.Top;
                self.Speed.Y = -300f;
                ((ISourceSpatialAudioEnvironment)runtime.Engine).SpatialAudio.Play(
                    "event:/game/general/assist_screenbottom", self.Position);
            }
            else if (self.Bottom < bounds.Top) self.Die();
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}

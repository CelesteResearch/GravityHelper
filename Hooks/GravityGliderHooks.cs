// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Rendering;
using Celeste.Game.Controls.Source;
using Celeste.Game.Gameplay.Holdables.Gliders;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Holdables;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityGliderHooks : IDisposable
    {
        private static readonly Action<OriginalGliderState, PixelCollision> CollideH =
            (Action<OriginalGliderState, PixelCollision>)typeof(OriginalGliderState)
                .GetMethod("OnCollideH", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<OriginalGliderState, PixelCollision>));

        private static readonly Action<OriginalGliderState, PixelCollision> CollideV =
            (Action<OriginalGliderState, PixelCollision>)typeof(OriginalGliderState)
                .GetMethod("OnCollideV", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<OriginalGliderState, PixelCollision>));

        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityGliderHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new ILHook(typeof(OriginalGliderState).GetMethod(nameof(OriginalGliderState.Update)),
                    PatchUpdate));
                hooks.Add(new Hook(typeof(OriginalGliderState).GetMethod(nameof(OriginalGliderState.Render)),
                    (Action<Action<OriginalGliderState, ISourceRenderContext>, OriginalGliderState,
                        ISourceRenderContext>)Render));
                hooks.Add(new Hook(typeof(OriginalGliderState).GetMethod(nameof(OriginalGliderState.Update)),
                    (Action<Action<OriginalGliderState>, OriginalGliderState>)Update));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(OriginalGliderState self) => ReferenceEquals(self.Hold.EngineContext, runtime.Engine);
        private bool Inverted(OriginalGliderState self) => Owns(self) && GravityEntityInversion.ShouldInvert(self);

        private void Render(Action<OriginalGliderState, ISourceRenderContext> original, OriginalGliderState self,
            ISourceRenderContext context)
        {
            if (!Inverted(self))
            {
                original(self, context);
                return;
            }

            var sprite = self.Get<Sprite>();
            var scale = sprite.Scale;
            float rotation = sprite.Rotation;
            sprite.Scale = new Vector2(scale.X, -scale.Y);
            sprite.Rotation = -rotation;
            try
            {
                original(self, context);
            }
            finally
            {
                sprite.Scale = scale;
                sprite.Rotation = rotation;
            }
        }

        private void Update(Action<OriginalGliderState> original, OriginalGliderState self)
        {
            if (!Owns(self))
            {
                original(self);
                return;
            }

            var axis = ((IOriginalGameInputProcessEnvironment)runtime.Engine).InputSource.GliderMoveY;
            int value = axis.Value;
            if ((runtime.PlayerComponent?.ShouldInvert ?? false) &&
                runtime.Settings.ControlScheme == ControlSchemeSetting.Absolute)
                ((Celeste.Core.Input.ISourceVirtualIntegerAxis)axis).Value = -value;
            try
            {
                original(self);
            }
            finally
            {
                ((Celeste.Core.Input.ISourceVirtualIntegerAxis)axis).Value = value;
            }
        }

        private void PatchUpdate(ILContext il)
        {
            var start = new ILCursor(il);
            if (!start.TryGotoNext(i => i.MatchCall<SourceActorEntityState>(nameof(SourceActorEntityState.MoveV))) ||
                !start.TryGotoNext(i => i.MatchLdarg(0), i => i.MatchCall<SceneEntityState>("get_Left")))
                throw new InvalidOperationException("Glider original post-MoveV bounds entry was not found.");
            var end = start.Clone();
            if (!end.TryGotoNext(i => i.MatchLdarg(0),
                    i => i.MatchLdfld<OriginalGliderState>(nameof(OriginalGliderState.Hold)),
                    i => i.MatchCallvirt<HoldableState>(nameof(HoldableState.CheckAgainstColliders))))
                throw new InvalidOperationException("Glider original bounds exit was not found.");
            var originalEntry = start.Next;
            var continuation = end.Next;
            start.Emit(OpCodes.Ldarg_0);
            start.EmitDelegate<Func<OriginalGliderState, bool>>(Inverted);
            start.Emit(OpCodes.Brfalse, originalEntry);
            start.Emit(OpCodes.Ldarg_0);
            start.EmitDelegate<Func<OriginalGliderState, bool>>(CheckInvertedBounds);
            start.Emit(OpCodes.Brfalse, continuation);
            start.Emit(OpCodes.Ret);
        }

        private static bool CheckInvertedBounds(OriginalGliderState self)
        {
            var bounds = ((OriginalLevelSceneRoot)self.Scene).Bounds;
            if (self.Left < bounds.Left)
            {
                self.Left = bounds.Left;
                CollideH(self, new PixelCollision(-Vector2.UnitX, Vector2.Zero, Vector2.Zero));
            }
            else if (self.Right > bounds.Right)
            {
                self.Right = bounds.Right;
                CollideH(self, new PixelCollision(Vector2.UnitX, Vector2.Zero, Vector2.Zero));
            }

            if (self.Bottom > bounds.Bottom)
            {
                self.Bottom = bounds.Bottom;
                CollideV(self, new PixelCollision(Vector2.UnitY, Vector2.Zero, Vector2.Zero));
            }
            else if (self.Bottom < bounds.Top - 16)
            {
                self.RemoveSelf();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}

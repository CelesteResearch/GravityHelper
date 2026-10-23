// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Audio;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Enemies.AngryOshiro;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityAngryOshiroHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityAngryOshiroHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            var type = typeof(OriginalAngryOshiroState);
            try
            {
                hooks.Add(new Hook(
                    type.GetConstructor([typeof(Vector2), typeof(bool), typeof(IOriginalAngryOshiroProcess)]),
                    (ConstructorHook)Construct));
                hooks.Add(new Hook(type.GetMethod("OnPlayerBounce", BindingFlags.Instance | BindingFlags.NonPublic),
                    (BounceHook)Bounce));
                hooks.Add(new Hook(type.GetMethod("HurtUpdate", BindingFlags.Instance | BindingFlags.NonPublic),
                    (HurtHook)Hurt));
                hooks.Add(new Hook(type.GetMethod(nameof(OriginalAngryOshiroState.Render)), (RenderHook)Render));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(OriginalAngryOshiroState self) =>
            ReferenceEquals(AngryOshiroSourceLoans.Engine(self), runtime.Engine);

        private void Construct(ConstructorOriginal original, OriginalAngryOshiroState self, Vector2 position,
            bool fromCutscene, IOriginalAngryOshiroProcess process)
        {
            original(self, position, fromCutscene, process);
            if (!ReferenceEquals(process, runtime.Engine)) return;
            var collider = AngryOshiroSourceLoans.BounceCollider(self).Collider;
            float normalTop = collider.Top;
            float invertedBottom = self.Collider.Bottom + (self.Collider.Top - normalTop);
            self.Add(new PlayerGravityListener(runtime, (_, args) =>
            {
                if (args.NewValue == GravityType.Normal) collider.Top = normalTop;
                else if (args.NewValue == GravityType.Inverted) collider.Bottom = invertedBottom;
            }));
        }

        private void Bounce(BounceOriginal original, OriginalAngryOshiroState self, RoomPlayerSession player)
        {
            if (!Owns(self) || runtime.PlayerComponent?.ShouldInvert != true)
            {
                original(self, player);
                return;
            }

            var state = AngryOshiroSourceLoans.State(self);
            if (state.State != 2 || player.Top < self.Bottom - 6) return;
            DynamicData.For(self).Set("GravityHelper_Inverted", true);
            ((ISourceSpatialAudioEnvironment)runtime.Engine).SpatialAudio.Play("event:/game/general/thing_booped",
                self.Position);
            ((ISourceFreezeEnvironment)runtime.Engine).Freeze(.2f);
            player.Bounce(self.Bottom - 2f);
            state.State = 5;
            AngryOshiroSourceLoans.Precharge(self).Stop();
            AngryOshiroSourceLoans.Charge(self).Stop();
        }

        private int Hurt(HurtOriginal original, OriginalAngryOshiroState self)
        {
            if (!Owns(self) || !GravityEntityInversion.ShouldInvert(self)) return original(self);
            self.X += 100f * runtime.Engine.DeltaTime;
            self.Y -= 200f * runtime.Engine.DeltaTime;
            var level = (OriginalLevelSceneRoot)self.Scene;
            if (self.Bottom >= (double)(level.Bounds.Top - 20)) return 5;
            DynamicData.For(self).Set("GravityHelper_Inverted", false);
            if (AngryOshiroSourceLoans.Leaving(self))
            {
                self.RemoveSelf();
                return 5;
            }

            self.X = level.Camera.Left - 48f;
            AngryOshiroSourceLoans.SetCameraOffset(self, -48f);
            AngryOshiroSourceLoans.SetRespawnAnimation(self, true);
            self.Visible = false;
            return 0;
        }

        private void Render(RenderOriginal original, OriginalAngryOshiroState self, ISourceRenderContext context)
        {
            if (!Owns(self) || !GravityEntityInversion.ShouldInvert(self))
            {
                original(self, context);
                return;
            }

            float scaleY = self.Sprite.Scale.Y;
            self.Sprite.Scale.Y = -scaleY;
            original(self, context);
            self.Sprite.Scale.Y = scaleY;
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void ConstructorOriginal(OriginalAngryOshiroState self, Vector2 position, bool fromCutscene,
            IOriginalAngryOshiroProcess process);

        private delegate void ConstructorHook(ConstructorOriginal original, OriginalAngryOshiroState self,
            Vector2 position, bool fromCutscene, IOriginalAngryOshiroProcess process);

        private delegate void BounceOriginal(OriginalAngryOshiroState self, RoomPlayerSession player);

        private delegate void BounceHook(BounceOriginal original, OriginalAngryOshiroState self,
            RoomPlayerSession player);

        private delegate int HurtOriginal(OriginalAngryOshiroState self);

        private delegate int HurtHook(HurtOriginal original, OriginalAngryOshiroState self);

        private delegate void RenderOriginal(OriginalAngryOshiroState self, ISourceRenderContext context);

        private delegate void RenderHook(RenderOriginal original, OriginalAngryOshiroState self,
            ISourceRenderContext context);
    }
}

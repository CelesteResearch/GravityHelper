// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Enemies.Seekers;
using Celeste.Mods.GravityHelper.Runtime;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravitySeekerHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Hook hook;

        public GravitySeekerHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new Hook(typeof(OriginalSeekerState).GetMethod(nameof(OriginalSeekerState.Update)),
                (UpdateHook)Update);
        }

        private void Update(UpdateOriginal original, OriginalSeekerState seeker)
        {
            if (seeker.Scene is ISourceEngineContextScene scene && ReferenceEquals(scene.EngineContext, runtime.Engine))
            {
                var bounce = seeker.BounceHitbox;
                var attack = seeker.AttackHitbox;
                if ((runtime.PlayerComponent?.ShouldInvert ?? false) != (bounce.Top > attack.Top))
                {
                    bounce.Top = -bounce.Bottom;
                    attack.Top = -attack.Bottom;
                }
            }

            original(seeker);
        }

        public void Dispose() => hook.Dispose();

        private delegate void UpdateOriginal(OriginalSeekerState seeker);

        private delegate void UpdateHook(UpdateOriginal original, OriginalSeekerState seeker);
    }
}

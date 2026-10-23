// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Hazards.Snowballs;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision.Shapes;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravitySnowballHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Hook hook;

        public GravitySnowballHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new Hook(typeof(OriginalSnowballState).GetMethod(nameof(OriginalSnowballState.Update)),
                (UpdateHook)Update);
        }

        private void Update(UpdateOriginal original, OriginalSnowballState snowball)
        {
            if (snowball.Scene is ISourceEngineContextScene scene &&
                ReferenceEquals(scene.EngineContext, runtime.Engine))
            {
                var bounce = (HitboxColliderGeometry)snowball.BounceCollider;
                var attack = snowball.Collider;
                if ((runtime.PlayerComponent?.ShouldInvert ?? false) != (bounce.Top > attack.Top))
                {
                    bounce.Top = -bounce.Bottom;
                    attack.Top = -attack.Bottom;
                }
            }

            original(snowball);
        }

        public void Dispose() => hook.Dispose();

        private delegate void UpdateOriginal(OriginalSnowballState snowball);

        private delegate void UpdateHook(UpdateOriginal original, OriginalSnowballState snowball);
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Mods.GravityHelper.Entities.Springs;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravitySpringHooks : IDisposable
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Func<OriginalSpringState, bool> PlayerCanUse = Getter<bool>("playerCanUse");

        private static readonly Func<OriginalSpringState, IOriginalSpringProcess> Process =
            Getter<IOriginalSpringProcess>("process");

        private static readonly Action<OriginalSpringState> Bounce =
            (Action<OriginalSpringState>)typeof(OriginalSpringState).GetMethod("BounceAnimate", PrivateInstance)
                .CreateDelegate(typeof(Action<OriginalSpringState>));

        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravitySpringHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new Hook(typeof(OriginalSpringState).GetMethod("OnCollide", PrivateInstance),
                (CollisionHook)OnCollide));
            hooks.Add(new Hook(typeof(OriginalSpringState).GetMethod("BounceAnimate", PrivateInstance),
                (BounceHook)BounceAnimate));
        }

        private void OnCollide(CollisionOriginal original, OriginalSpringState spring, RoomPlayerSession player)
        {
            if (!ReferenceEquals(Process(spring), runtime.Engine) ||
                !(runtime.PlayerComponent?.ShouldInvert ?? false) ||
                spring.Orientation != SpringOrientation.Floor)
            {
                original(spring, player);
                return;
            }

            if (player.Machine.State == 9 || !PlayerCanUse(spring)) return;
            if (player.Data.Motion.Normal.Speed.Y > 0) return;
            Bounce(spring);
            GravitySpring.InvertedSuperBounce(player, spring.Top);
        }

        private void BounceAnimate(BounceOriginal original, OriginalSpringState spring)
        {
            if (ReferenceEquals(Process(spring), runtime.Engine) && spring is GravitySpring gravitySpring)
                gravitySpring.CustomBounceAnimate();
            else original(spring);
        }

        public void Dispose()
        {
            foreach (var hook in hooks) hook.Dispose();
            hooks.Clear();
        }

        private static Func<OriginalSpringState, T> Getter<T>(string name)
        {
            var method = new DynamicMethod(name, typeof(T), new[] { typeof(OriginalSpringState) },
                typeof(OriginalSpringState), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, typeof(OriginalSpringState).GetField(name, PrivateInstance));
            il.Emit(OpCodes.Ret);
            return (Func<OriginalSpringState, T>)method.CreateDelegate(typeof(Func<OriginalSpringState, T>));
        }

        private delegate void CollisionOriginal(OriginalSpringState spring, RoomPlayerSession player);

        private delegate void CollisionHook(CollisionOriginal original, OriginalSpringState spring,
            RoomPlayerSession player);

        private delegate void BounceOriginal(OriginalSpringState spring);

        private delegate void BounceHook(BounceOriginal original, OriginalSpringState spring);
    }
}

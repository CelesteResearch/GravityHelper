// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Rendering;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityMoveBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = new();

        public GravityMoveBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new Hook(
                typeof(MoveBlockState).GetConstructor(new[]
                {
                    typeof(Vector2), typeof(int), typeof(int), typeof(MoveBlockDirection), typeof(bool), typeof(bool),
                    typeof(ISourceSolidWorld)
                }),
                (Action<Action<MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld>,
                    MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld>)Construct));
            hooks.Add(new Hook(
                typeof(MoveBlockState).GetConstructor(new[]
                    { typeof(EntityData), typeof(Vector2), typeof(ISourceSolidWorldFactory) }),
                (Action<Action<MoveBlockState, EntityData, Vector2, ISourceSolidWorldFactory>, MoveBlockState,
                    EntityData, Vector2, ISourceSolidWorldFactory>)ConstructData));
            hooks.Add(new Hook(typeof(MovingBlockState).GetMethod("Render"),
                (Action<Action<MovingBlockState, ISourceRenderContext>, MovingBlockState, ISourceRenderContext>)
                Render));
            hooks.Add(new Hook(typeof(MovingBlockState).GetMethod("MoveVExact"),
                (Action<Action<MovingBlockState, int>, MovingBlockState, int>)MoveVExact));
        }

        private void Construct(
            Action<MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld> orig,
            MoveBlockState self, Vector2 position, int width, int height, MoveBlockDirection direction, bool canSteer,
            bool fast, ISourceSolidWorld world)
        {
            orig(self, position, width, height, direction, canSteer, fast, world);
            if (ReferenceEquals(world.EngineContext, runtime.Engine) && canSteer &&
                (direction == MoveBlockDirection.Left || direction == MoveBlockDirection.Right))
                self.OwnedComponents.Add(new MoveBlockBottomComponent());
        }

        private void ConstructData(Action<MoveBlockState, EntityData, Vector2, ISourceSolidWorldFactory> orig,
            MoveBlockState self, EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
        {
            orig(self, data, offset, worlds);
            if (ReferenceEquals(MovingBlockSourceLoans.World(self).EngineContext, runtime.Engine) && self.CanSteer &&
                (self.Direction == MoveBlockDirection.Left || self.Direction == MoveBlockDirection.Right))
                self.OwnedComponents.Add(new MoveBlockBottomComponent());
        }

        private void Render(Action<MovingBlockState, ISourceRenderContext> orig, MovingBlockState self,
            ISourceRenderContext context)
        {
            if (self is MoveBlockState &&
                ReferenceEquals(MovingBlockSourceLoans.World(self).EngineContext, runtime.Engine) &&
                self.Get<MoveBlockBottomComponent>() is { } component)
            {
                var position = self.Position;
                self.Position += self.Shake;
                component.Render(context);
                self.Position = position;
            }

            orig(self, context);
        }

        private void MoveVExact(Action<MovingBlockState, int> orig, MovingBlockState self, int move)
        {
            if (self is MoveBlockState &&
                ReferenceEquals(MovingBlockSourceLoans.World(self).EngineContext, runtime.Engine) &&
                runtime.PlayerComponent?.ShouldInvert == true)
            {
                var noSquish = MovingBlockSourceLoans.NoSquish(self);
                if (noSquish != null && move > 0 && noSquish.Top >= self.Bottom)
                    while (move != 0 && noSquish.Body.CollideSolidAt(noSquish.Position + Vector2.UnitY * move))
                        move -= Math.Sign(move);
            }

            orig(self, move);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}

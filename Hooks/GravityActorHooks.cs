// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityActorHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly Func<SceneEntityState, bool> shouldInvert;
        private readonly Func<SceneEntityState, bool> shouldInvertChecked;
        private readonly GravityOverrideState overrides;
        private readonly IGravityJumpThruContactQueries contacts;
        private readonly List<IDisposable> hooks = [];

        public GravityActorHooks(ISourceEngineContext engine, Func<SceneEntityState, bool> shouldInvert,
            Func<SceneEntityState, bool> shouldInvertChecked, GravityOverrideState overrides)
        {
            this.engine = engine;
            this.shouldInvert = shouldInvert;
            this.shouldInvertChecked = shouldInvertChecked;
            this.overrides = overrides;
            contacts = GravityRuntimeContext.For(engine).JumpThruContacts;
            Type actor = typeof(SourceActorEntityState);
            hooks.Add(new Hook(actor.GetMethod(nameof(SourceActorEntityState.MoveV)),
                (MoveFloatHook)MoveVertical));
            hooks.Add(new Hook(actor.GetMethod(nameof(SourceActorEntityState.MoveVExact)),
                (MoveIntegerHook)MoveVerticalExact));
            hooks.Add(new Hook(actor.GetProperty(nameof(SourceActorEntityState.LiftSpeed)).GetMethod,
                (LiftSpeedHook)GetLiftSpeed));
            hooks.Add(new Hook(
                actor.GetMethod(nameof(SourceActorEntityState.IsRiding), [typeof(ISourceEntityCollider)]),
                (RideSolidHook)RideSolid));
            hooks.Add(new Hook(
                actor.GetMethod(nameof(SourceActorEntityState.IsRiding), [typeof(ISourceJumpThruCollider)]),
                (RideJumpThruHook)RideJumpThru));
            hooks.Add(new ILHook(actor.GetMethod(nameof(SourceActorEntityState.OnGround), [typeof(int)]),
                OnGround));
            hooks.Add(new ILHook(typeof(SourceActorSquish).GetMethod(nameof(SourceActorSquish.TryWiggle)),
                TransformSquishOffsets));
            hooks.Add(new ILHook(typeof(PixelActor).GetMethod(nameof(PixelActor.MoveVExact)), SelectRegularJumpThru));
        }

        private bool Owns(SourceActorEntityState actor)
            => actor.Scene is ISourceEngineContextScene scene && ReferenceEquals(scene.EngineContext, engine);

        private bool Checked(SourceActorEntityState actor) => Owns(actor) && shouldInvertChecked(actor);

        private bool MoveVertical(MoveFloat original, SourceActorEntityState actor, float amount,
            SourceCollisionHandler onCollide, ISourceEntityCollider pusher)
        {
            if (overrides.Semaphore > 0 || !Checked(actor)) return original(actor, amount, onCollide, pusher);
            // Source uses this scope around rounded movement: its nested MoveVExact must not flip again.
            overrides.Semaphore++;
            bool result = original(actor, -amount, onCollide, pusher);
            overrides.Semaphore--;
            return result;
        }

        private bool MoveVerticalExact(MoveInteger original, SourceActorEntityState actor, int amount,
            SourceCollisionHandler onCollide, ISourceEntityCollider pusher)
            => original(actor, overrides.Semaphore <= 0 && Checked(actor) ? -amount : amount, onCollide, pusher);

        private Vector2 GetLiftSpeed(LiftSpeed original, SourceActorEntityState actor)
        {
            Vector2 speed = original(actor);
            if (Owns(actor) && shouldInvert(actor)) speed.Y = -speed.Y;
            return speed;
        }

        private bool RideSolid(RideSolidOriginal original, SourceActorEntityState actor, ISourceEntityCollider solid)
            => !Checked(actor)
                ? original(actor, solid)
                : SourceEntityCollision.CheckAt(actor, solid, actor.Position - Vector2.UnitY);

        private bool RideJumpThru(RideJumpThruOriginal original, SourceActorEntityState actor,
            ISourceJumpThruCollider jumpThru)
        {
            if (!Owns(actor)) return original(actor, jumpThru);
            if (actor.IgnoreJumpThrus) return false;
            bool inverted = shouldInvertChecked(actor);
            bool upsideDown = IsUpsideDown(actor, jumpThru);
            return inverted == upsideDown && !SourceEntityCollision.Check(actor, jumpThru) &&
                   SourceEntityCollision.CheckAt(actor, jumpThru,
                       actor.Position + (inverted ? -Vector2.UnitY : Vector2.UnitY));
        }

        private void OnGround(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdarg(1));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, SourceActorEntityState, int>>(GroundDirection);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchLdarg(0),
                instruction => instruction.MatchCall<SourceActorEntityState>("get_Body"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<SourceActorEntityState, int, bool>>(GroundJumpThru);
            cursor.Emit(OpCodes.Ret);
        }

        private int GroundDirection(int downCheck, SourceActorEntityState actor)
            => Checked(actor) ? -downCheck : downCheck;

        private bool GroundJumpThru(SourceActorEntityState actor, int downCheck)
        {
            bool inverted = Checked(actor);
            Vector2 at = actor.Position + new Vector2(0, inverted ? -downCheck : downCheck);
            return Owns(actor) ? JumpThruOutside(actor, at, inverted) : actor.Body.CollideJumpThruOutside(at);
        }

        private bool JumpThruOutside(SourceActorEntityState actor, Vector2 at, bool inverted)
            => contacts.First(actor, at, inverted, true) != null;

        private bool IsUpsideDown(SourceActorEntityState actor, ISourceJumpThruCollider jumpThru)
            => contacts.IsUpsideDown(jumpThru);

        private void SelectRegularJumpThru(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall(typeof(Math), nameof(Math.Sign)));
            if (!cursor.Next.MatchStloc(out int signIndex))
                throw new InvalidOperationException("Actor vertical sign local changed.");
            var sign = il.Body.Variables[signIndex];
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<PixelActor>("SolidAt"));
            var ceilingHit = (VariableDefinition)cursor.Prev.Previous.Operand;
            cursor.GotoNext(MoveType.Before, instruction => instruction.MatchBrtrue(out _));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, sign);
            cursor.Emit(OpCodes.Ldloca, ceilingHit);
            cursor.EmitDelegate<NegativeJumpThruSelection>(SelectNegativeJumpThru);
            cursor.GotoNext(MoveType.Before, instruction => instruction.MatchCall<PixelActor>("JumpThruOutside"));
            // The real call's out local remains the actual collision-target storage used below.
            var hit = (Mono.Cecil.Cil.VariableDefinition)cursor.Prev.Operand;
            cursor.Index++;
            cursor.Emit(OpCodes.Ldloca, hit);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<JumpThruSelection>(SelectRegularJumpThru);
        }

        private delegate bool NegativeJumpThruSelection(bool found, PixelActor body, int direction,
            ref PixelPlatform hit);

        private bool SelectNegativeJumpThru(bool found, PixelActor body, int direction, ref PixelPlatform hit)
        {
            if (found || direction >= 0 || body.IgnoreJumpThrus ||
                body.SourceColliderOwner is not SourceActorEntityState actor || !Owns(actor)) return found;
            var candidate = contacts.First(actor, actor.Position - Vector2.UnitY, true, true);
            hit = candidate?.Platform;
            return candidate != null;
        }

        private bool SelectRegularJumpThru(bool found, ref PixelPlatform hit, PixelActor body)
        {
            if (!found || body.SourceColliderOwner is not SourceActorEntityState actor || !Owns(actor) ||
                hit?.SourceEntityOwner is not ISourceJumpThruCollider jumpThru || !IsUpsideDown(actor, jumpThru))
                return found;
            // GravityHelper's original IL differs from generic ceiling filtering: after an upside-down
            // first hit it scans the source JumpThru bucket again, keeping tracker order among regular ones.
            var candidate = contacts.First(actor, actor.Position + Vector2.UnitY, false, true);
            hit = candidate?.Platform;
            return candidate != null;
        }

        private void TransformSquishOffsets(ILContext il)
        {
            var cursor = new ILCursor(il);
            int transformed = 0;
            while (cursor.TryGotoNext(MoveType.Before, i => i.MatchCall<Vector2>("op_Addition")))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<Vector2, PixelActor, Vector2>>((offset, body) =>
                {
                    if (body.SourceColliderOwner is SourceActorEntityState actor && Checked(actor))
                        offset.Y = -offset.Y;
                    return offset;
                });
                cursor.Index++;
                transformed++;
            }

            if (transformed != 4) throw new InvalidOperationException("Actor squish offset sites changed.");
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool MoveFloat(SourceActorEntityState actor, float amount, SourceCollisionHandler collision,
            ISourceEntityCollider pusher);

        private delegate bool MoveInteger(SourceActorEntityState actor, int amount, SourceCollisionHandler collision,
            ISourceEntityCollider pusher);

        private delegate bool MoveFloatHook(MoveFloat original, SourceActorEntityState actor, float amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher);

        private delegate bool MoveIntegerHook(MoveInteger original, SourceActorEntityState actor, int amount,
            SourceCollisionHandler collision, ISourceEntityCollider pusher);

        private delegate Vector2 LiftSpeed(SourceActorEntityState actor);

        private delegate Vector2 LiftSpeedHook(LiftSpeed original, SourceActorEntityState actor);

        private delegate bool RideSolidOriginal(SourceActorEntityState actor, ISourceEntityCollider solid);

        private delegate bool RideSolidHook(RideSolidOriginal original, SourceActorEntityState actor,
            ISourceEntityCollider solid);

        private delegate bool RideJumpThruOriginal(SourceActorEntityState actor, ISourceJumpThruCollider jumpThru);

        private delegate bool RideJumpThruHook(RideJumpThruOriginal original, SourceActorEntityState actor,
            ISourceJumpThruCollider jumpThru);

        private delegate bool JumpThruSelection(bool found, ref PixelPlatform hit, PixelActor body);
    }
}

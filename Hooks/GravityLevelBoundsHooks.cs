// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Geometry;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Camera;
using Celeste.Simulation.Gameplay.Collision;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityLevelBoundsHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;

        private static readonly Action<OriginalLevelSceneRoot, Vector2, Vector2> NextLevel =
            (Action<OriginalLevelSceneRoot, Vector2, Vector2>)typeof(OriginalLevelSceneRoot)
                .GetMethod("NextLevel", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<OriginalLevelSceneRoot, Vector2, Vector2>));

        public GravityLevelBoundsHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new ILHook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.EnforceBounds)),
                PatchBounds);
        }

        private bool Owns(OriginalLevelSceneRoot level) => ReferenceEquals(level.EngineContext, runtime.Engine);

        private bool Inverted(OriginalLevelSceneRoot level) =>
            Owns(level) && (runtime.PlayerComponent?.ShouldInvert ?? false);

        private static bool DisabledUp(OriginalLevelSceneRoot level) =>
            level.Scheduler.Entities.TryGetTrackedEntities(typeof(DisableUpTransitionController), out var values) &&
            values.Count != 0;

        private void PatchBounds(ILContext il)
        {
            var cursor = new ILCursor(il);
            for (int index = 0; index < 3; index++)
                if (!cursor.TryGotoNext(MoveType.After,
                        instruction => instruction.Operand is FieldReference field && field.Name == "CameraLockMode"))
                    throw new InvalidOperationException("EnforceBounds vertical-tail CameraLockMode entry missing.");
            cursor.Index -= 2;
            Instruction entry = cursor.Next;
            cursor.Goto(entry, MoveType.AfterLabel);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<OriginalLevelSceneRoot, RoomPlayerSession, bool>>((level, player) =>
            {
                if (!Inverted(level)) return false;
                ApplyInvertedVerticalBounds(level, player);
                return true;
            });
            cursor.Emit(OpCodes.Brfalse, entry);
            cursor.Emit(OpCodes.Ret);
            cursor.GotoNext(instruction => instruction.Operand is MethodReference method &&
                                           method.Name == nameof(RoomPlayerSession.BeforeUpTransition));
            cursor.GotoPrev(MoveType.After,
                instruction => instruction.Operand is MethodReference method &&
                               method.Name == nameof(OriginalChapterMapData.CanTransitionTo));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, OriginalLevelSceneRoot, bool>>((can, level) =>
                can && (!Owns(level) || !DisabledUp(level)));
        }

        private void ApplyInvertedVerticalBounds(OriginalLevelSceneRoot level, RoomPlayerSession player)
        {
            IntRect bounds = level.Bounds;
            var rectangle = new IntRect((int)level.Camera.Left, (int)level.Camera.Top, 320, 180);
            var map = (OriginalChapterMapData)level.Session.MapData;
            bool invincible = ((IOriginalGameplayProcess)runtime.Engine).SaveContext.CurrentSave.Assists.Invincible;

            void TryDie(int bounceAt)
            {
                if (invincible)
                {
                    player.Play("event:/game/general/assist_screenbottom");
                    player.Bounce(bounceAt);
                }
                else player.Die(Vector2.Zero);
            }

            if (level.CameraLockMode != RoomCameraLockMode.None && player.Bottom > rectangle.Bottom)
            {
                player.Bottom = rectangle.Bottom;
                player.OnBoundsV();
            }
            else if (player.CenterY > bounds.Bottom)
            {
                if (map.CanTransitionTo(level, player.Center + Vector2.UnitY * 12) &&
                    !((OriginalLevelRoomData)level.Session.LevelData).DisableDownTransition &&
                    !SourceSceneCollisionQueries.CheckAt(player,
                        ((ISourceScene)player.Scene).SourceScene.Entities.GetTrackedEntities("Celeste.Solid"),
                        player.Position + Vector2.UnitY * 4))
                {
                    player.BeforeDownTransition();
                    NextLevel(level, player.Center + Vector2.UnitY * 12, Vector2.UnitY);
                }
                else if (player.Bottom > bounds.Bottom + 24)
                {
                    player.Bottom = bounds.Bottom + 24;
                    player.OnBoundsV();
                }
            }

            if (level.CameraLockMode != RoomCameraLockMode.None && rectangle.Top > bounds.Top + 4 &&
                player.Bottom < rectangle.Top)
                TryDie(rectangle.Top);
            else if (player.Top < bounds.Top && map.CanTransitionTo(level, player.Center - Vector2.UnitY * 12) &&
                     !DisabledUp(level))
            {
                player.BeforeUpTransition();
                NextLevel(level, player.Center - Vector2.UnitY * 12, -Vector2.UnitY);
            }
            else if (player.Bottom < bounds.Top && invincible) TryDie(bounds.Top);
            else if (player.Bottom < bounds.Top - 4) player.Die(Vector2.Zero);
        }

        public void Dispose() => hook.Dispose();
    }
}

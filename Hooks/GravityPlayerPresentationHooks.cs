// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Components;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Enemies.Badeline.Chasers;
using Celeste.Game.Entities.BadelineDummy;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Presentation.Hair;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.Presentation.Player.Death;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerPresentationHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Type ghostType;
        private readonly bool renderOnly;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerPresentationHooks(GravityRuntimeContext runtime, Type ghostType = null)
            : this(runtime, ghostType, false)
        {
        }

        public GravityPlayerPresentationHooks(GravityRuntimeContext runtime, Type ghostType, bool renderOnly)
        {
            this.runtime = runtime;
            this.ghostType = ghostType;
            this.renderOnly = renderOnly;
            if (!renderOnly)
            {
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Render)),
                    (PlayerRenderHook)RenderPlayer));
                hooks.Add(new Hook(
                    typeof(RoomPlayerSession).GetMethod("CreateTrail", BindingFlags.NonPublic | BindingFlags.Instance),
                    (TrailHook)CreateTrail));
            }

            hooks.Add(new Hook(typeof(BadelineDummyPresentation).GetMethod(nameof(BadelineDummyPresentation.Render)),
                (DummyRenderHook)RenderDummy));
            hooks.Add(new Hook(typeof(PlayerSprite).GetMethod(nameof(PlayerSprite.Render)),
                (SpriteRenderHook)RenderSprite));
            hooks.Add(new ILHook(typeof(PlayerHair).GetMethod(nameof(PlayerHair.AfterUpdate)), AdjustHairOffsets));
            // Shared Render delegates all outline/color passes to its real local Draw. Hook only that render site.
            var draw = typeof(PlayerHair).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name.StartsWith("<Render>g__Draw|", StringComparison.Ordinal));
            hooks.Add(new ILHook(draw, AdjustRenderedHairScale));
        }

        private bool Owns(object entity)
            => entity is ISourceEntityScene { Scene: ISourceEngineContextScene scene } &&
               ReferenceEquals(scene.EngineContext, runtime.Engine);

        private bool PlayerInverted => runtime.PlayerComponent?.ShouldInvert ?? false;

        private static bool EntityInverted(object entity)
            => DynamicData.For(entity).Data.TryGetValue("GravityHelper_Inverted", out var value) && (bool)value;

        private void RenderPlayer(PlayerRender original, RoomPlayerSession player, ISourceRenderContext context)
        {
            float scaleY = player.Sprite.Scale.Y;
            bool invert = Owns(player) && runtime.PlayerComponent != null &&
                          (PlayerInverted ^ runtime.ForceInvertPlayerRenderSemaphore > 0);
            if (invert) player.Sprite.Scale.Y = -scaleY;
            original(player, context);
            if (invert) player.Sprite.Scale.Y = scaleY;
        }

        private void CreateTrail(TrailOriginal original, RoomPlayerSession player)
        {
            if (!Owns(player) || !PlayerInverted)
            {
                original(player);
                return;
            }

            float scaleY = player.Sprite.Scale.Y;
            player.Sprite.Scale.Y = -scaleY;
            original(player);
            player.Sprite.Scale.Y = scaleY;
        }

        private void RenderSprite(SpriteRender original, PlayerSprite sprite, ISourceRenderContext context)
        {
            if (sprite.Entity is RoomPlayerSession)
            {
                original(sprite, context);
                return;
            }

            bool invert = sprite.Entity != null && Owns(sprite.Entity) &&
                          (sprite.Entity is BadelineOldsiteState || sprite.Entity.GetType() == ghostType) &&
                          EntityInverted(sprite.Entity);
            float scaleY = sprite.Scale.Y;
            if (invert) sprite.Scale.Y = -scaleY;
            original(sprite, context);
            if (invert) sprite.Scale.Y = scaleY;
        }

        private Vector2 TransformHairVector(Vector2 value, PlayerHair hair)
        {
            if (hair?.Entity == null || !Owns(hair.Entity)) return value;
            if (renderOnly && hair.Entity is not RoomPlayerSession && hair.Entity is not PlayerDeathPresentation &&
                hair.Entity is not BadelineDummyPresentation && hair.Entity is not BadelineOldsiteState &&
                hair.Entity.GetType() != ghostType) return value;
            bool inverted = hair.Entity is RoomPlayerSession ? PlayerInverted : EntityInverted(hair.Entity);
            return inverted ? new Vector2(value.X, -value.Y) : value;
        }

        private void RenderDummy(DummyRender original, BadelineDummyPresentation dummy, ISourceRenderContext context)
        {
            bool inverted = Owns(dummy) && EntityInverted(dummy);
            float scaleY = dummy.Sprite.Scale.Y;
            if (inverted) dummy.Sprite.Scale.Y = -scaleY;
            original(dummy, context);
            if (inverted) dummy.Sprite.Scale.Y = scaleY;
        }

        private void AdjustHairOffsets(ILContext il)
        {
            var cursor = new ILCursor(il);

            void TransformNextAddition()
            {
                cursor.GotoNext(MoveType.Before, i => i.MatchCall<Vector2>("op_Addition"));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<Vector2, PlayerHair, Vector2>>(TransformHairVector);
                cursor.Index++;
            }

            TransformNextAddition();
            TransformNextAddition();
            cursor.GotoNext(i => i.MatchLdfld<PlayerHair>(nameof(PlayerHair.StepYSinePerSegment)));
            TransformNextAddition();
            TransformNextAddition();
            cursor.GotoNext(i => i.MatchLdfld<PlayerHair>(nameof(PlayerHair.StepYSinePerSegment)));
            TransformNextAddition();
            TransformNextAddition();
        }

        private void AdjustRenderedHairScale(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, i => i.MatchCall<PlayerHair>(nameof(PlayerHair.GetHairScale)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, PlayerHair, Vector2>>(TransformHairVector);
            if (cursor.TryGotoNext(i => i.MatchCall<PlayerHair>(nameof(PlayerHair.GetHairScale))))
                throw new InvalidOperationException("Shared hair Draw has additional scale sites.");
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void PlayerRender(RoomPlayerSession player, ISourceRenderContext context);

        private delegate void PlayerRenderHook(PlayerRender original, RoomPlayerSession player,
            ISourceRenderContext context);

        private delegate void TrailOriginal(RoomPlayerSession player);

        private delegate void TrailHook(TrailOriginal original, RoomPlayerSession player);

        private delegate void DummyRender(BadelineDummyPresentation dummy, ISourceRenderContext context);

        private delegate void DummyRenderHook(DummyRender original, BadelineDummyPresentation dummy,
            ISourceRenderContext context);

        private delegate void SpriteRender(PlayerSprite sprite, ISourceRenderContext context);

        private delegate void SpriteRenderHook(SpriteRender original, PlayerSprite sprite,
            ISourceRenderContext context);
    }
}

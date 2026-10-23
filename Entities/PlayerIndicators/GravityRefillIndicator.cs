// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Components;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Presentation.Shaders;


namespace Celeste.Mods.GravityHelper.Entities.PlayerIndicators
{
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.GravityRefillIndicator")]
    public class GravityRefillIndicator : SceneEntityState
    {
        private const int columns = 3;

        private string currentKey => (runtime.PlayerComponent?.ShouldInvert ?? false) ? "down" : "up";

        private readonly Sprite _sprite;

        private readonly GravityRuntimeContext runtime;

        public GravityRefillIndicator(ISourceEngineContext engine) : base(((ISourceEntitySourceDataEnvironment)engine)
            .EntitySourceDataRegistration)
        {
            runtime = GravityRuntimeContext.For(engine);
            Depth = -1000000;

            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;

            Add(_sprite = runtime.GameSprites.Create("gravityRefillIndicator"));
        }

        public override void Update()
        {
            base.Update();

            if (((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>() is not { } player)
            {
                Visible = false;
                return;
            }

            var charges = (runtime.PlayerComponent as PlayerGravityComponent)?.GravityCharges ?? 0;
            var hasCharges = charges > 0;
            var offset = new Vector2(0, (runtime.PlayerComponent?.ShouldInvert ?? false) ? 20f : -20f);
            Position = player.Position + offset;
            Visible = hasCharges;

            var key = currentKey;

            if (_sprite.Animating && !hasCharges)
                _sprite.Stop();
            else if (!_sprite.Animating && hasCharges)
                _sprite.Play(key, true);
            else if (_sprite.Animating && _sprite.CurrentAnimationID != key)
            {
                var frame = _sprite.CurrentAnimationFrame;
                _sprite.Play(key);
                _sprite.SetAnimationFrame(frame);
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            if (!_sprite.Animating) return;

            var numberOfCharges = (runtime.PlayerComponent as PlayerGravityComponent)?.GravityCharges ?? 0;
            if (numberOfCharges == 0) return;

            var rows = (int)Math.Ceiling(numberOfCharges / (float)columns);
            var finalRowItems = numberOfCharges % columns;
            if (finalRowItems == 0) finalRowItems = columns;
            var yOffset = (int)_sprite.Height;
            var xHalfOffset = (int)Math.Ceiling(_sprite.Width / 2f + 1);

            if (!(runtime.PlayerComponent?.ShouldInvert ?? false))
                yOffset = -yOffset;

            using var _ = GravityTintShader.For(runtime).Enter(context);
            _sprite.Color =
                runtime.Settings.ColorSchemeType == ColorSchemeSetting.Default
                    ? PackedColor.White
                    : runtime.Settings.GetColorScheme()[GravityType.Toggle];

            for (int row = 0; row < rows; row++)
            {
                _sprite.Position.Y = row * yOffset;
                int x = (columns - 1) * -xHalfOffset;
                int rowItems = row < rows - 1 ? columns : finalRowItems;
                x += (columns - rowItems) * xHalfOffset;
                for (int column = 0; column < rowItems; column++)
                {
                    _sprite.Position.X = x;
                    x += 2 * xHalfOffset;
                    _sprite.Render(context);
                }
            }
        }
    }
}

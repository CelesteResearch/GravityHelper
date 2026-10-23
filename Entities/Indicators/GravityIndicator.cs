// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Display;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Presentation.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using System.Numerics;


namespace Celeste.Mods.GravityHelper.Entities.Indicators
{
    [SceneTracked]
    [CustomEntity("GravityHelper/GravityIndicator = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Entities.GravityIndicator")]
    public class GravityIndicator : SceneEntityState
    {
        private readonly GravityRuntimeContext runtime;

        public static GravityIndicator Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public bool ShowRipples { get; }
        public bool ShowParticles { get; }
        public float BloomAlpha { get; }
        public float BloomRadius { get; }
        public float IdleAlpha { get; }
        public float TurningAlpha { get; }
        public bool SyncToPlayer { get; }
        public GravityType TurnTarget { get; set; }

        private float _turnTime;

        public float TurnTime
        {
            get => _turnTime;
            set
            {
                _turnTime = value;
                var arrowAnimation = GravityIndicatorSpriteAccess.Animations(_arrowSprite)["arrow"];
                arrowAnimation.Delay = value / (arrowAnimation.Frames.Length / 2f);
            }
        }

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        private readonly Sprite _arrowSprite;
        private readonly Sprite _rippleSprite;
        private readonly VertexLightState _vertexLight;
        private readonly BloomPointState _bloomPoint;

        private const int up_arrow_frame = 0;
        private const int down_arrow_frame = 8;

        public GravityIndicator(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            runtime = GravityRuntimeContext.For(engine);
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();

            ShowRipples = data.Bool("showRipples", false);
            ShowParticles = data.Bool("showParticles", true);
            Depth = data.Int("depth", 8500);
            BloomAlpha = data.Float("bloomAlpha", 0.6f);
            BloomRadius = data.Float("bloomRadius", 14f);
            IdleAlpha = Math.Clamp(data.Float("idleAlpha", 1f), 0f, 1f);
            TurningAlpha = Math.Clamp(data.Float("turningAlpha", .4f), 0f, 1f);
            SyncToPlayer = data.Bool("syncToPlayer", false);

            Add(_arrowSprite = runtime.GameSprites.Create("gravityIndicator"));
            _arrowSprite.Rate = 0f;
            _arrowSprite.Play("arrow");
            _arrowSprite.Color = PackedColor.Multiply(PackedColor.White, IdleAlpha);

            TurnTime = Math.Max(data.Float("turnTime", .3f), .1f);

            if (ShowRipples)
                Add(_rippleSprite = runtime.GameSprites.Create("gravityRipple"));

            if (BloomAlpha > 0 && BloomRadius > 0)
            {
                Add(_bloomPoint = new BloomPointState(BloomAlpha, BloomRadius));
                Add(_vertexLight =
                    new VertexLightState(engine, PackedColor.Red, 1f, (int)BloomRadius, (int)BloomRadius));
            }

            Add(new AccessibilityListener(onAccessibilityChange));
            onAccessibilityChange();
        }

        private void onAccessibilityChange()
        {
            _bloomPoint.Alpha = runtime.Settings.RefillBloom ? BloomAlpha : 0f;
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            _arrowSprite.SetAnimationFrame((runtime.PlayerComponent?.ShouldInvert ?? false)
                ? up_arrow_frame
                : down_arrow_frame);
            updateRipple();
        }

        public override void Update()
        {
            if (SyncToPlayer) TurnTarget = runtime.PlayerComponent?.CurrentGravity ?? GravityType.None;

            updateArrow();
            updateRipple();
            updateParticles();

            base.Update();
        }

        private void updateArrow()
        {
            var shouldInvert = TurnTarget == GravityType.Inverted;

            if (shouldInvert && _arrowSprite.CurrentAnimationFrame == up_arrow_frame ||
                !shouldInvert && _arrowSprite.CurrentAnimationFrame == down_arrow_frame)
            {
                _arrowSprite.Rate = 0f;
                _arrowSprite.Color = PackedColor.Multiply(PackedColor.White, IdleAlpha);
            }
            else if (shouldInvert && _arrowSprite.CurrentAnimationFrame < down_arrow_frame ||
                     !shouldInvert && _arrowSprite.CurrentAnimationFrame > down_arrow_frame)
            {
                _arrowSprite.Rate = -1f;
                _arrowSprite.Color = PackedColor.Multiply(PackedColor.White, TurningAlpha);
            }
            else
            {
                _arrowSprite.Rate = 1f;
                _arrowSprite.Color = PackedColor.Multiply(PackedColor.White, TurningAlpha);
            }
        }

        private void updateRipple()
        {
            if (_rippleSprite != null)
            {
                var upArrowFrame = _arrowSprite.CurrentAnimationFrame == up_arrow_frame;
                var downArrowFrame = _arrowSprite.CurrentAnimationFrame == down_arrow_frame;
                float multiplier = upArrowFrame ? 1 : -1;
                const float ripple_offset = 2f;
                _rippleSprite.Position.Y = ripple_offset * -multiplier;
                _rippleSprite.Scale.Y = multiplier;
                _rippleSprite.Color =
                    PackedColor.Multiply(HighlightColor(upArrowFrame ? GravityType.Inverted : GravityType.Normal), .8f);
                _rippleSprite.Visible = upArrowFrame || downArrowFrame;
            }
        }

        private void updateParticles()
        {
            if (!ShowParticles) return;

            if (_arrowSprite.CurrentAnimationFrame == up_arrow_frame ||
                _arrowSprite.CurrentAnimationFrame == down_arrow_frame)
            {
                var scheme = runtime.Settings.GetColorScheme();
                var emitNormal = _arrowSprite.CurrentAnimationFrame == down_arrow_frame;
                _vertexLight.Color =
                    PackedColor.Multiply((emitNormal ? scheme.NormalColor : scheme.InvertedColor).Lighter(), IdleAlpha);

                if (Scene is OriginalLevelSceneRoot level && level.OnInterval(0.1f))
                {
                    var offset = Vector2.UnitY * (emitNormal ? 5f : -5f);
                    var range = Vector2.One * 6f;
                    var direction = Celeste.Core.Mathematics.Vector2Geometry.Angle(Vector2.UnitY) *
                                    (emitNormal ? 1 : -1);
                    var particleType =
                        emitNormal ? scheme.P_GravityRefill_Glow_Normal : scheme.P_GravityRefill_Glow_Inverted;
                    level.ParticlesBG.Emit(particleType, 2, Position + offset, range, direction);
                }
            }
            else
            {
                _vertexLight.Color = PackedColor.Multiply(PackedColor.White, TurningAlpha);
            }
        }

        private PackedColor HighlightColor(GravityType type)
        {
            var color = runtime.Settings.GetColorScheme()[type];
            color.ToHSV(out var hue, out var saturation, out var value);
            return type switch
            {
                GravityType.Normal => ColorExtensions.FromHSV(hue - 29.2f, saturation, value),
                GravityType.Inverted => ColorExtensions.FromHSV(hue - 4.9f, saturation * .891f, value * .863f),
                GravityType.Toggle => ColorExtensions.FromHSV(hue - 14.3f, saturation * .735f, value * .961f),
                _ => PackedColor.White
            };
        }
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Components;
using Celeste.Core.Randomness;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Scenes;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Presentation.Colors;


namespace Celeste.Mods.GravityHelper.Entities.PlayerIndicators
{
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.GravityShieldIndicator")]
    public class GravityShieldIndicator : SceneEntityState
    {
        public float ShieldTotalTime { get; set; }
        public float ShieldTimeRemaining { get; set; }

        private const float flash_time = 1f;
        private bool _flash;

        private const int particle_count = 20;
        private const float radius = 14f;
        private readonly List<Vector2> _particles = [];
        private readonly float[] _speeds = [1.5f, 2f, 2.5f];

        private PackedColor[] _particleColors = [];

        private readonly BloomPointState _bloom;
        private readonly VertexLightState _light;

        private const float bloom_alpha = 0.8f;
        private const float light_alpha = 1f;

        private readonly ISourceRandom _particleRandom;
        private readonly GravityRuntimeContext runtime;

        public GravityShieldIndicator(ISourceEngineContext engine) : this(engine,
            ((ISourceRandomProcessEnvironment)engine).RandomProcess.Factory.CreateUnseeded())
        {
        }

        public GravityShieldIndicator(ISourceEngineContext engine, ISourceRandom random) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            runtime = GravityRuntimeContext.For(engine);
            _particleRandom = random;
            Depth = -1000000;
            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;
            Visible = false;
            Active = false;

            Add(_bloom = new BloomPointState(0f, 16f),
                _light = new VertexLightState(engine, PackedColor.White, 0f, 16, 48));

            using var _ = new GravityIndicatorRandomScope(runtime.Engine, _particleRandom);
            for (int index = 0; index < particle_count; ++index)
            {
                float angle = SourceRandomMath.NextAngle(_particleRandom);
                float length = (radius - 1) * (float)Math.Sqrt(SourceRandomMath.NextFloat(_particleRandom));
                _particles.Add(new Vector2((float)Math.Cos(angle) * length, (float)Math.Sin(angle) * length));
            }

            Add(new AccessibilityListener(onAccessibilityChange));
            onAccessibilityChange();
        }

        private void onAccessibilityChange()
        {
            var colorScheme = runtime.Settings.GetColorScheme();
            colorScheme.NormalColor.ToHSV(out float normalHue, out float normalSaturation, out float normalValue);
            colorScheme.InvertedColor.ToHSV(out float invertedHue, out float invertedSaturation,
                out float invertedValue);
            var blueVioletFromNormal =
                ColorExtensions.FromHSV(normalHue + 31, normalSaturation * 0.81f, normalValue * 0.89f);
            var mediumVioletRedFromInverted =
                ColorExtensions.FromHSV(invertedHue - 38, invertedSaturation * 0.89f, invertedValue * 0.78f);

            _particleColors =
            [
                colorScheme.NormalColor,
                blueVioletFromNormal,
                colorScheme.InvertedColor,
                mediumVioletRedFromInverted
            ];
        }

        public override void Update()
        {
            base.Update();

            using var _ = new GravityIndicatorRandomScope(runtime.Engine, _particleRandom);

            if (ShieldTimeRemaining > 0)
            {
                for (int i = 0; i < _particles.Count; i++)
                {
                    // need to take a random sample every time
                    float angle = SourceRandomMath.NextAngle(_particleRandom);
                    float maxMove = radius * _speeds[i % _speeds.Length];
                    _particles[i] = ((ISourceVectorEnvironment)runtime.Engine).VectorArithmetic.Approach(_particles[i],
                        Vector2.Zero, maxMove * runtime.Engine.DeltaTime);
                    if (((ISourceVectorEnvironment)runtime.Engine).VectorArithmetic.LengthSquared(_particles[i]) <
                        8 * 8)
                    {
                        const float length = radius - 1;
                        _particles[i] = new Vector2(length * (float)Math.Cos(angle), length * (float)Math.Sin(angle));
                    }
                }

                ShieldTimeRemaining -= runtime.Engine.DeltaTime;
                if (ShieldTimeRemaining <= 0)
                    Deactivate();
            }

            if (ShieldTimeRemaining <= flash_time)
            {
                if (((SourceSceneState)Scene).OnInterval(0.1f)) _flash = !_flash;
                float amount =
                    ((ISourceEasingEnvironment)runtime.Engine).EasingArithmetic.QuintOut(ShieldTimeRemaining /
                        flash_time);
                _light.Alpha = light_alpha * amount;
                _bloom.Alpha = bloom_alpha * amount;
            }
        }


        public override void Render(ISourceRenderContext context)
        {
            if ((Scene as ISourceScene)?.SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>() is not
                { } player) return;

            if (!_flash)
            {
                var offset = (runtime.PlayerComponent?.ShouldInvert ?? false) ? 4f : -4f;
                var origin = Vector2Geometry.Round(player.Center + Vector2.UnitY * offset);

                Position = Vector2Geometry.Round(player.Center + Vector2.UnitY * offset);

                for (var i = 0; i < _particles.Count; i++)
                {
                    var part = Vector2Geometry.Round(_particles[i]);
                    var color = _particleColors[i % _particleColors.Length];
                    SourcePrimitiveDrawing.Point(context, this, origin + part, color * 0.7f);
                }

                SourcePrimitiveDrawing.Circle(context, this, origin, radius, PackedColor.Violet * 0.7f, 3,
                    ((ISourceVectorEnvironment)runtime.Engine).VectorArithmetic);
            }
        }

        public void Activate(float time)
        {
            ShieldTimeRemaining = ShieldTotalTime = time;

            // only lock if we weren't already active to prevent
            // multiple shields getting us in a bad semaphore state
            if (!Active) runtime.PlayerComponent?.Lock();

            Active = Visible = true;
            _flash = false;

            _bloom.Alpha = bloom_alpha;
            _light.Alpha = light_alpha;
        }

        public void Deactivate()
        {
            ShieldTimeRemaining = ShieldTotalTime = 0;
            Active = Visible = false;
            runtime.PlayerComponent?.Unlock();

            _bloom.Alpha = 0f;
            _light.Alpha = 0f;
        }
    }
}

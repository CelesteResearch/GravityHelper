using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Graphics;
using Celeste.Game.Settings;
using Celeste.Graphics.Content;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Game.Presentation.Rendering;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Effects;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Runtime;

namespace Celeste.Mods.GravityHelper.Presentation.Shaders
{
    public sealed class GravityTintShader
    {
        private static readonly ConditionalWeakTable<GravityRuntimeContext, GravityTintShader> Owners = new();

        public static GravityTintShader For(GravityRuntimeContext runtime) =>
            Owners.GetValue(runtime, static owner => new GravityTintShader(owner));

        private readonly GravityRuntimeContext runtime;
        private bool effectLoaded;
        private SourceEffect tintEffect;
        private GravityTintShader(GravityRuntimeContext runtime) => this.runtime = runtime;

        private bool CanBegin(bool onlyForAccessibility)
        {
            if (onlyForAccessibility && runtime.Settings.ColorSchemeType == ColorSchemeSetting.Default) return false;
            if (!effectLoaded)
            {
                effectLoaded = true;
                if (((IEverestContentRegistryEnvironment)runtime.Engine).SourceContentRegistry.TryGet(
                        "Effects/GravityHelper/CustomTintShader.cso", out var metadata))
                    tintEffect = new SourceEffect(((ISourceGraphicsDeviceEnvironment)runtime.Engine).GraphicsDevice,
                        metadata.Data);
                else
                    ((IEverestLoggerProcessEnvironment)runtime.Engine).Logger.Warn("GravityHelper",
                        "Couldn't find custom tint shader");
            }

            return tintEffect != null;
        }

        private void BeginLoaded(ISourceRenderContext context)
        {
            GameplayRendererState.End(context);
            if (runtime.Engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
            {
                var parameters = tintEffect.Parameters;
                parameters["DeltaTime"]?.SetValue(runtime.Engine.DeltaTime);
                parameters["Time"]?.SetValue(level.TimeActive);
                var gameplay =
                    ((OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)runtime.Engine).GameGraphics)
                    .Buffers
                    .Gameplay;
                parameters["Dimensions"]?.SetValue(new Vector2(gameplay.Width, gameplay.Height));
                parameters["CamPos"]?.SetValue(level.Camera.Position);
                parameters["ColdCoreMode"]?.SetValue(level.CoreMode == RoomCoreMode.Cold);
                parameters["Photosensitive"]?.SetValue(((ISettingsContext)runtime.Engine).Settings.DisableFlashes);
                var viewport = ((ISourceGraphicsDeviceEnvironment)runtime.Engine).GraphicsDevice.Viewport;
                parameters["TransformMatrix"]
                    ?.SetValue(Matrix4x4.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1));
                parameters["ViewMatrix"]?.SetValue(Matrix4x4.Identity);
                context.BeginBatch(new SourceRenderBatchState(SourceSpriteSortMode.Deferred,
                    SourceBlendState.AlphaBlend,
                    SourceSamplerState.PointWrap, SourceDepthStencilState.None, SourceRasterizerState.CullNone,
                    tintEffect,
                    level.Camera.Matrix));
            }
            else throw new InvalidOperationException("GravityHelper custom tint requires the actual gameplay Level.");
        }

        public bool TryBegin(ISourceRenderContext context, bool onlyForAccessibility = true)
        {
            if (!CanBegin(onlyForAccessibility)) return false;
            BeginLoaded(context);
            return true;
        }

        public bool TryBegin(bool onlyForAccessibility = true)
        {
            if (!CanBegin(onlyForAccessibility)) return false;
            BeginLoaded(((ISourceRenderEnvironment)runtime.Engine).CurrentContext);
            return true;
        }

        public void End(ISourceRenderContext context)
        {
            context.EndBatch();
            GameplayRendererState.Begin(context);
        }

        public TintScope Enter(ISourceRenderContext context, bool onlyForAccessibility = true)
            => TryBegin(context, onlyForAccessibility) ? new TintScope(this, context) : default;

        public void Clear()
        {
            tintEffect = null;
            effectLoaded = false;
        }

        public readonly struct TintScope : IDisposable
        {
            private readonly GravityTintShader owner;
            private readonly ISourceRenderContext context;

            internal TintScope(GravityTintShader owner, ISourceRenderContext context)
            {
                this.owner = owner;
                this.context = context;
            }

            public void Dispose() => owner?.End(context);
        }
    }
}

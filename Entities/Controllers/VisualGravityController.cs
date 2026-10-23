// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Scene.Tracking.Attributes;
using System.Numerics;


namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    [CustomEntity("GravityHelper/VisualGravityController = Create")]
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.Controllers.VisualGravityController")]
    public class VisualGravityController : BaseGravityController<VisualGravityController>
    {
        public static VisualGravityController Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public float FieldArrowOpacity { get; } = 0.5f;
        public float FieldBackgroundOpacity { get; } = 0.15f;
        public float FieldParticleOpacity { get; } = 0.5f;
        public string FieldNormalColor { get; } = "0000FF";
        public string FieldInvertedColor { get; } = "FF0000";
        public string FieldToggleColor { get; } = "800080";
        public string FieldArrowColor { get; } = "FFFFFF";
        public string FieldParticleColor { get; } = "FFFFFF";
        public bool FieldFlashOnTrigger { get; } = true;
        public bool FieldShowParticles { get; } = true;
        public int FieldParticleDensity { get; } = 4;
        public float LineMinAlpha { get; } = 0.45f;
        public float LineMaxAlpha { get; } = 0.95f;
        public float LineFlashTime { get; } = 0.35f;
        public string LineColor { get; } = "FFFFFF";
        public float LineThickness { get; } = 2f;

        // ReSharper disable once UnusedMember.Global
        public VisualGravityController(ISourceEngineContext engine) : base(engine)
        {
            // ephemeral controller
        }

        // ReSharper disable once UnusedMember.Global
        public VisualGravityController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
            FieldArrowOpacity = data.Float("fieldArrowOpacity", FieldArrowOpacity);
            FieldBackgroundOpacity = data.Float("fieldBackgroundOpacity", FieldBackgroundOpacity);
            FieldParticleOpacity = data.Float("fieldParticleOpacity", FieldParticleOpacity);
            FieldNormalColor = data.Attr("fieldNormalColor", FieldNormalColor);
            FieldInvertedColor = data.Attr("fieldInvertedColor", FieldInvertedColor);
            FieldToggleColor = data.Attr("fieldToggleColor", FieldToggleColor);
            FieldArrowColor = data.Attr("fieldArrowColor", FieldArrowColor);
            FieldParticleColor = data.Attr("fieldParticleColor", FieldParticleColor);
            FieldFlashOnTrigger = data.Bool("fieldFlashOnTrigger", FieldFlashOnTrigger);
            FieldShowParticles = data.Bool("fieldShowParticles", FieldShowParticles);
            FieldParticleDensity = data.Int("fieldParticleDensity", FieldParticleDensity);
            LineMinAlpha = data.Float("lineMinAlpha", LineMinAlpha);
            LineMaxAlpha = data.Float("lineMaxAlpha", LineMaxAlpha);
            LineFlashTime = data.Float("lineFlashTime", LineFlashTime);
            LineColor = data.Attr("lineColor", LineColor);
            LineThickness = data.Float("lineThickness", LineThickness);
        }
    }
}

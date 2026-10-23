using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.GravityHelper.Metadata;
using Celeste.Mods.GravityHelper.Presentation;
using Celeste.Game.Components.Talking;
using Celeste.Game.Entities.Lookouts;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Graphics.Animation;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.GravityHelper.Entities.WatchTowers
{
    [Celeste.Everest.Modules.Entities.Metadata.CustomEntity("GravityHelper/UpsideDownWatchTower = Create")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Entities.UpsideDownWatchTower")]
    [Celeste.Everest.Tracking.Attributes.EverestTrackedAs(typeof(OriginalLookoutState))]
    public class UpsideDownWatchTower : Celeste.Everest.Gameplay.Lookouts.EverestLookoutState
    {
        public static UpsideDownWatchTower Create(Celeste.Game.Execution.Levels.OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset,
            Celeste.Game.Content.Maps.Entities.OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        private static readonly string[] prefixes = ["", "badeline_", "nobackpack_"];

        private static readonly string[][] pairs =
        [
            ["lookingUp", "lookingDown"],
            ["lookingUpRight", "lookingDownRight"],
            ["lookingUpLeft", "lookingDownLeft"]
        ];

        // ReSharper disable NotAccessedField.Local
        private readonly VersionInfo _modVersion;
        private readonly VersionInfo _pluginVersion;
        // ReSharper restore NotAccessedField.Local

        private readonly string _textureDirectory;
        private bool _addedUI;

        public UpsideDownWatchTower(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(data, offset, (IOriginalLookoutProcess)engine)
        {
            var sprite = Get<Sprite>();
            _modVersion = data.ModVersion();
            _pluginVersion = data.PluginVersion();
            _textureDirectory = data.Attr("textureDirectory").Trim();

            Collider.TopCenter = -Collider.BottomCenter;

            var talkComponent = Get<OriginalTalkComponent>();
            talkComponent.Bounds.Y = -talkComponent.Bounds.Bottom;
            talkComponent.DrawAt.Y *= -1;

            var vertexLight = Get<VertexLightState>();
            vertexLight.Y *= -1;

            if (!string.IsNullOrWhiteSpace(_textureDirectory))
            {
                var onFrameChange = sprite.OnFrameChange;
                GravitySpriteTemplates.CreateOnWithPath(engine, sprite, "lookout", _textureDirectory);
                sprite.OnFrameChange = onFrameChange;
            }

            sprite.Position.Y *= -1;
            sprite.Scale.Y *= -1;

            var animations = sprite.Animations;
            bool failedKey = false;

            foreach (var prefix in prefixes)
            {
                foreach (var pair in pairs)
                {
                    var upAnimString = prefix + pair[0];
                    var downAnimString = prefix + pair[1];

                    if (!animations.TryGetValue(upAnimString, out var upAnim))
                    {
                        System.Diagnostics.Trace.TraceWarning($"Couldn't find up animation {upAnimString}");
                        failedKey = true;
                        continue;
                    }

                    if (!animations.TryGetValue(upAnimString, out var downAnim))
                    {
                        System.Diagnostics.Trace.TraceWarning($"Couldn't find down animation {downAnimString}");
                        failedKey = true;
                        continue;
                    }

                    animations[upAnimString] = downAnim;
                    animations[downAnimString] = upAnim;
                }
            }

            // if we failed getting any animation key, dump out the available ones
            if (failedKey)
            {
                var animKeys = string.Join(",", animations.Keys);
                System.Diagnostics.Trace.TraceWarning($"WatchTower animations available: {animKeys}");
            }
        }

        public override void Update()
        {
            if (!_addedUI)
            {
                var talkComponent = Get<OriginalTalkComponent>();
                if (talkComponent.UI == null)
                {
                    _addedUI = true;
                    ((ISourceScene)Scene).SourceScene.QueueSourceEntity(talkComponent.UI =
                        new UpsideDownTalkComponentUI(talkComponent));
                }
            }

            base.Update();
        }
    }
}

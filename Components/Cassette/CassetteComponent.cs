using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Maps;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.GravityHelper.Components.Cassette
{
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Components.CassetteComponent")]
    public class CassetteComponent : SourceComponentState
    {
        public bool Enabled { get; set; } = true;
        public int CassetteIndex { get; }
        public System.Action<CassetteStates> OnStateChange;
        private CassetteStates _cassetteState = CassetteStates.Off;

        public CassetteStates CassetteState
        {
            get => _cassetteState;
            set
            {
                _cassetteState = value;
                OnStateChange?.Invoke(value);
            }
        }

        private MapEntityId? _entityId;

        public CassetteComponent(int cassetteIndex, MapEntityId? id = null) : base(true, false)
        {
            CassetteIndex = cassetteIndex;
            _entityId = id;
        }

        public CassetteComponent(int cassetteIndex, OriginalLevelEntityData data) : this(cassetteIndex,
            new MapEntityId(data.Level.Name, data.ID))
        {
        }

        public void TrySetActivatedSilently(int currentIndex)
        {
            if (_entityId != null && _entityId.Value.Level == ((OriginalLevelSceneRoot)Scene).Session.Level)
                CassetteState = CassetteIndex == currentIndex ? CassetteStates.On : CassetteStates.Off;
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            if (scene is not OriginalLevelSceneRoot level) return;
            level.HasCassetteBlocks = true;
            EnsureManager(level);
        }

        internal static void EnsureManager(OriginalLevelSceneRoot level)
        {
            var existing = level.Scheduler.Entities.Entities;
            for (int i = 0; i < existing.Count; i++)
                if (existing[i] is OriginalCassetteBlockManagerState)
                    return;
            var adding = level.Scheduler.GetToAdd();
            for (int i = 0; i < adding.Count; i++)
                if (adding[i] is OriginalCassetteBlockManagerState)
                    return;
            level.Scheduler.QueueSourceEntity(((IOriginalCassetteManagerEnvironment)level.EngineContext)
                .CassetteManagers
                .Create());
        }
    }
}

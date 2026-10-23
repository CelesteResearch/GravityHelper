using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using System.Runtime.CompilerServices;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using System.Collections.Generic;

namespace Celeste.Mods.GravityHelper.Runtime
{
    public sealed class GravityRuntimeContext
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, GravityRuntimeContext> Contexts = new();

        public static GravityRuntimeContext For(ISourceEngineContext engine)
            => Contexts.GetValue(engine, static owner => new GravityRuntimeContext(owner));

        public GravityRuntimeContext(ISourceEngineContext engine) => Engine = engine;
        public ISourceEngineContext Engine { get; }
        public GravityComponent PlayerComponent;
        public IGravityJumpThruContactQueries JumpThruContacts { get; set; }
        public int ForceInvertPlayerRenderSemaphore;
        public GravityOverrideState Overrides { get; } = new();
        public GravityHelperSession Session { get; set; } = new();
        public GravityChaserHistory ChaserHistory { get; } = new();
        public HashSet<string> ModSupportBlacklist { get; } = [];
        public GravityType? GravityBeforeReload;
        public GravityHelperSettings Settings { get; set; } = new();
        public ISpriteFactory GameSprites => ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.SpriteFactory;

        public void ClearStatics()
        {
            Overrides.Semaphore = 0;
            PlayerComponent = null;
        }
    }
}

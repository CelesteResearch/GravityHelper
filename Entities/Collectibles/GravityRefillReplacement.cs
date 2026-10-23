// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System.Linq;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Mods.GravityHelper.Runtime;

namespace Celeste.Mods.GravityHelper.Entities.Collectibles
{
    public static class GravityRefillReplacement
    {
        public static void Replace(OriginalLevelSceneRoot level)
        {
            var engine = level.EngineContext;
            if (!GravityRuntimeContext.For(engine).Settings.ReplaceRefills) return;
            // Only exact vanilla instances are replaced. Already custom/derived refills stay untouched.
            var refills = level.SourceScene.Entities.Entities
                .Where(entity => entity.GetType() == typeof(OriginalRefillState)).Cast<OriginalRefillState>().ToList();
            foreach (var refill in refills)
            {
                var replacement = new GravityRefill(engine, refill.Position, refill.TwoDashes, refill.OneUse);
                level.SourceScene.RemoveEntity(refill);
                level.SourceScene.QueueSourceEntity(replacement);
            }
        }
    }
}

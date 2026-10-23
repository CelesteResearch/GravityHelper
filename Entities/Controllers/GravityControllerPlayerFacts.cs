using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Maps.Processing;
using Celeste.Game.Content.Maps;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    internal static class GravityControllerPlayerFacts
    {
        internal static bool IsIntroState(RoomPlayerSession player)
        {
            int state = player.Machine.State;
            return state is >= 12 and <= 15 or 23 or 25;
        }

        internal static bool CheckPlatform(RoomPlayerSession player, System.Numerics.Vector2 at, bool solidTiles)
        {
            var all = ((Celeste.Scene.Tracking.ISourceScene)player.Scene).SourceScene.Entities.Entities;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is not SourcePlatformEntityState platform) continue;
                bool tiles = platform is ISourceSolidTilesEntity;
                if (tiles != solidTiles) continue;
                if (SourceEntityCollision.CheckAt(player, platform, at)) return true;
            }

            return false;
        }

        internal static string InventoryName(ISourceEngineContext engine, OriginalLevelSceneRoot level) =>
            level.Session.MapData is OriginalChapterMapData map
                ? ((IEverestChapterMapProcessingEnvironment)engine).MapProcessing.GetModeMeta(map)?.Inventory ??
                  string.Empty
                : string.Empty;
    }
}

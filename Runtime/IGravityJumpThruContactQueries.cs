using System.Numerics;
using Celeste.Physics.Collision.Riding;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Runtime
{
    public interface IGravityJumpThruContactQueries
    {
        bool IsUpsideDown(ISourceJumpThruCollider platform);
        SourceJumpThruEntityState First(SceneEntityState entity, Vector2 at, bool inverted, bool outside = false);
    }
}

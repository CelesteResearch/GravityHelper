using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Game.Gameplay.Platforms.CrushBlocks;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.GravityHelper.Interop
{
    internal static class MovingBlockSourceLoans
    {
        internal static readonly Func<SourcePlatformEntityState, ISourceSolidWorld> World =
            Field<SourcePlatformEntityState, ISourceSolidWorld>("SourceWorld");

        internal static readonly Func<MovingBlockState, List<SpriteImage>> Body =
            Field<MovingBlockState, List<SpriteImage>>("body");

        internal static readonly Func<MovingBlockState, RoomPlayerSession> NoSquish =
            Field<MovingBlockState, RoomPlayerSession>("noSquish");

        internal static readonly Func<OriginalCrushBlockState, ISourceEngineContext> CrushEngine =
            Field<OriginalCrushBlockState, ISourceEngineContext>("engine");

        internal static readonly Action<MovingBlockState, AtlasEntry, Vector2, float, Vector2, List<SpriteImage>>
            AddImage = BindAddImage();

        private static Func<T, TValue> Field<T, TValue>(string name)
        {
            var self = Expression.Parameter(typeof(T));
            return Expression
                .Lambda<Func<T, TValue>>(
                    Expression.Field(self, typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)),
                    self).Compile();
        }

        private static Action<MovingBlockState, AtlasEntry, Vector2, float, Vector2, List<SpriteImage>> BindAddImage()
        {
            var types = new[]
            {
                typeof(MovingBlockState), typeof(AtlasEntry), typeof(Vector2), typeof(float), typeof(Vector2),
                typeof(List<SpriteImage>)
            };
            var parameters = new ParameterExpression[types.Length];
            for (int i = 0; i < types.Length; i++) parameters[i] = Expression.Parameter(types[i]);
            var args = new Expression[types.Length - 1];
            Array.Copy(parameters, 1, args, 0, args.Length);
            var method = typeof(MovingBlockState).GetMethod("AddImage", BindingFlags.Instance | BindingFlags.NonPublic);
            return Expression
                .Lambda<Action<MovingBlockState, AtlasEntry, Vector2, float, Vector2, List<SpriteImage>>>(
                    Expression.Call(parameters[0], method, args), parameters).Compile();
        }
    }
}

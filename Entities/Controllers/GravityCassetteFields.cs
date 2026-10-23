using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Game.Gameplay.Platforms.Cassette;

namespace Celeste.Mods.GravityHelper.Entities.Controllers
{
    internal static class GravityCassetteFields
    {
        internal static readonly Func<OriginalCassetteBlockManagerState, float> Tempo = Getter<float>("tempoMult");

        internal static readonly Func<OriginalCassetteBlockManagerState, int>
            CurrentIndex = Getter<int>("currentIndex");

        private static Func<OriginalCassetteBlockManagerState, T> Getter<T>(string name)
        {
            var owner = typeof(OriginalCassetteBlockManagerState);
            var m = new DynamicMethod(name, typeof(T), new[] { owner }, owner, true);
            var il = m.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!);
            il.Emit(OpCodes.Ret);
            return (Func<OriginalCassetteBlockManagerState, T>)m.CreateDelegate(
                typeof(Func<OriginalCassetteBlockManagerState, T>));
        }
    }
}

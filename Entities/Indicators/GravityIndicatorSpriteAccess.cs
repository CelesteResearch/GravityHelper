using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Celeste.Graphics.Animation;

namespace Celeste.Mods.GravityHelper.Entities.Indicators
{
    internal static class GravityIndicatorSpriteAccess
    {
        internal static readonly Func<Sprite, Dictionary<string, Sprite.Animation>> Animations = Create();

        private static Func<Sprite, Dictionary<string, Sprite.Animation>> Create()
        {
            var t = typeof(Dictionary<string, Sprite.Animation>);
            var m = new DynamicMethod("GravityIndicatorAnimations", t, new[] { typeof(Sprite) }, typeof(Sprite), true);
            var il = m.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(Sprite).GetField("animations",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!);
            il.Emit(OpCodes.Ret);
            return (Func<Sprite, Dictionary<string, Sprite.Animation>>)m.CreateDelegate(
                typeof(Func<Sprite, Dictionary<string, Sprite.Animation>>));
        }
    }
}

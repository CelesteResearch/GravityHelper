// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps;

namespace Celeste.Mods.GravityHelper.Activation
{
    public static class GravityHookActivationPolicy
    {
        private static bool RequiresHooks(EntityData data)
        {
            if (data.Name is "GravityHelper/MomentumSpring" or "GravityHelper/CeilingMomentumSpring") return false;
            return data.Name.StartsWith("GravityHelper") || data.Has("_gravityHelper");
        }

        public static bool RequiresHooksForMap(OriginalChapterMapData map, out bool renderOnly)
        {
            // The source tests the first matching entity, not whether every matching entity is a controller.
            foreach (var room in map.Levels)
            foreach (var data in room.Entities)
                if (RequiresHooks(data))
                {
                    renderOnly = data.Name == "GravityHelper/ForceLoadGravityController";
                    return true;
                }

            renderOnly = false;
            foreach (var room in map.Levels)
            foreach (var data in room.Triggers)
                if (RequiresHooks(data))
                    return true;
            return false;
        }

        public static GravityHookLevel Select(OriginalChapterMapData map, bool allowInAllMaps)
        {
            bool requiresHooks = RequiresHooksForMap(map, out bool renderOnly);
            if (allowInAllMaps) return requiresHooks ? GravityHookLevel.GravityHelperMap : GravityHookLevel.Forced;
            return requiresHooks
                ? renderOnly ? GravityHookLevel.RenderOnly : GravityHookLevel.GravityHelperMap
                : GravityHookLevel.None;
        }
    }
}

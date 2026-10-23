using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;

namespace Celeste.Mods.GravityHelper.Metadata
{
    public static class GravityEntityAttributes
    {
        public static VersionInfo ModVersion(this EntityData d) =>
            VersionInfo.TryParse(d.Attr("modVersion"), out var v) ? v : new VersionInfo(0, 1);

        public static VersionInfo PluginVersion(this EntityData d) =>
            VersionInfo.TryParse(d.Attr("pluginVersion"), out var v) ? v : new VersionInfo(0, 1);
    }
}

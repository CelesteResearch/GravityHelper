namespace Celeste.Mods.GravityHelper.Configuration
{
    public interface IGravityColorSchemeResolver
    {
        IGravityColorScheme Resolve(ColorSchemeSetting setting);
    }
}

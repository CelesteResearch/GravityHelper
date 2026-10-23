// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

namespace Celeste.Mods.GravityHelper.Configuration
{
    public sealed partial class GravityHelperSettings
    {
        public void MigrateIfRequired()
        {
            if (SettingsVersion >= LatestSettingsVersion) return;
            SettingsVersion = LatestSettingsVersion;
        }
    }
}

// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System.Collections.Generic;
using Celeste.Mods.GravityHelper.Gravity;

namespace Celeste.Mods.GravityHelper.Runtime
{
    public sealed class GravityChaserHistory
    {
        private readonly Dictionary<int, GravityType> states = [];

        public GravityType GravityTypeForState(float timestamp, GravityType defaultValue = GravityType.Normal)
            => states.TryGetValue((int)(timestamp * 1000), out var value) ? value : defaultValue;

        public bool TryGravityTypeForState(float timestamp, out GravityType value)
            => states.TryGetValue((int)(timestamp * 1000), out value);

        public void SetGravityTypeForState(float timestamp, GravityType value)
            => states[(int)(timestamp * 1000)] = value;

        public void RemoveGravityTypeForState(float timestamp) => states.Remove((int)(timestamp * 1000));
        public void Clear() => states.Clear();
    }
}

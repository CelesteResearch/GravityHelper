// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;

namespace Celeste.Mods.GravityHelper.Runtime
{
    internal readonly struct InvokeOnDispose : IDisposable
    {
        private readonly Action action;
        public InvokeOnDispose(Action action) => this.action = action;
        public void Dispose() => action?.Invoke();
    }
}

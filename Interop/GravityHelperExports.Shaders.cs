// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Rendering;
using Celeste.Mods.GravityHelper.Presentation.Shaders;
using Celeste.Mods.GravityHelper.Runtime;

namespace Celeste.Mods.GravityHelper.Interop
{
    public sealed partial class GravityHelperExports
    {
        public bool BeginCustomTintShader(bool onlyForAccessibility = true)
            => GravityTintShader.For(runtime).TryBegin(onlyForAccessibility);

        public void EndCustomTintShader()
            => GravityTintShader.For(runtime).End(((ISourceRenderEnvironment)runtime.Engine).CurrentContext);

        public IDisposable WithCustomTintShader(bool onlyForAccessibility = true)
            => BeginCustomTintShader(onlyForAccessibility)
                ? new InvokeOnDispose(EndCustomTintShader)
                : new InvokeOnDispose(null);
    }
}

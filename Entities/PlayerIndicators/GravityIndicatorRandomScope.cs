using System;
using Celeste.Core.Randomness;
using Celeste.Core.Scheduling;

namespace Celeste.Mods.GravityHelper.Entities.PlayerIndicators
{
    internal readonly struct GravityIndicatorRandomScope : IDisposable
    {
        private readonly SourceRandomProcessState process;

        public GravityIndicatorRandomScope(ISourceEngineContext engine, ISourceRandom random)
        {
            process = ((ISourceRandomProcessEnvironment)engine).RandomProcess;
            process.PushRandom(random);
        }

        public void Dispose() => process.PopRandom();
    }
}

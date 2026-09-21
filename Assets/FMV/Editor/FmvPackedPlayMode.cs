using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace FmvDemo.Editor
{
    public sealed class FmvPackedPlayMode : BuildScriptPackedPlayMode
    {
        public override string Name => "FMV - Existing Build (checks freshness)";
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput input)
        {
            foreach (var step in FmvContentPipeline.Steps) step.RefineData();
            FmvContentPipeline.RequireCurrentBuild();
            return base.BuildDataImplementation<TResult>(input);
        }
    }
}

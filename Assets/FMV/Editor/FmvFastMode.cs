using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace FmvDemo.Editor
{
    public sealed class FmvFastMode : BuildScriptFastMode
    {
        public override string Name => "FMV - Asset Database (automatic registration)";
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput input)
        {
            foreach (var step in FmvContentPipeline.Steps) step.RefineData();
            return base.BuildDataImplementation<TResult>(input);
        }
    }
}

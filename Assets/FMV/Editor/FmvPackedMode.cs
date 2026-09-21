using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;

namespace FmvDemo.Editor
{
    public sealed class FmvPackedMode : BuildScriptPackedMode
    {
        public override string Name => "FMV - Build Content (automatic registration)";
        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput input)
        {
            foreach (var step in FmvContentPipeline.Steps) step.RefineData();
            var result = base.BuildDataImplementation<TResult>(input);
            if (string.IsNullOrEmpty(result.Error)) FmvContentPipeline.SaveReceipt(input.Registry.GetFilePaths());
            return result;
        }
    }
}

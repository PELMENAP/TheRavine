using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class ItemPipelineBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        ItemPipelineValidator.Report result = ItemPipelineValidator.Run(false);
        if (result.Errors > 0)
            throw new BuildFailedException($"[ItemValidator] {result.Errors} errors in item/object data, see console");
    }
}

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IOSPostBuildFix
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        if (!File.Exists(projectPath))
        {
            return;
        }

        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        RemoveBadLinkerFlag(project, mainTargetGuid);
        RemoveBadLinkerFlag(project, frameworkTargetGuid);

        project.WriteToFile(projectPath);
    }

    static void RemoveBadLinkerFlag(PBXProject project, string targetGuid)
    {
        if (string.IsNullOrEmpty(targetGuid))
        {
            return;
        }

        project.UpdateBuildProperty(
            targetGuid,
            "OTHER_LDFLAGS",
            new string[0],
            new[] { "-ld64" });
    }
}
#endif

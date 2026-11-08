using System;
using System.IO;
using UnityEditor;

// Unity doesn't always notice when a script changes on disk, which leaves you
// playing an old version of the game. So every couple of seconds, while the
// game isn't running, we look for scripts newer than the last build ourselves.
[InitializeOnLoad]
static class ReloadChangedScripts
{
    const string Build = "Library/ScriptAssemblies/Assembly-CSharp.dll";

    static double nextCheck;
    static DateTime alreadyTried;   // so a script with an error isn't retried forever

    static ReloadChangedScripts()
    {
        EditorApplication.update += Check;
    }

    static void Check()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2.0;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        DateTime newest = DateTime.MinValue;
        foreach (string script in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            DateTime changed = File.GetLastWriteTime(script);
            if (changed > newest) newest = changed;
        }

        if (newest > File.GetLastWriteTime(Build) && newest != alreadyTried)
        {
            alreadyTried = newest;
            AssetDatabase.Refresh();
        }
    }
}

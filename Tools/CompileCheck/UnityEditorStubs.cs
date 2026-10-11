// ============================================================================
//  UnityEditor 桩（编译体检用，只覆盖 Assets\Editor\ProjectSetup.cs 用到的部分）
// ============================================================================
using System;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public static class EditorApplication
    {
        public static void Exit(int code) { }
        public static bool isPlaying { get { return false; } }
        public static bool isPlayingOrWillChangePlaymode { get { return false; } }
        public static bool isCompiling { get { return false; } }
        public static bool isUpdating { get { return false; } }
        public static void ExecuteMenuItem(string name) { }
    }

    public static class AssetDatabase
    {
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void CreateAsset(UnityEngine.Object o, string path) { }
        public static string CreateFolder(string parent, string name) { return ""; }
        public static string GetAssetPath(UnityEngine.Object o) { return ""; }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object { return null; }
    }

    public class EditorBuildSettingsScene
    {
        public string path { get; set; }
        public bool enabled { get; set; }
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public EditorBuildSettingsScene() { }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
        public static bool DisplayDialog(string title, string message, string ok) { return true; }
        public static void ClearProgressBar() { }
    }

    public static class Selection
    {
        public static UnityEngine.GameObject activeGameObject { get { return null; } }
        public static UnityEngine.Object activeObject { get { return null; } }
    }

    public static class Undo
    {
        public static void RecordObject(UnityEngine.Object o, string name) { }
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup) { return new UnityEngine.SceneManagement.Scene(); }
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) { return new UnityEngine.SceneManagement.Scene(); }
        public static UnityEngine.SceneManagement.Scene OpenScene(string path) { return new UnityEngine.SceneManagement.Scene(); }
        public static UnityEngine.SceneManagement.Scene OpenScene(string path, OpenSceneMode mode) { return new UnityEngine.SceneManagement.Scene(); }
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene) { return true; }
        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) { return true; }
        public static void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene) { }
        public static bool SaveOpenScenes() { return true; }
    }
}
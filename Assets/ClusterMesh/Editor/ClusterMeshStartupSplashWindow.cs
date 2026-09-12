using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshStartupSplashWindow : EditorWindow
    {
        public const string PromoImagePath = "Assets/ClusterMesh/ClusterMesh-promo-512.png";
        public const string SessionShownKey = "ClusterMesh.StartupSplash.Shown";
        public const int ImageSize = 512;

        const int Pad = 12;
        const int ButtonRow = 36;

        Texture2D _promo;

        [InitializeOnLoadMethod]
        static void ShowOnEditorLaunch()
        {
            if (!ShouldShowOnLaunch())
                return;
            EditorApplication.delayCall += TryShowOnLaunch;
        }

        public static bool ShouldShowOnLaunch()
        {
            if (Application.isBatchMode)
                return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            return !SessionState.GetBool(SessionShownKey, false);
        }

        static void TryShowOnLaunch()
        {
            if (!ShouldShowOnLaunch())
                return;
            ShowSplash();
        }

        [MenuItem("Tools/ClusterMesh/关于 ClusterMesh", priority = 100)]
        public static void ShowSplash()
        {
            SessionState.SetBool(SessionShownKey, true);
            var window = CreateInstance<ClusterMeshStartupSplashWindow>();
            window.titleContent = new GUIContent("ClusterMesh");
            float width = ImageSize + Pad * 2;
            float height = ImageSize + Pad * 2 + ButtonRow;
            window.minSize = window.maxSize = new Vector2(width, height);
            window.ShowUtility();
            window.Focus();
            window.LoadPromo();
        }

        void OnEnable()
        {
            LoadPromo();
        }

        void LoadPromo()
        {
            _promo = AssetDatabase.LoadAssetAtPath<Texture2D>(PromoImagePath);
        }

        void OnGUI()
        {
            if (_promo == null)
                LoadPromo();

            var imageRect = new Rect(Pad, Pad, ImageSize, ImageSize);
            if (_promo != null)
                GUI.DrawTexture(imageRect, _promo, ScaleMode.ScaleToFit, true);
            else
                EditorGUI.HelpBox(imageRect, "找不到宣传图：" + PromoImagePath, MessageType.Warning);

            if (GUI.Button(new Rect(Pad, Pad + ImageSize + 8, ImageSize, 24), "关闭"))
                Close();
        }
    }
}

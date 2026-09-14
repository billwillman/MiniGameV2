using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Reflection;

namespace FoW
{
    // This hack is required to call OnCompileScripts AFTER the AssetDatabase has refreshed (there doesn't seem to be any other way to do this)
    class FogOfWarSetup_CallCompileScriptsHack : AssetPostprocessor
    {
#if UNITY_2020_1_OR_NEWER
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
#else
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
#endif
        {
            FogOfWarSetup.OnCompileScripts();
        }
    }

    public class FogOfWarSetup : EditorWindow
    {
        string _fogOfWarPath = null;
        Vector2 _scroll = Vector2.zero;
        bool _hasInstalled = false;
        GUIStyle _messageStyle = null;
        GUIStyle _headerStyle = null;

        const string _defaultFogOfWarPath = "Assets/FogOfWar/";
        const string _lastInstalledID = "FogOfWarSetup_LastInstalledID";

        public const string rendererLegacyID = "Legacy";
        public const string rendererPPSv2ID = "PPSv2";
        public const string rendererURPID = "URP";
        public const string rendererHDRPID = "HDRP";

        public static string[] rendererIDs = new string[]
        {
            rendererLegacyID,
            rendererPPSv2ID,
            rendererURPID,
            rendererHDRPID
        };

        public static string GetFogOfWarPath()
        {
            string[] guids = AssetDatabase.FindAssets("t:script FogOfWarSetup");
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return path.Substring(0, path.LastIndexOf("/Editor/")) + "/";
        }

        void OnGUI()
        {
            if (_fogOfWarPath == null)
                _fogOfWarPath = GetFogOfWarPath();
            if (_messageStyle == null)
            {
                _messageStyle = new GUIStyle(EditorStyles.boldLabel);
                _messageStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel);
                _headerStyle.alignment = TextAnchor.MiddleCenter;
                _headerStyle.fontSize *= 2;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Thank you for using FogOfWar!", _messageStyle);
            InstallGUI();
            DebugGUI();
            EditorGUILayout.EndScrollView();
        }

        void Header(string label)
        {
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField(label, _headerStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));
        }

        void InstallGUI()
        {
            _hasInstalled = false;

            Header("Install");
            EditorGUILayout.HelpBox("This tool will help you set up FogOfWar, identify issues, and remove unnecessary files.", MessageType.Info);

            RendererGUI(rendererLegacyID);
            RendererGUI(rendererPPSv2ID);
            RendererGUI(rendererURPID);
            RendererGUI(rendererHDRPID);
        }

        void DebugGUI()
        {
            Header("Debug Current Scene");

            if (!_hasInstalled)
            {
                EditorGUILayout.HelpBox("Once you have installed a FogOfWar renderer, look here to make sure your scene is set up correctly.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("If your FogOfWar is not looking correct, open the scene and any issues will be displayed below.", MessageType.None);

            // Renderer errors
            foreach (string rendererid in rendererIDs)
            {
                string folderpath = _fogOfWarPath + "RenderPipelines/" + rendererid + "/";
                bool isinstalled = AssetDatabase.IsValidFolder(folderpath);
                if (!isinstalled)
                    continue;

                EditorGUILayout.Separator();
                EditorGUILayout.LabelField(rendererid, EditorStyles.boldLabel);
                CallStaticMethod(rendererid, "GetSceneErrors");

                if (FogOfWarError.hasMessages)
                    FogOfWarError.Display(true);
                else
                    EditorGUILayout.HelpBox("Everything appears to be set up correctly!", MessageType.Info);
            }

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Teams & Units", EditorStyles.boldLabel);

            // FogOfWarTeam errors 
            FogOfWarTeam[] teams = FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>();
            if (teams.Length == 0)
                EditorGUILayout.HelpBox("There is no FogOfWarTeam component in the scene!", MessageType.Error);

            foreach (FogOfWarTeam team in teams)
                FogOfWarTeamEditor.GetErrors(team, teams);

            // FogOfWarUnit errors
            FogOfWarUnit[] units = FogOfWarUtils.FindObjectsOfType<FogOfWarUnit>();
            if (units.Length == 0)
                EditorGUILayout.HelpBox("There is no FogOfWarUnit components in the scene!", MessageType.Error);

            foreach (FogOfWarUnit unit in units)
                FogOfWarUnitEditor.GetErrors(unit, teams);

            // FogOfWarHideInFog errors
            FogOfWarHideInFog[] hideinfogs = FogOfWarUtils.FindObjectsOfType<FogOfWarHideInFog>();
            foreach (FogOfWarHideInFog hideinfog in hideinfogs)
                FogOfWarHideInFogEditor.GetErrors(hideinfog, teams);

            if (FogOfWarError.hasMessages)
                FogOfWarError.Display(true);
            else
                EditorGUILayout.HelpBox("Everything appears to be set up correctly!", MessageType.Info);
        }

        void BeginRow(string label)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
        }

        void EndRow()
        {
            EditorGUILayout.EndHorizontal();
        }

        bool RowButton(string label, string buttontext)
        {
            BeginRow(label);
            bool pressed = GUILayout.Button(buttontext);
            EndRow();
            return pressed;
        }

        public static void OnCompileScripts()
        {
            string id = EditorPrefs.GetString(_lastInstalledID, null);
            if (string.IsNullOrEmpty(id))
                return;

            EditorPrefs.DeleteKey(_lastInstalledID);

            string fogofwarpath = GetFogOfWarPath();
            if (fogofwarpath != _defaultFogOfWarPath)
            {
                string subfolderpath = "RenderPipelines/" + id;
                AssetDatabase.MoveAsset(_defaultFogOfWarPath + subfolderpath, fogofwarpath + subfolderpath);
                AssetDatabase.DeleteAsset(_defaultFogOfWarPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            CallStaticMethod(id, "TryFixInstallIssues");
        }

        void RendererGUI(string id)
        {
            string folderpath = _fogOfWarPath + "RenderPipelines/" + id + "/";

            bool isinstalled = AssetDatabase.IsValidFolder(folderpath);
            if (isinstalled)
                _hasInstalled = true;

            if (RowButton(id, isinstalled ? "Uninstall" : "Install"))
            {
                if (!isinstalled)
                {
                    string unitypackagepath = _fogOfWarPath + "RenderPipelines/" + id + ".unitypackage";
                    DefaultAsset unitypackage = AssetDatabase.LoadAssetAtPath<DefaultAsset>(unitypackagepath);
                    if (unitypackage == null)
                        EditorUtility.DisplayDialog("Error", "Missing installation file: " + unitypackagepath + ".\nPlease reimport the FogOfWar asset from the asset store.", "OK");
                    else
                    {
                        AssetDatabase.ImportPackage(unitypackagepath, false);
                        EditorPrefs.SetString(_lastInstalledID, id);
                        AssetDatabase.Refresh();
                    }
                }

                if (isinstalled && EditorUtility.DisplayDialog("Uninstalling " + id, "Are you sure you want to uninstall the " + id + " for FogOfWar?", "Uninstall", "Cancel"))
                {
                    AssetDatabase.DeleteAsset(folderpath);
                    AssetDatabase.Refresh();
                }
            }

            // Display install errors
            if (isinstalled)
            {
                CallStaticMethod(id, "GetInstallErrors");

                bool haserrors = FogOfWarError.hasErrors;
                FogOfWarError.Display(true);

                if (haserrors)
                {
                    if (GUILayout.Button("Auto-Fix"))
                        CallStaticMethod(id, "TryFixInstallIssues");
                    EditorGUILayout.Space(30);
                }
            }
        }

        static void CallStaticMethod(string id, string methodname)
        {
            string classname = "FoW.FogOfWar" + id + "Editor";
            System.Type errorclass = System.Type.GetType(classname);
            if (errorclass == null)
            {
                FogOfWarError.Error(null, "Failed to find error check class " + classname + "!");
                return;
            }

            MethodInfo geterrorsmethod = errorclass.GetMethod(methodname, BindingFlags.Public | BindingFlags.Static);
            if (errorclass == null)
            {
                FogOfWarError.Error(null, "Failed to find " + classname + "." + methodname + "!");
                return;
            }

            geterrorsmethod.Invoke(null, null);
        }

        [MenuItem("Window/FogOfWar Setup")]
        public static void ShowWindow()
        {
            GetWindow(typeof(FogOfWarSetup));
        }

        static bool GetAlwaysIncludedShaderProperty(string shadername, out Shader shader, out SerializedProperty property)
        {
            shader = null;
            property = null;

            string[] guids = AssetDatabase.FindAssets("t:shader " + shadername);
            if (guids.Length == 0)
                return false;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                    return false;
            }

            SerializedObject graphicssettings = new SerializedObject(AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset"));
            property = graphicssettings.FindProperty("m_AlwaysIncludedShaders");
            bool found = false;
            for (int i = 0; i < property.arraySize; ++i)
            {
                SerializedProperty shaderprop = property.GetArrayElementAtIndex(i);
                if (shader == shaderprop.objectReferenceValue)
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        public static void CheckShaderIsIncludedInBuild(string shadername)
        {
            if (!GetAlwaysIncludedShaderProperty(shadername, out Shader shader, out _))
                FogOfWarError.Error(shader, "Shader '" + shadername + "' has not be included in the Always Included Shaders list in the Graphics Settings. FogOfWar will not appear correctly in builds.");
        }

        public static void ForceAddShaderBuild(string shadername)
        {
            if (GetAlwaysIncludedShaderProperty(shadername, out Shader shader, out SerializedProperty property) || shader == null || property == null)
                return;

            int arrayindex = property.arraySize;
            property.InsertArrayElementAtIndex(arrayindex);
            SerializedProperty shaderprop = property.GetArrayElementAtIndex(arrayindex);
            shaderprop.objectReferenceValue = shader;

            property.serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        public static void CheckMainShadersIncludedInBuild()
        {
            CheckShaderIsIncludedInBuild("FogOfWarBlurShader");
            CheckShaderIsIncludedInBuild("FogOfWarHardwareCombine");
            CheckShaderIsIncludedInBuild("FogOfWarHardwareFade");
            CheckShaderIsIncludedInBuild("FogOfWarHardwareShape");
            CheckShaderIsIncludedInBuild("FogOfWarHardwareVisibleToPartial");
        }

        public static void ForceAddMainShadersInBuild()
        {
            ForceAddShaderBuild("FogOfWarBlurShader");
            ForceAddShaderBuild("FogOfWarHardwareCombine");
            ForceAddShaderBuild("FogOfWarHardwareFade");
            ForceAddShaderBuild("FogOfWarHardwareShape");
            ForceAddShaderBuild("FogOfWarHardwareVisibleToPartial");
        }
    }
}

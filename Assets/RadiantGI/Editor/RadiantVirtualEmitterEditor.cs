using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace RadiantGI.Universal {

    [CustomEditor(typeof(RadiantVirtualEmitter))]
    public class RadiantVirtualEmitterEditor : Editor {

        [MenuItem("GameObject/Light/Radiant Virtual Emitter", false, 200)]
        static void CreateRadiantVirtualEmitter(MenuCommand menuCommand) {
            GameObject go = new GameObject("Radiant Virtual Emitter");
            go.AddComponent<RadiantVirtualEmitter>();
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }


        SerializedProperty color, intensity, range, shape, shapeSize;
        SerializedProperty addMaterialEmission, targetRenderer, material, emissionPropertyName, materialIndex;
        SerializedProperty boxCenter, boxSize, boundsInLocalSpace, fadeDistance, renderingLayerMask;

        private readonly BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();
        private readonly BoxBoundsHandle m_RangeBoxHandle = new BoxBoundsHandle();
        private readonly BoxBoundsHandle m_ShapeHandle = new BoxBoundsHandle();
        private readonly SphereBoundsHandle m_SphereHandle = new SphereBoundsHandle();

        void OnEnable() {
            color = serializedObject.FindProperty("color");
            intensity = serializedObject.FindProperty("intensity");
            range = serializedObject.FindProperty("range");
            shape = serializedObject.FindProperty("shape");
            shapeSize = serializedObject.FindProperty("shapeSize");
            addMaterialEmission = serializedObject.FindProperty("addMaterialEmission");
            targetRenderer = serializedObject.FindProperty("targetRenderer");
            material = serializedObject.FindProperty("material");
            emissionPropertyName = serializedObject.FindProperty("emissionPropertyName");
            materialIndex = serializedObject.FindProperty("materialIndex");
            boxCenter = serializedObject.FindProperty("boxCenter");
            boxSize = serializedObject.FindProperty("boxSize");
            boundsInLocalSpace = serializedObject.FindProperty("boundsInLocalSpace");
            fadeDistance = serializedObject.FindProperty("fadeDistance");
            renderingLayerMask = serializedObject.FindProperty("renderingLayerMask");
        }

        protected virtual void OnSceneGUI() {
            RadiantVirtualEmitter vi = (RadiantVirtualEmitter)target;

            // draw the handle
            Bounds bounds = vi.GetBounds();
            m_BoundsHandle.center = bounds.center;
            m_BoundsHandle.size = bounds.size;
            EditorGUI.BeginChangeCheck();
            m_BoundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck()) {
                // record the target object before setting new values so changes can be undone/redone
                Undo.RecordObject(vi, "Change Bounds");

                // copy the handle's updated data back to the target object
                Bounds newBounds = new Bounds();
                newBounds.center = m_BoundsHandle.center;
                newBounds.size = m_BoundsHandle.size;
                vi.SetBounds(newBounds);
            }

            if (vi.shape == RadiantVirtualEmitter.EmitterShape.Box) {
                Color prevColor = Handles.color;
                Matrix4x4 prevMatrix = Handles.matrix;

                // Draw shape and range handles in the emitter's local rotated frame so they orient with the transform.
                Handles.matrix = Matrix4x4.TRS(vi.transform.position, vi.transform.rotation, Vector3.one);

                m_RangeBoxHandle.center = Vector3.zero;
                m_RangeBoxHandle.size = vi.shapeSize + Vector3.one * vi.range * 2f;
                Handles.color = new Color(0f, 1f, 1f, 0.5f);
                EditorGUI.BeginChangeCheck();
                m_RangeBoxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(vi, "Change Range");
                    Vector3 rangeExtents = (m_RangeBoxHandle.size - vi.shapeSize) * 0.5f;
                    vi.range = Mathf.Max(0, rangeExtents.x, rangeExtents.y, rangeExtents.z);
                }

                m_ShapeHandle.center = Vector3.zero;
                m_ShapeHandle.size = vi.shapeSize;
                Handles.color = Color.yellow;
                EditorGUI.BeginChangeCheck();
                m_ShapeHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObjects(new Object[] { vi, vi.transform }, "Change Emitter Shape");
                    // Handle center is in local space; convert back to world for transform.position.
                    vi.transform.position = Handles.matrix.MultiplyPoint3x4(m_ShapeHandle.center);
                    vi.shapeSize = m_ShapeHandle.size;
                }

                Handles.color = prevColor;
                Handles.matrix = prevMatrix;
            } else {
                // draw sphere radius
                m_SphereHandle.center = vi.transform.position;
            m_SphereHandle.radius = vi.range;
                EditorGUI.BeginChangeCheck();
                m_SphereHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck()) {
                    // record the target object before setting new values so changes can be undone/redone
                    Undo.RecordObject(vi, "Change Radius");
                    vi.range = m_SphereHandle.radius;
                }
            }
        }

        static readonly GUIContent k_RenderingLayerMaskLabel = new GUIContent("Rendering Layer Mask", "Surfaces in these rendering layers will receive lighting from this emitter. Only used when 'Virtual Emitters - Use Rendering Layers' is enabled in the Radiant Volume profile.");

        static void DrawRenderingLayerMaskField(SerializedProperty prop) {
            string[] names = GetRenderingLayerNames();
            EditorGUI.BeginChangeCheck();
            int newMask = EditorGUILayout.MaskField(k_RenderingLayerMaskLabel, prop.intValue, names);
            if (EditorGUI.EndChangeCheck()) {
                prop.intValue = newMask;
            }
        }

        static readonly string[] s_LayerNames = BuildLayerNames();
        static string[] BuildLayerNames() {
            string[] r = new string[32];
            for (int i = 0; i < 32; i++) r[i] = "Layer " + i;
            return r;
        }
        static string[] GetRenderingLayerNames() => s_LayerNames;

        public override void OnInspectorGUI() {

            serializedObject.Update();

            EditorGUILayout.PropertyField(color);
            EditorGUILayout.PropertyField(addMaterialEmission);
            if (addMaterialEmission.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetRenderer);
                EditorGUILayout.PropertyField(material);
                EditorGUILayout.PropertyField(emissionPropertyName);
                EditorGUILayout.PropertyField(materialIndex);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(intensity);
            EditorGUILayout.PropertyField(range);
            EditorGUILayout.PropertyField(shape);
            if (shape.enumValueIndex == (int)RadiantVirtualEmitter.EmitterShape.Box) {
                EditorGUILayout.PropertyField(shapeSize, new GUIContent("Size"));
            }
            EditorGUILayout.PropertyField(boxCenter);
            EditorGUILayout.PropertyField(boxSize);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(boundsInLocalSpace, new GUIContent("Local Space"));
            if (EditorGUI.EndChangeCheck()) {
                RadiantVirtualEmitter vi = (RadiantVirtualEmitter)target;
                if (boundsInLocalSpace.boolValue) {
                    boxCenter.vector3Value = Vector3.zero;
                } else {
                    boxCenter.vector3Value = vi.transform.position;
                }
                vi.SetBounds(new Bounds(boxCenter.vector3Value, boxSize.vector3Value));
            }
            EditorGUILayout.PropertyField(fadeDistance);
            DrawRenderingLayerMaskField(renderingLayerMask);

            serializedObject.ApplyModifiedProperties();

        }

    }


    public static class RadiantVirtualEmitterEditorExtension {

        [MenuItem("GameObject/Create Other/Radiant GI/Virtual Emitter")]
        static void CreateEmitter(MenuCommand menuCommand) {
            GameObject emitter = new GameObject("Radiant Virtual Emitter", typeof(RadiantVirtualEmitter));

            GameObjectUtility.SetParentAndAlign(emitter, menuCommand.context as GameObject);

            Undo.RegisterCreatedObjectUndo(emitter, "Create Virtual Emitter");
            Selection.activeObject = emitter;
        }

    }
}


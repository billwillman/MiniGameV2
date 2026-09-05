using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace FoW
{
    [CustomPropertyDrawer(typeof(EnableIfAttribute))]
    public class EnableIfAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginDisabledGroup(!DoEnabledCheck(property));
            EditorGUI.PropertyField(position, property, label);
            EditorGUI.EndDisabledGroup();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement element = base.CreatePropertyGUI(property);
            if (element != null)
                element.SetEnabled(DoEnabledCheck(property));
            return element;
        }

        bool DoEnabledCheck(SerializedProperty property)
        {
            Object[] owners = property.serializedObject.targetObjects;
            if (owners.Length != 1)
                return true;

            EnableIfAttribute enableif = attribute as EnableIfAttribute;
            SerializedProperty checkproperty = property.serializedObject.FindProperty(enableif.valueName);

            if (checkproperty.propertyType == SerializedPropertyType.Boolean)
                return enableif.IsEnabled(checkproperty.boolValue);
            if (checkproperty.propertyType == SerializedPropertyType.Integer)
                return enableif.IsEnabled(checkproperty.intValue);
            if (checkproperty.propertyType == SerializedPropertyType.Float)
                return enableif.IsEnabled(checkproperty.floatValue);
            if (checkproperty.propertyType == SerializedPropertyType.Enum)
                return enableif.IsEnabled(checkproperty.enumValueIndex);
            if (checkproperty.propertyType == SerializedPropertyType.LayerMask)
                return enableif.IsEnabled(checkproperty.intValue);
            if (checkproperty.propertyType == SerializedPropertyType.ObjectReference)
                return enableif.IsEnabled(checkproperty.objectReferenceValue);

            throw new System.Exception("Unsupported type for EnableIfAttribute.");
        }
    }
}

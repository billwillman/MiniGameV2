using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FoW
{
    public struct FogOfWarError
    {
        public Object owner;
        public string text;
        public MessageType messageType;

        static List<FogOfWarError> _cachedErrors = new List<FogOfWarError>();
        public static bool hasMessages => _cachedErrors.Count > 0;
        public static bool hasErrors
        {
            get
            {
                for (int i = 0; i < _cachedErrors.Count; ++i)
                {
                    if (_cachedErrors[i].messageType == MessageType.Error)
                        return true;
                }
                return false;
            }
        }

        public static void Warning(Object owner, string text)
        {
            Add(owner, text, MessageType.Warning);
        }

        public static void Error(Object owner, string text)
        {
            Add(owner, text, MessageType.Error);
        }

        public static void Add(Object owner, string text, MessageType messagetype)
        {
            _cachedErrors.Add(new FogOfWarError()
            {
                owner = owner,
                text = text,
                messageType = messagetype
            });
        }

        public static void Display(bool showowner)
        {
            for (int i = 0; i < _cachedErrors.Count; ++i)
            {
                string displaytext = _cachedErrors[i].text;
                if (showowner && _cachedErrors[i].owner != null)
                    displaytext = _cachedErrors[i].owner.GetType().Name + " (" + _cachedErrors[i].owner.name + "): " + displaytext;

                EditorGUILayout.HelpBox(displaytext, _cachedErrors[i].messageType);
            }

            _cachedErrors.Clear();
        }
    }
}

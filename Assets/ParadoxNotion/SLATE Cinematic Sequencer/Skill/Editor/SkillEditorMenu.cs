using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Slate;

namespace GAS
{
    public class SkillEditorMenu : Editor
    {
        [MenuItem("Tools/�������ܱ༭��")]
        public static void OpenSkillEditorWindow()
        {
            var CutScene = Commands.CreateCutscene();
            List<System.Type> groupTypes = new List<System.Type> { 
                typeof(CommonGroup),
                typeof(DSGroup),
                typeof(ClientGroup),
            };
            CutScene.ChangeToSkillMode(groupTypes);
        }
    }

}

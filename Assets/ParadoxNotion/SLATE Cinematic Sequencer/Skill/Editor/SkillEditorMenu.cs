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
            CutScene.ChangeToSkillMode();
            // �������
            //------------
        }
    }

}

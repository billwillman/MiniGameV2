using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Slate;

namespace GAS
{
    public class SkillEditorMenu : Editor
    {
        [MenuItem("Tools/创建技能编辑器")]
        public static void OpenSkillEditorWindow()
        {
            var CutScene = Commands.CreateCutscene();
            CutScene.ChangeToSkillMode();
            // 创建轨道
            //------------
        }
    }

}

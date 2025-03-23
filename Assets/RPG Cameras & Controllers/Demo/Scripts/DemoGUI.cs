using UnityEngine;
using UnityEngine.SceneManagement;

namespace JohnStairs.RCC.Demo {
    public class DemoGUI : MonoBehaviour {
        private void Awake() {
        }

        public void Update() {
        }

        public void ClickPresetMMO() {
            SceneManager.LoadScene("MMO");
        }

        public void ClickPresetARPG() {
            SceneManager.LoadScene("ARPG");
        }

        public void ClickPresetIsometric() {
            SceneManager.LoadScene("Isometric");
        }

        public void ClickPresetPlayground() {
            SceneManager.LoadScene("Playground");
        }
    }
}
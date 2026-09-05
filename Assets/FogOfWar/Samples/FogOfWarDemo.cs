using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FoW
{
    public class FogOfWarDemo : MonoBehaviour
    {
        public string nextDemoName;
        public Button nextButton;
        public Button demoInfoButton;
        public GameObject demoInfoPanel;

        void Start()
        {
            nextButton.onClick.AddListener(NextDemo);
            demoInfoButton.onClick.AddListener(() => demoInfoPanel.SetActive(!demoInfoPanel.activeSelf));

            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponentInChildren<LayoutGroup>().GetComponent<RectTransform>());
        }

        void NextDemo()
        {
            SceneManager.LoadScene(nextDemoName + '_' + gameObject.scene.name.Split('_')[1]);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();
        }
    }
}

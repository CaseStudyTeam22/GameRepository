using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    // DontDestroyOnLoad されたオブジェクトを破棄する
    private void DestroyAllDontDestroyOnLoadObjects()
    {
        var temp = new GameObject("Temp");
        DontDestroyOnLoad(temp);

        foreach (var root in temp.scene.GetRootGameObjects())
        {
            if (root.name != "Temp")
            {
                Destroy(root);
            }
        }

        Destroy(temp);
    }

    public void LoadScene(string sceneName)
    {
        DestroyAllDontDestroyOnLoadObjects();
        SceneManager.LoadScene(sceneName);
    }
}

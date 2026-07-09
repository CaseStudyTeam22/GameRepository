using UnityEngine;

public class ExitButton : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("ÉQÅ[ÉÄèIóπ");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

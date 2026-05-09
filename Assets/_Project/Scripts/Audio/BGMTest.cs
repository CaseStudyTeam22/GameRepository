using UnityEngine;

namespace GamblingAction.Audio
{
    public class BGMTest : MonoBehaviour
    {
        void Start()
        {
            // ゲーム開始時にBGMシステムを起動する
            // （"Play_BGM" はステップ1で作ったEvent名）
            AkUnitySoundEngine.PostEvent("Play_BGMSystem", gameObject);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                // SetState("State Group名", "State名")
                AkUnitySoundEngine.SetState("GameState", "Explore");
                Debug.Log("BGMへ遷移！");
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                AkUnitySoundEngine.SetState("GameState", "Battle");
                Debug.Log("BGMへ（小節の区切りでサビから）遷移！");
            }
        }
    }
}

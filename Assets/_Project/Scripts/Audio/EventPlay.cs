using System.Collections;
using UnityEngine;

namespace GamblingAction.Audio
{
    public class EventPlay : MonoBehaviour
    {
        [Header("Wwise設定")]
        [SerializeField] private AK.Wwise.Event myEvent;

        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _renderer.material.color = Color.grey;
        }

        private void Start()
        {
            StartCoroutine(PostWhenReady());

        }

        private IEnumerator PostWhenReady()
        {
            // AkBankコンポーネントのロード完了を1フレーム待つ
            yield return null;

            uint playingID = myEvent.Post(
                gameObject,
                (uint)AkCallbackType.AK_EndOfEvent,
                OnEventCallback,
                null
            );

            Debug.Log("Playing ID: " + playingID);
        }

        private void OnEventCallback(object cookie, AkCallbackType type, AkCallbackInfo info)
        {
            if (type == AkCallbackType.AK_EndOfEvent)
            {
                // 音が終わったらCubeを赤くする
                _renderer.material.color = Color.red;
                Debug.Log("音が終わりました");
            }
        }
    }
}

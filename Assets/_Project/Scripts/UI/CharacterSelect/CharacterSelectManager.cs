using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using GamblingAction.UI;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("キャラクターUIデータ")]
    [SerializeField]
    private List<CharacterSelectData> m_CharacterUiDatas;   // キャラクターUIデータのリスト

    [Header("キャラクターUIの親オブジェクト")]
    [SerializeField]
    private Transform m_CharacterRoot;

    [Header("キャラクターUIのプレハブ")]
    [SerializeField]
    private CharacterSelectItem m_ItemPrefab;

    [Header("キャラクターUIのスライド速度")]
    [SerializeField]
    private float m_MoveSpeed = 5.0f;

    private const float CharacterUiSpacing = 1000.0f;   // キャラクターUIの間隔

    private int m_SelectIndex = 0;   // 現在選択されているキャラクターのインデックス

    private Vector2 m_TargetPos = Vector2.zero;    // キャラクターUIの目標位置

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // キャラクターUIの生成
        CreateCharacterUi();

        // 最初のキャラクターを選択状態にする
        m_SelectIndex = 0;
        Debug.Log($"SetCharacter No.{m_SelectIndex} :  {m_CharacterUiDatas[m_SelectIndex].m_CharaType.ToString()}");   // デバッグ用のログ出力
    }

    // Update is called once per frame
    void Update()
    {
        RectTransform rect = m_CharacterRoot.GetComponent<RectTransform>();

        // キャラクターUIの位置を滑らかに移動させる
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, m_TargetPos, Time.deltaTime * m_MoveSpeed);

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextCharacter();   // 右矢印キーが押されたら次のキャラクターを選択する

            // デバッグ用のログ出力
            Debug.Log($"SetCharacter No.{m_SelectIndex} :  {m_CharacterUiDatas[m_SelectIndex].m_CharaType.ToString()}");
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            BackCharacter();   // 左矢印キーが押されたら前のキャラクターを選択する

            // デバッグ用のログ出力
            Debug.Log($"SetCharacter No.{m_SelectIndex} :  {m_CharacterUiDatas[m_SelectIndex].m_CharaType.ToString()}");
        }
    }

    // 選択キャラクターアイコンの生成
    private void CreateCharacterUi()
    {
        for (int i = 0; i < m_CharacterUiDatas.Count; ++i)
        {
            // キャラクターUIの生成処理をここに実装
            CharacterSelectItem item = Instantiate(m_ItemPrefab, m_CharacterRoot);

            // キャラクターUIのデータを設定
            item.Setup(m_CharacterUiDatas[i]);

            // キャラクターUIの位置を設定
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(i * CharacterUiSpacing, 0.0f);  // キャラクターUIの位置を設定（例: 横に500ピクセルずつ配置）
        }
    }

    // 次のキャラクターを選択する処理
    private void NextCharacter()
    {
        // 次のキャラクターを選択する処理をここに実装
        m_SelectIndex++;

        // 最後のキャラクターを選択した後は最初のキャラクターに戻る
        if (m_SelectIndex >= m_CharacterUiDatas.Count)
        {
            m_SelectIndex = 0;
        }

        // UIを移動させる処理を呼び出す
        MoveCharacterUi();
    }


    // 前のキャラクターを選択する処理
    private void BackCharacter()
    {
        // 前のキャラクターを選択する処理をここに実装
        m_SelectIndex--;

        // 最初のキャラクターを選択した後は最後のキャラクターに戻る
        if (m_SelectIndex < 0)
        {
            m_SelectIndex = m_CharacterUiDatas.Count - 1;
        }

        // UIを移動させる処理を呼び出す
        MoveCharacterUi();
    }

    // キャラクターUIを移動させる処理
    private void MoveCharacterUi()
    {
        // キャラクターUIの目標位置を更新（UI全体を左へ動かしてるからm_SelectIndexはマイナス）
        m_TargetPos = new Vector2(-m_SelectIndex * CharacterUiSpacing, 0.0f);
    }
}

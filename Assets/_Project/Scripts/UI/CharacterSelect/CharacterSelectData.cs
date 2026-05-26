using UnityEngine;

[System.Serializable]
public class CharacterSelectData
{
    public ECharacterType m_CharaType;  // キャラクターの種類

    [Header("UIデータ")]
    public Sprite m_Icon;   // キャラクターのアイコン
    public string m_CharacterName;   // キャラクターの名前

    [TextArea]
    public string m_Description;    // キャラクターの説明文
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelButton : MonoBehaviour
{
    public TMP_Text label;
    public GameObject lockIcon;   // cadenas
    public Button button;

    LevelInfo info;
    LevelMenuUI menu;

    public void Setup(LevelInfo level, bool unlocked, LevelMenuUI owner)
    {
        info = level;
        menu = owner;

        label.text = level.displayName;
        lockIcon.SetActive(!unlocked);
        button.interactable = unlocked;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (info != null) menu.LoadLevel(info);
    }
}
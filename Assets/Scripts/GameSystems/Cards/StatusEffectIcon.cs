using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StatusEffectIcon : MonoBehaviour
{
    public Image icon;

    public void Setup(StatusEffectData data)
    {
        icon.sprite = data.icon;
    }
}

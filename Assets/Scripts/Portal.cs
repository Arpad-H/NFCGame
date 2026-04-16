using TMPro;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public ResonanceType resonanceType;
    public TextMeshProUGUI identityText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public  void SetResonanceType(ResonanceType type)
    {
        resonanceType = type;
        identityText.text = type.ToString();
    }
}

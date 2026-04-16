using TMPro;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public Resonance resonance;
    public TextMeshProUGUI identityText;
    
    private Renderer portalRenderer;
    private MaterialPropertyBlock propBlock;
    void Awake()
    {
        portalRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }
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
        resonance = ResonanceLibrary.Instance.GetResonance(type);
        identityText.text = resonance.identity;
        ApplyColor(resonance.color);
    }
    private void ApplyColor(Color newColor)
    {
        if (portalRenderer == null) return;

        // Using MaterialPropertyBlock is more efficient than .material
        // as it prevents creating a unique material instance for every portal
        portalRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", newColor); // Use "_Color" if using Standard Shader
        portalRenderer.SetPropertyBlock(propBlock);
    }
}

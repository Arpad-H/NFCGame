using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// SCENE SETUP:
///   1. Create a dedicated export scene.
///   2. Place the card prefab in the scene at (0,0,0).
///      Set its own Canvas component's Render Mode to "World Space".
///   3. Add a Camera — set Projection to "Orthographic".
///      Position at (0, 0, -10) looking toward +Z.
///      Clear Flags: "Solid Color", Background: (R=0, G=0, B=0, A=0).
///   4. Attach this script to any GameObject. Wire up the three Inspector references.
///      For cardCanvas, drag the Canvas that is on the card prefab itself.
///   5. Hit Play — all cards export, then Play mode stops automatically.
///
/// OUTPUT: A "CardExports/" folder next to your project's Assets folder.
public class CardExporter : MonoBehaviour
{
    [Header("Scene References")]
    public Camera exportCamera;
    public CardVisualizer cardVisualizer;
    public Canvas cardCanvas;

    [Header("Export Settings")]
    public int textureWidth = 512;
    public int textureHeight = 768;
    public string outputFolder = "CardExports";

    private async void Start()
    {
        Debug.Log("[CardExporter] Initializing CardLibrary via Addressables...");
        await CardLibrary.Initialize();
        Debug.Log("[CardExporter] CardLibrary ready. Starting export...");
        StartCoroutine(ExportAll());
    }

    private IEnumerator ExportAll()
    {
        var cards = CardLibrary.GetCards();
        string exportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
        Directory.CreateDirectory(exportPath);
        Debug.Log($"[CardExporter] Exporting {cards.Count} cards to: {exportPath}");

        exportCamera.clearFlags = CameraClearFlags.SolidColor;
        exportCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        exportCamera.allowHDR = false;
        exportCamera.allowMSAA = false;

        if (cardCanvas != null && cardCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            cardCanvas.worldCamera = exportCamera;

        // Auto-frame an orthographic camera to exactly fit the card RectTransform
        if (exportCamera.orthographic && cardCanvas != null && cardCanvas.renderMode == RenderMode.WorldSpace)
        {
            RectTransform cardRect = cardVisualizer.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                Vector2 size = cardRect.rect.size;
                Vector3 worldScale = cardRect.lossyScale;
                float worldW = size.x * worldScale.x;
                float worldH = size.y * worldScale.y;
                float aspect = (float)textureWidth / textureHeight;
                float orthoFromH = worldH / 2f;
                float orthoFromW = (worldW / 2f) / aspect;
                exportCamera.orthographicSize = Mathf.Max(orthoFromH, orthoFromW);
            }
        }

        RenderTexture rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        exportCamera.targetTexture = rt;

        int count = 0;
        foreach (var card in cards)
        {
            // Clear conditionally-set fields so values don't bleed between card types
            cardVisualizer.HPText.text = "";
            cardVisualizer.AttackText.text = "";
            cardVisualizer.PassiveText.text = "";
            cardVisualizer.Effect1Text.text = "";
            cardVisualizer.Effect2Text.text = "";

            cardVisualizer.SetupForLibrary(card);

            // Two frames so layout groups and ContentSizeFitters fully rebuild
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            exportCamera.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(exportPath, SanitizeFileName(card.cardName) + ".png"), png);

            Destroy(tex);
            count++;
            Debug.Log($"[CardExporter] [{count}/{cards.Count}] {card.cardName}");
        }

        exportCamera.targetTexture = null;
        rt.Release();
        Destroy(rt);

        Debug.Log($"[CardExporter] Done! {count} cards saved to: {exportPath}");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}

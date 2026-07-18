using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// SCENE SETUP:
///   1. Create a dedicated export scene.
///   2. Add a Camera — set Projection to "Orthographic".
///      Position at (0, 0, -10) looking toward +Z.
///      Clear Flags: "Solid Color", Background: (R=0, G=0, B=0, A=0).
///   3. Attach this script to any GameObject. Wire up exportCamera and both
///      card prefabs (fieldableCardPrefab, spellCardPrefab). Each prefab's own
///      Canvas should be Render Mode "World Space".
///   4. Hit Play — every card is instantiated from the prefab that matches its
///      type, exported, then destroyed; Play mode stops automatically.
///
/// OUTPUT: A "CardExports/" folder next to your project's Assets folder:
///   - <card>.png            raw render, transparent background
///   - Sheets/<card>.png     composite 15x20 cm photo: the card back (left) and
///                           front (right) side by side (1 mm gap), each grown as
///                           large as the aspect ratio allows — ready for a photo
///                           print. Falls back to two fronts if no back prefab is set (bcs printing companies are unreliable).
///   - Print/<card>.pdf      print-ready PDF: one 70x120 mm page with the card
///                           image filling the centered 62x112 mm printable
///                           area (the grey area of the print shop's guide)
///   - Print/_AllCards.pdf   every card as one page of a single PDF
///
public class CardExporter : MonoBehaviour
{
    [Header("Scene References")]
    public Camera exportCamera;

    [Header("Card Prefabs")]
    [Tooltip("Full-card prefab for minions and items (FieldableCardVisualizer).")]
    public GameObject fieldableCardPrefab;
    [Tooltip("Full-card prefab for spells (SpellCardVisualizer).")]
    public GameObject spellCardPrefab;

    [Header("Export Settings")]
    [Tooltip("Keep width:height equal to the printable-area aspect (62:112), otherwise the PDF stretches the image. 744x1344 is that aspect at ~300 DPI.")]
    public int textureWidth = 744;
    public int textureHeight = 1344;
    public string outputFolder = "CardExports";
    public bool exportPng = true;
    [Tooltip("Seconds to wait after SetupForLibrary before capturing, so async art/addressable loads have time to finish (otherwise some cards get exported showing their placeholder). Increase if you still see placeholders.")]
    public float cardInitializationDelay = 0.25f;

    [Header("Print PDFs (dimensions from the print shop's 70x120 mm guide)")]
    [Tooltip("Write Print/<card>.pdf per card: a 70x120 mm page with the image filling the printable area.")]
    public bool exportPdfPerCard = true;
    [Tooltip("Write Print/_AllCards.pdf with every card as one page.")]
    public bool exportCombinedPdf = true;
    [Tooltip("Physical page size in mm.")]
    public Vector2 pageSizeMm = new Vector2(70f, 120f);
    [Tooltip("Printable area in mm (the grey area of the guide), centered on the page.")]
    public Vector2 printableSizeMm = new Vector2(62f, 112f);

    [Header("Photo Sheets (two copies of each card on one 15x20cm photo)")]
    [Tooltip("Write Sheets/<card>.png: one composite photo per card with the front and, on the left, " +
             "the card back, each grown as large as the render's aspect ratio allows.")]
    public bool exportSheets = true;
    [Tooltip("Optional card-back prefab (e.g. CardV2Backside). When set, each sheet shows this back on the " +
             "left and the card front on the right. Leave empty to put two copies of the front instead.")]
    public GameObject cardBackPrefab;
    [Tooltip("Physical photo size in mm. Orientation is chosen automatically to maximise card size " +
             "(landscape wins for a 15x20 photo), so 150x200 and 200x150 are equivalent here.")]
    public Vector2 sheetSizeMm = new Vector2(150f, 200f);
    [Tooltip("Gap between the two cards, in mm.")]
    public float cardGapMm = 1f;
    [Tooltip("Blank margin kept around everything, in mm. 0 = full bleed to the photo edge.")]
    public float sheetMarginMm = 0f;
    [Tooltip("Output resolution of the composite photo, in DPI. 300 is standard for photo prints.")]
    public int sheetDpi = 300;
    [Tooltip("Subfolder (under the export folder) for the composite photos.")]
    public string sheetsFolder = "Sheets";

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
        string printPath = Path.Combine(exportPath, "Print");
        if (exportPdfPerCard || exportCombinedPdf)
        {
            Directory.CreateDirectory(printPath);

            float printableAspect = printableSizeMm.x / printableSizeMm.y;
            float textureAspect = (float)textureWidth / textureHeight;
            if (Mathf.Abs(textureAspect - printableAspect) > printableAspect * 0.01f)
                Debug.LogWarning($"[CardExporter] Texture {textureWidth}x{textureHeight} doesn't match the " +
                                 $"{printableSizeMm.x}x{printableSizeMm.y} mm printable area, so the PDF image " +
                                 "will be distorted. Use e.g. 744x1344 (62:112 aspect at ~300 DPI).");
        }
        string sheetsPath = Path.Combine(exportPath, sheetsFolder);
        SheetLayout sheetLayout = default;
        if (exportSheets)
        {
            Directory.CreateDirectory(sheetsPath);
            sheetLayout = ComputeSheetLayout();
            if (sheetLayout.valid)
                Debug.Log($"[CardExporter] Photo sheet {sheetLayout.pageWmm:0.#}x{sheetLayout.pageHmm:0.#} mm: " +
                          $"two cards at {sheetLayout.cardWmm:0.#}x{sheetLayout.cardHmm:0.#} mm each " +
                          $"({sheetLayout.sheetW}x{sheetLayout.sheetH} px @ {sheetDpi} DPI).");
            else
                Debug.LogWarning("[CardExporter] Sheet layout is invalid (margins/gap too large for the photo). Skipping sheets.");
        }

        Debug.Log($"[CardExporter] Exporting {cards.Count} cards to: {exportPath}");

        exportCamera.clearFlags = CameraClearFlags.SolidColor;
        exportCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        exportCamera.allowHDR = false;
        exportCamera.allowMSAA = false;

        RenderTexture rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        exportCamera.targetTexture = rt;

        PrintPdfWriter combined = exportCombinedPdf
            ? new PrintPdfWriter(Path.Combine(printPath, "_AllCards.pdf"), pageSizeMm, printableSizeMm)
            : null;

        // The back is identical on every card, so render it once and reuse it for
        // the left slot of every sheet. Null when no back prefab is wired up, in
        // which case sheets fall back to two copies of the front.
        Texture2D backTex = null;
        if (exportSheets && sheetLayout.valid && cardBackPrefab != null)
            yield return RenderToTexture(cardBackPrefab, null, rt, t => backTex = t);

        int count = 0;
        try
        {
            foreach (var card in cards)
            {
                // Each card type has its own prefab (and its own set of components),
                // so spawn the matching one fresh rather than reusing one object and
                // clearing fields between types.
                GameObject prefab = CardPrefabResolver.Resolve(card, fieldableCardPrefab, spellCardPrefab);
                Texture2D tex = null;
                yield return RenderToTexture(prefab,
                    go => go.GetComponent<CardVisualizer>().SetupForLibrary(card), rt, t => tex = t);

                string fileName = SanitizeFileName(card.cardName);
                if (exportPng)
                    File.WriteAllBytes(Path.Combine(exportPath, fileName + ".png"), tex.EncodeToPNG());

                if (exportSheets && sheetLayout.valid)
                    File.WriteAllBytes(Path.Combine(sheetsPath, fileName + ".png"),
                                       BuildSheetPng(backTex != null ? backTex : tex, tex, sheetLayout));

                if (exportPdfPerCard || combined != null)
                {
                    // Flattened onto white and compressed once, shared by both PDFs.
                    byte[] zlibRgb = PrintPdfWriter.FlattenToZlibRgb(tex);
                    if (exportPdfPerCard)
                    {
                        using (var pdf = new PrintPdfWriter(Path.Combine(printPath, fileName + ".pdf"), pageSizeMm, printableSizeMm))
                            pdf.AddPage(zlibRgb, tex.width, tex.height);
                    }
                    combined?.AddPage(zlibRgb, tex.width, tex.height);
                }

                Destroy(tex);
                count++;
                Debug.Log($"[CardExporter] [{count}/{cards.Count}] {card.cardName}");
            }
        }
        finally
        {
            // Runs even when the export is aborted mid-way, so the combined PDF
            // still gets its cross-reference table and remains openable.
            combined?.Dispose();
        }

        if (backTex != null)
            Destroy(backTex);
        exportCamera.targetTexture = null;
        rt.Release();
        Destroy(rt);

        Debug.Log($"[CardExporter] Done! {count} cards saved to: {exportPath}");
        if (exportPdfPerCard || exportCombinedPdf)
            Debug.Log($"[CardExporter] Print-ready PDFs in: {printPath}");

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

    /// <summary>
    /// Spawns <paramref name="prefab"/>, lets <paramref name="setup"/> populate it
    /// (null for the static card back), renders it through the export camera into
    /// <paramref name="rt"/>, and hands the read-back Texture2D to <paramref name="onDone"/>.
    /// The caller owns the returned texture and must Destroy it.
    /// </summary>
    private IEnumerator RenderToTexture(GameObject prefab, System.Action<GameObject> setup,
                                        RenderTexture rt, System.Action<Texture2D> onDone)
    {
        GameObject cardGo = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        Canvas cardCanvas = cardGo.GetComponent<Canvas>();
        if (cardCanvas != null && cardCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            cardCanvas.worldCamera = exportCamera;

        setup?.Invoke(cardGo);

        if (cardInitializationDelay > 0f)
            yield return new WaitForSecondsRealtime(cardInitializationDelay);

        // Auto-frame an orthographic camera to exactly fit the card RectTransform.
        if (exportCamera.orthographic && cardCanvas != null && cardCanvas.renderMode == RenderMode.WorldSpace)
        {
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
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

        Destroy(cardGo);
        onDone(tex);
    }

    /// Pixel geometry for one composite photo: the two card slots (bottom-left
    /// origin, matching Texture2D pixel space) inside the full sheet.
    private struct SheetLayout
    {
        public bool valid;
        public int sheetW, sheetH;      // pixels
        public RectInt slotA, slotB;    // pixels
        public float pageWmm, pageHmm, cardWmm, cardHmm; // for logging
    }

    /// <summary>
    /// Works out how to place two copies of a card on the photo: picks the
    /// orientation (15x20 vs 20x15) that lets the cards grow largest, sizes
    /// each card to the render's own aspect ratio (so nothing is stretched),
    /// then centres the pair with the configured gap.
    /// </summary>
    private SheetLayout ComputeSheetLayout()
    {
        float aspect = (float)textureWidth / textureHeight; // card width / height

        // Best card size for a given page orientation, honouring the margin,
        // gap and aspect. Card grows until it hits the width (two side by side)
        // or the height, whichever binds first.
        (float w, float h) Fit(float pageW, float pageH)
        {
            float availW = pageW - 2f * sheetMarginMm;
            float availH = pageH - 2f * sheetMarginMm;
            float widthLimitedW = (availW - cardGapMm) * 0.5f;
            if (widthLimitedW <= 0f || availH <= 0f)
                return (0f, 0f);
            float widthLimitedH = widthLimitedW / aspect;
            return widthLimitedH <= availH
                ? (widthLimitedW, widthLimitedH)   // two cards fill the width
                : (aspect * availH, availH);        // cards fill the height
        }

        // Try the photo both ways up and keep the orientation with bigger cards.
        var (cwA, chA) = Fit(sheetSizeMm.x, sheetSizeMm.y);
        var (cwB, chB) = Fit(sheetSizeMm.y, sheetSizeMm.x);
        float pageW, pageH, cardW, cardH;
        if (cwA * chA >= cwB * chB) { pageW = sheetSizeMm.x; pageH = sheetSizeMm.y; cardW = cwA; cardH = chA; }
        else { pageW = sheetSizeMm.y; pageH = sheetSizeMm.x; cardW = cwB; cardH = chB; }

        var layout = new SheetLayout { pageWmm = pageW, pageHmm = pageH, cardWmm = cardW, cardHmm = cardH };
        if (cardW <= 0f || cardH <= 0f)
            return layout; // valid stays false

        float pxPerMm = sheetDpi / 25.4f;
        layout.sheetW = Mathf.RoundToInt(pageW * pxPerMm);
        layout.sheetH = Mathf.RoundToInt(pageH * pxPerMm);
        int slotW = Mathf.RoundToInt(cardW * pxPerMm);
        int slotH = Mathf.RoundToInt(cardH * pxPerMm);

        float groupW = 2f * cardW + cardGapMm;
        float groupX = (pageW - groupW) * 0.5f;
        float cardY = (pageH - cardH) * 0.5f;
        int y = Mathf.RoundToInt(cardY * pxPerMm);
        int gap = Mathf.RoundToInt(cardGapMm * pxPerMm);
        int xA = Mathf.RoundToInt(groupX * pxPerMm);
        layout.slotA = new RectInt(xA, y, slotW, slotH);
        layout.slotB = new RectInt(xA + slotW + gap, y, slotW, slotH);
        layout.valid = true;
        return layout;
    }

    /// Composites the left and right faces onto a white photo-sheet and returns PNG bytes.
    private static byte[] BuildSheetPng(Texture2D left, Texture2D right, SheetLayout layout)
    {
        Color32[] leftSrc = left.GetPixels32();   // bottom row first
        Color32[] rightSrc = right.GetPixels32();

        Color32[] sheet = new Color32[layout.sheetW * layout.sheetH];
        Color32 white = new Color32(255, 255, 255, 255);
        for (int i = 0; i < sheet.Length; i++)
            sheet[i] = white;

        BlitSlot(sheet, layout.sheetW, layout.sheetH, leftSrc, left.width, left.height, layout.slotA);
        BlitSlot(sheet, layout.sheetW, layout.sheetH, rightSrc, right.width, right.height, layout.slotB);

        var tex = new Texture2D(layout.sheetW, layout.sheetH, TextureFormat.RGB24, false);
        tex.SetPixels32(sheet);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);
        return png;
    }

    /// Bilinearly scales the card into a slot, flattening transparency onto white.
    private static void BlitSlot(Color32[] dst, int dstW, int dstH,
                                 Color32[] src, int srcW, int srcH, RectInt slot)
    {
        for (int dy = 0; dy < slot.height; dy++)
        {
            int destY = slot.y + dy;
            if (destY < 0 || destY >= dstH)
                continue;
            float v = (dy + 0.5f) / slot.height;
            int rowBase = destY * dstW;
            for (int dx = 0; dx < slot.width; dx++)
            {
                int destX = slot.x + dx;
                if (destX < 0 || destX >= dstW)
                    continue;
                float u = (dx + 0.5f) / slot.width;
                dst[rowBase + destX] = SampleFlatWhite(src, srcW, srcH, u, v);
            }
        }
    }

    /// Bilinear sample of a bottom-left-origin RGBA buffer, alpha-flattened onto white.
    private static Color32 SampleFlatWhite(Color32[] src, int w, int h, float u, float v)
    {
        float fx = u * w - 0.5f;
        float fy = v * h - 0.5f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, w - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, h - 1);
        int x1 = Mathf.Min(x0 + 1, w - 1);
        int y1 = Mathf.Min(y0 + 1, h - 1);
        float tx = Mathf.Clamp01(fx - Mathf.FloorToInt(fx));
        float ty = Mathf.Clamp01(fy - Mathf.FloorToInt(fy));

        Color32 c00 = src[y0 * w + x0], c10 = src[y0 * w + x1];
        Color32 c01 = src[y1 * w + x0], c11 = src[y1 * w + x1];

        float r = Mathf.Lerp(Mathf.Lerp(c00.r, c10.r, tx), Mathf.Lerp(c01.r, c11.r, tx), ty);
        float g = Mathf.Lerp(Mathf.Lerp(c00.g, c10.g, tx), Mathf.Lerp(c01.g, c11.g, tx), ty);
        float b = Mathf.Lerp(Mathf.Lerp(c00.b, c10.b, tx), Mathf.Lerp(c01.b, c11.b, tx), ty);
        float a = Mathf.Lerp(Mathf.Lerp(c00.a, c10.a, tx), Mathf.Lerp(c01.a, c11.a, tx), ty) / 255f;

        return new Color32(
            (byte)(r * a + 255f * (1f - a) + 0.5f),
            (byte)(g * a + 255f * (1f - a) + 0.5f),
            (byte)(b * a + 255f * (1f - a) + 0.5f),
            255);
    }

    /// <summary>
    /// Minimal single-purpose PDF writer: fixed-size pages (given in mm), each
    /// showing one RGB image stretched over the centered printable area.
    /// Writes the PDF byte format directly so no third-party library is needed.
    /// </summary>
    private sealed class PrintPdfWriter : IDisposable
    {
        private const float MmToPt = 72f / 25.4f;

        private readonly FileStream file;
        private readonly Dictionary<int, long> objOffsets = new Dictionary<int, long>();
        private readonly List<int> pageObjIds = new List<int>();
        private readonly float pageW, pageH;           // PDF points
        private readonly float imgX, imgY, imgW, imgH; // PDF points
        private int nextObjId = 3;                     // ids 1 (Catalog) and 2 (Pages) are written on Dispose
        private bool closed;

        public PrintPdfWriter(string path, Vector2 pageMm, Vector2 printableMm)
        {
            pageW = pageMm.x * MmToPt;
            pageH = pageMm.y * MmToPt;
            imgW = printableMm.x * MmToPt;
            imgH = printableMm.y * MmToPt;
            imgX = (pageW - imgW) * 0.5f;
            imgY = (pageH - imgH) * 0.5f;
            file = new FileStream(path, FileMode.Create, FileAccess.Write);
            Ascii("%PDF-1.4\n");
            file.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A }, 0, 6); // high-bit comment marks the file as binary
        }

        public void AddPage(byte[] zlibRgb, int pixelWidth, int pixelHeight)
        {
            int imgId = BeginObj();
            Ascii("<< /Type /XObject /Subtype /Image /Width " + pixelWidth + " /Height " + pixelHeight +
                  " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length " + zlibRgb.Length +
                  " >>\nstream\n");
            file.Write(zlibRgb, 0, zlibRgb.Length);
            Ascii("\nendstream\nendobj\n");

            byte[] content = Encoding.ASCII.GetBytes(
                "q\n" + Pt(imgW) + " 0 0 " + Pt(imgH) + " " + Pt(imgX) + " " + Pt(imgY) + " cm\n/Im0 Do\nQ\n");
            int contentId = BeginObj();
            Ascii("<< /Length " + content.Length + " >>\nstream\n");
            file.Write(content, 0, content.Length);
            Ascii("\nendstream\nendobj\n");

            int pageId = BeginObj();
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + Pt(pageW) + " " + Pt(pageH) +
                  "] /TrimBox [0 0 " + Pt(pageW) + " " + Pt(pageH) +
                  "] /Resources << /XObject << /Im0 " + imgId + " 0 R >> >> /Contents " + contentId + " 0 R >>\nendobj\n");
            pageObjIds.Add(pageId);
        }

        public void Dispose()
        {
            if (closed)
                return;
            closed = true;

            objOffsets[2] = file.Position;
            var kids = new StringBuilder();
            foreach (int id in pageObjIds)
                kids.Append(id).Append(" 0 R ");
            Ascii("2 0 obj\n<< /Type /Pages /Count " + pageObjIds.Count + " /Kids [" + kids + "] >>\nendobj\n");

            objOffsets[1] = file.Position;
            Ascii("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

            long xrefPos = file.Position;
            Ascii("xref\n0 " + nextObjId + "\n0000000000 65535 f \n"); // every xref entry is exactly 20 bytes
            for (int id = 1; id < nextObjId; id++)
                Ascii(objOffsets[id].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
            Ascii("trailer\n<< /Size " + nextObjId + " /Root 1 0 R >>\nstartxref\n" + xrefPos + "\n%%EOF\n");
            file.Dispose();
        }

        /// <summary>
        /// Flattens the RGBA render onto a white background (print has no
        /// alpha), reorders rows top-first as PDF images expect, and
        /// zlib-compresses the raw RGB data for a lossless /FlateDecode image.
        /// </summary>
        public static byte[] FlattenToZlibRgb(Texture2D tex)
        {
            Color32[] pixels = tex.GetPixels32(); // bottom row first
            int w = tex.width;
            int h = tex.height;
            byte[] rgb = new byte[w * h * 3];
            int i = 0;
            for (int y = h - 1; y >= 0; y--)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = pixels[row + x];
                    int a = c.a;
                    int inv = 255 - a;
                    rgb[i++] = (byte)((c.r * a + 255 * inv + 127) / 255);
                    rgb[i++] = (byte)((c.g * a + 255 * inv + 127) / 255);
                    rgb[i++] = (byte)((c.b * a + 255 * inv + 127) / 255);
                }
            }

            using (var ms = new MemoryStream())
            {
                // DeflateStream emits a raw deflate stream; PDF's FlateDecode
                // wants the zlib wrapper, so add the header and Adler-32 here.
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var deflate = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                    deflate.Write(rgb, 0, rgb.Length);
                uint adler = Adler32(rgb);
                ms.WriteByte((byte)(adler >> 24));
                ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8));
                ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            int i = 0;
            int remaining = data.Length;
            while (remaining > 0)
            {
                int n = remaining > 5552 ? 5552 : remaining; // largest block that can't overflow uint
                remaining -= n;
                while (n-- > 0)
                {
                    a += data[i++];
                    b += a;
                }
                a %= Mod;
                b %= Mod;
            }
            return (b << 16) | a;
        }

        private int BeginObj()
        {
            int id = nextObjId++;
            objOffsets[id] = file.Position;
            Ascii(id + " 0 obj\n");
            return id;
        }

        private void Ascii(string s)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(s);
            file.Write(bytes, 0, bytes.Length);
        }

        private static string Pt(float v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}

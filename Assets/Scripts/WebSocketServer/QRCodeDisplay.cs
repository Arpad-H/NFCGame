using System;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using QRCoder;
using System.IO;

public class QRCodeDisplay : MonoBehaviour
{
    public RawImage[] qrSlotImages;
    private string serverIP;

    [Header("QR Appearance")]
    [Tooltip("Colour of the dark modules — the actual QR pattern. This is what carries the code's look.")]
    public Color darkColor = Color.black;

    [Tooltip("Colour of the light modules / background. Set the alpha to 0 for a transparent background so a parchment/frame image behind the RawImage shows through.")]
    public Color lightColor = Color.white;

    [Tooltip("Pixels rendered per QR module. Higher = crisper/bigger texture (more px). 20 was the original default.")]
    [Range(4, 40)]
    public int pixelsPerModule = 20;

    [Tooltip("Draw the quiet-zone border around the code. Keep ON — scanners are far more reliable with it. If OFF, leave visible padding around the RawImage yourself.")]
    public bool drawQuietZones = true;

    public void DisplayQRCodes(LobbyType lobbyType)
    {

        serverIP = GetLocalIP();

        for (int i = 0; i < qrSlotImages.Length; i++)
        {
            // Assign index + 1 as the Player ID (e.g., Player1, Player2)
            string playerID = (i + 1).ToString();
            string lobbyTypeString =  lobbyType.ToString();
            string url = $"nfcgame://connect?ws=ws://{serverIP}:8080/Game?id={playerID}&lobbyType={lobbyTypeString}";;

            Texture2D qrTex = GenerateQR(url, darkColor, lightColor, pixelsPerModule, drawQuietZones);
            qrSlotImages[i].texture = qrTex;

            Debug.Log($"QR for Player {playerID} generated: {url}");
        }
    }

    // Static so other connect screens (e.g. the tutorial's one-player QR) can
    // reuse the exact IP the normal lobby advertises.
    public static string GetLocalIP()
    {
        try
        {
            // This opens a dummy UDP connection. It doesn't actually send data, 
            // but it forces the OS to determine the active local IP routing to the network.
            using (System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                System.Net.IPEndPoint endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"UDP IP fetch failed, falling back to DNS parsing: {e.Message}");
        
            // Fallback: If you are entirely offline, the above might throw.
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    string ipStr = ip.ToString();
                
                    // ONLY ignore the loopback (127.x.x.x). 
                    // Allow 10.x, 192.168.x, and 172.x which are standard private IPs.
                    if (!ipStr.StartsWith("127."))
                    {
                        return ipStr;
                    }
                }
            }

            return "127.0.0.1";
        }
    }

    // Colours default to the original plain black-on-white so existing static
    // callers (e.g. the tutorial connect screen) keep their look unchanged.
    // Pass a lightColor with alpha 0 for a transparent background.
    public static Texture2D GenerateQR(string text, Color? darkColor = null, Color? lightColor = null,
        int pixelsPerModule = 20, bool drawQuietZones = true)
    {
        Color dark = darkColor ?? Color.black;
        Color light = lightColor ?? Color.white;

        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

        // RGBA overload: supports custom colours AND transparency (alpha byte).
        byte[] qrBytes = qrCode.GetGraphic(pixelsPerModule, ToRgba(dark), ToRgba(light), drawQuietZones);

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(qrBytes);        // resizes to the PNG's real dimensions
        tex.filterMode = FilterMode.Point; // crisp module edges, no blur when scaled
        tex.Apply();
        return tex;
    }

    private static byte[] ToRgba(Color c)
    {
        return new byte[]
        {
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f),
        };
    }
}
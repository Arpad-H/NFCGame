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
    
    void Start()
    {
        serverIP = GetLocalIP();
        
        for (int i = 0; i < qrSlotImages.Length; i++)
        {
            // Assign index + 1 as the Player ID (e.g., Player1, Player2)
            string playerID = (i + 1).ToString(); 
            string url = $"nfcgame://connect?ws=ws://{serverIP}:8080/Game?id={playerID}";

            Texture2D qrTex = GenerateQR(url);
            qrSlotImages[i].texture = qrTex;

            Debug.Log($"QR for Player {playerID} generated: {url}");
        }
    }

    string GetLocalIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }

    Texture2D GenerateQR(string text)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

        byte[] qrBytes = qrCode.GetGraphic(20);

        Texture2D tex = new Texture2D(256, 256);
        tex.LoadImage(qrBytes);
        return tex;
    }
}
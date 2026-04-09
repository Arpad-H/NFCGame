using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using QRCoder;
using System.IO;

public class QRCodeDisplay : MonoBehaviour
{
    public RawImage qrImage;

    void Start()
    {
        string ip = GetLocalIP();
        string url = $"nfcgame://connect?ws=ws://{ip}:8080";

        Texture2D qrTex = GenerateQR(url);
        qrImage.texture = qrTex;

        Debug.Log("QR URL: " + url);
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
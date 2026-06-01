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

    public void DisplayQRCodes(LobbyType lobbyType)
    {

        serverIP = GetLocalIP();
        
        for (int i = 0; i < qrSlotImages.Length; i++)
        {
            // Assign index + 1 as the Player ID (e.g., Player1, Player2)
            string playerID = (i + 1).ToString(); 
            string lobbyTypeString =  lobbyType.ToString();
            string url = $"nfcgame://connect?ws=ws://{serverIP}:8080/Game?id={playerID}&lobbyType={lobbyTypeString}";;

            Texture2D qrTex = GenerateQR(url);
            qrSlotImages[i].texture = qrTex;

            Debug.Log($"QR for Player {playerID} generated: {url}");
        }
    }

    string GetLocalIP()
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
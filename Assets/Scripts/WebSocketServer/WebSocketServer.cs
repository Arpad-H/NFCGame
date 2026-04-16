using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class WebSocketServerBehaviour : MonoBehaviour
{
    private WebSocketServer wssv;

    void Start()
    {
        string ip = GetLocalIP();
        int port = 8080;

        wssv = new WebSocketServer($"ws://{ip}:{port}");
        wssv.AddWebSocketService<GameSocket>("/Game");
        wssv.Start();

        Debug.Log($"WebSocket running at ws://{ip}:{port}");
    }

    void OnApplicationQuit()
    {
        wssv.Stop();
    }

    string GetLocalIP()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                string ipStr = ip.ToString();
                // Ignore the VPN/Virtual adapter and the loopback
                if (!ipStr.StartsWith("10.") && !ipStr.StartsWith("127."))
                {
                    return ipStr;
                }
            }
        }
        // Fallback if no 192 address is found
        return "127.0.0.1";
    }
}

public class GameSocket : WebSocketBehavior
{
    public string PlayerID { get; private set; }

    protected override void OnOpen()
    {
        // Capture the ID from the URL: ws://.../Game?id=1
        PlayerID = QueryString["id"];
        
        // If no ID was provided in the URL, give them a guest name
        if (string.IsNullOrEmpty(PlayerID)) {
            PlayerID = "UnknownPlayer";
        }

        Debug.Log($"[Server] {PlayerID} has joined the game. (Session: {ID})");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        // Now we combine the PlayerID with the incoming data
        Debug.Log($"[Message] {PlayerID} says: {e.Data}");

        // Optional: Send a confirmation back to the sender
        // Send($"Server received your message, {PlayerID}!");
    }

    protected override void OnClose(CloseEventArgs e)
    {
        Debug.Log($"[Server] {PlayerID} disconnected.");
    }
}
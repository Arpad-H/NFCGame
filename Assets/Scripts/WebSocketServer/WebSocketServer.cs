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
        wssv.AddWebSocketService<GameSocket>("/");
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
                return ip.ToString();
        }
        return "127.0.0.1";
    }
}

public class GameSocket : WebSocketBehavior
{
    
    public string PlayerID { get; private set; }
    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log("Received: " + e.Data);

        // Example: parse JSON later
        // For now just log
    }

    protected override void OnOpen()
    {
        
        PlayerID = QueryString["id"];
        
        Debug.Log($"[Server] Socket Instance {ID} assigned to Player: {PlayerID}");
    }

    protected override void OnClose(CloseEventArgs e)
    {
        Debug.Log("Client disconnected");
    }
}
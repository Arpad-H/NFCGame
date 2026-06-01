using System;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;

public class WebSocketServerBehaviour : MonoBehaviour
{
    public static WebSocketServerBehaviour Instance;
    private static readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

    // We reference the menu here because this object persists
    public ConnectionMenu currentMenu;
    private GameManager gameManager;
    private WebSocketServer wssv;
    public List<PlayerData> ConnectedPlayers = new List<PlayerData>();
    public ConcurrentDictionary<int, string> PlayerSessions = new ConcurrentDictionary<int, string>();
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        UpdateMenuReference();
    }

    void Start()
    {
        // Only run initialization if this is the original instance
        if (Instance == this)
        {
            string ip = GetLocalIP();
            int port = 8080;

            wssv = new WebSocketServer($"ws://{ip}:{port}");
            wssv.AddWebSocketService<GameSocket>("/Game");
            wssv.Start();
            Debug.Log($"WebSocket running at ws://{ip}:{port}");
        }
    }

    public static void EnqueueAction(Action action) => _executionQueue.Enqueue(action);

    public void UpdateMenuReference()
    {
        currentMenu = FindAnyObjectByType<ConnectionMenu>();
    }

    public void UpdateGameManagerReference(GameManager gm)
    {
        gameManager = gm;
    }

    public void HandlePlayerJoin(int id, string name)
    {
        EnqueueAction(() =>
        {
            // 1. Update the master list
            if (!ConnectedPlayers.Exists(p => p.id == id))
            {
                ConnectedPlayers.Add(new PlayerData(id, name));
            }

            // 2. If a menu exists right now, tell it to update
            if (currentMenu != null)
            {
                currentMenu.RefreshUI();
            }

            CheckAllPlayersConnected();
            
        });
    }

    private void CheckAllPlayersConnected()
    {
        if (ConnectedPlayers.Count == 2 )
        {
            Debug.Log("Both players connected! Starting game...");
           // currentMenu.StartGameWithCountdown();
         

        }
    }

    private void CheckGameStartConditions()
    {
        //TODO checkk that both players have 3 resonances
        if (ConnectedPlayers.Count == 2 && ConnectedPlayers.TrueForAll(p => p.resonances.Count == 3))
        {
            Debug.Log("Both players have selected resonances! Starting game...");
            
            currentMenu.StartGameWithCountdown();
        }
    }

    public void HandlePlayerDisconnect(int id)
    {
        EnqueueAction(() =>
        {
            ConnectedPlayers.RemoveAll(p => p.id == id);
            if (currentMenu != null) currentMenu.RefreshUI();
        });
    }

    public void HandlePlayerElementSelect(int playerId, List<ResonanceType> resonanceTypes)
    {
        EnqueueAction(() =>
        {
            PlayerData player = ConnectedPlayers.Find(p => p.id == playerId);
            if (player != null)
            {
                player.resonances = resonanceTypes;
                Debug.Log($"Player {playerId} selected elements: {string.Join(", ", resonanceTypes)}");
                currentMenu.RefreshUI();
            }
            CheckGameStartConditions();
        });
    }

    public void HandlePlayerPlayCard(int playerId, string cardName )
    {
        EnqueueAction(() =>
        {
            if (gameManager != null)
            {
                Debug.Log($"Player {playerId} played card: {cardName}");
                gameManager.HandlePlayerPlayCard(cardName);
            }

            
        });
    }

    void Update()
    {
        while (_executionQueue.TryDequeue(out var action)) action.Invoke();
    }

    void OnApplicationQuit()
    {
        if (wssv != null)
        {
            wssv.Stop();
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
    public void BroadcastToPlayers(string message)
    {
        if (wssv != null && wssv.WebSocketServices.TryGetServiceHost("/Game", out var host))
        {
            // This sends the message to EVERYONE connected to /Game
            host.Sessions.Broadcast(message);
        }
    }

    public void SendToPlayer(int playerId, string message)
    {
        if (PlayerSessions.TryGetValue(playerId, out string sessionId))
        {
            if (wssv != null && wssv.WebSocketServices.TryGetServiceHost("/Game", out var host))
            {
                // SendTo takes the string message and the string Session ID
                host.Sessions.SendTo(message, sessionId);
            }
        }
        else
        {
            Debug.LogWarning($"[Server] Cannot send message, no active session for Player {playerId}");
        }
    }
    
}

public class GameSocket : WebSocketBehavior
{
    public int PlayerID { get; private set; }

    protected override void OnOpen()
    {
        PlayerID = int.TryParse(QueryString["id"], out int result) ? result : 0;
        WebSocketServerBehaviour.Instance.PlayerSessions[PlayerID] = this.ID;
        string playerName = QueryString["name"];
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = $"Mage {PlayerID}";
        }

        // Dispatch to main thread
        WebSocketServerBehaviour.EnqueueAction(() =>
        {
            if (WebSocketServerBehaviour.Instance.currentMenu != null)
            {
                WebSocketServerBehaviour.Instance.HandlePlayerJoin(PlayerID, playerName);
            }
        });
        
        Debug.Log($"[Server] {playerName} (ID: {PlayerID}) joined.");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        Debug.Log($"[Message] {PlayerID} says: {e.Data}");

        if (e.Data.StartsWith("SELECT_ELEMENTS:"))
        {
            string elementsPart = e.Data.Substring("SELECT_ELEMENTS:".Length);
            List<ResonanceType> selectedElements = new List<ResonanceType>();

            foreach (var elem in elementsPart.Split(','))
            {
                if (Enum.TryParse(elem.Trim(), out ResonanceType resonance))
                {
                    selectedElements.Add(resonance);
                }
            }

            WebSocketServerBehaviour.EnqueueAction(() =>
            {
                if (WebSocketServerBehaviour.Instance.currentMenu != null)
                {
                    WebSocketServerBehaviour.Instance.HandlePlayerElementSelect(PlayerID, selectedElements);
                }
            });
        }
        else if (e.Data.StartsWith("PLAY_CARD:"))
        {
            string cardName = e.Data.Substring("PLAY_CARD:".Length);
            if (cardName.Trim() == "")
            {
                Debug.LogWarning($"Invalid card ID received from Player {PlayerID}: {e.Data}");
                return;
            }
            WebSocketServerBehaviour.EnqueueAction(() =>
            {
                    WebSocketServerBehaviour.Instance.HandlePlayerPlayCard(PlayerID, cardName);
            });
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        WebSocketServerBehaviour.Instance.PlayerSessions.TryRemove(PlayerID, out _);
        WebSocketServerBehaviour.EnqueueAction(() =>
        {
            if (WebSocketServerBehaviour.Instance.currentMenu != null)
            {
                WebSocketServerBehaviour.Instance.HandlePlayerDisconnect(PlayerID);
            }
        });
        Debug.Log($"[Server] {PlayerID} disconnected.");
    }
    
}
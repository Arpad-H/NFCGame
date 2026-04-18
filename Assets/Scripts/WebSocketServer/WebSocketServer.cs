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
    private Board gameBoard;
    private WebSocketServer wssv;
    public List<PlayerData> ConnectedPlayers = new List<PlayerData>();

    void Awake()
    {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
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

    public void UpdateMenuReference() {
        currentMenu = FindAnyObjectByType<ConnectionMenu>();
    }
    public void UpdateGameBoardReference(Board board) {
        gameBoard = board;
    }
    public void HandlePlayerJoin(int id, string name)
    {
        EnqueueAction(() => {
            // 1. Update the master list
            if (!ConnectedPlayers.Exists(p => p.id == id)) {
                ConnectedPlayers.Add(new PlayerData(id, name));
            }

            // 2. If a menu exists right now, tell it to update
            if (currentMenu != null) {
                currentMenu.RefreshUI();
            }

            CheckGameStartConditions();
        });
    }
    private void CheckGameStartConditions() {
        if (ConnectedPlayers.Count >= 2) {
            Debug.Log("Both players connected! Starting game...");
            currentMenu.StartGameWithCountdown();
        }
    }
    public void HandlePlayerDisconnect(int id)
    {
        EnqueueAction(() => {
            ConnectedPlayers.RemoveAll(p => p.id == id);
            if (currentMenu != null) currentMenu.RefreshUI();
        });
    }
    public void HandlePlayerElementSelect(int playerId, List<ResonanceType> resonanceTypes)
    {
        EnqueueAction(() => {
            PlayerData player = ConnectedPlayers.Find(p => p.id == playerId);
            if (player != null) {
                player.resonances = resonanceTypes;
                Debug.Log($"Player {playerId} selected elements: {string.Join(", ", resonanceTypes)}");
                currentMenu.RefreshUI();
            }
        });
    }
    
    void Update() {
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
    public int PlayerID { get; private set; }
    protected override void OnOpen()
    {
        PlayerID =int.TryParse(QueryString["id"], out int result) ? result : 0;
        string playerName = QueryString["name"];
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = $"Mage {PlayerID}";
        }

        // Dispatch to main thread
        WebSocketServerBehaviour.EnqueueAction(() => {
            if (WebSocketServerBehaviour.Instance.currentMenu != null) {
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
            WebSocketServerBehaviour.EnqueueAction(() => {
                if (WebSocketServerBehaviour.Instance.currentMenu != null) {
                    WebSocketServerBehaviour.Instance.HandlePlayerElementSelect(PlayerID, selectedElements);
                }
            });
        }
        else if (e.Data.StartsWith("PLAY_CARD:"))
        {
            string cardID = e.Data.Substring("PLAY_CARD:".Length);
            //TODO: Handle card play logic, e.g. WebSocketServerBehaviour.Instance.HandlePlayerPlayCard(PlayerID, cardID);
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        WebSocketServerBehaviour.EnqueueAction(() => {
            if (WebSocketServerBehaviour.Instance.currentMenu != null) {
                WebSocketServerBehaviour.Instance.HandlePlayerDisconnect(PlayerID);
            }
        });
        Debug.Log($"[Server] {PlayerID} disconnected.");
    }
}
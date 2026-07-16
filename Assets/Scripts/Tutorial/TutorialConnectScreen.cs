using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Riftborn.Tutorial
{
    // The tutorial's one-player onboarding, shown over the main menu: one QR
    // (player id 1, BLIND_PICK deep link — the app's normal lobby path), a
    // status line, and a countdown into TutorialScene once that player is
    // connected with 3 resonances picked. The board ignores the picks (the
    // bootstrap forces Death/Holy/Plague), but the player is told to pick
    // exactly those so the app side agrees with the board no matter how the
    // app uses them.
    //
    // Deliberately zero server changes: GameSocket/HandlePlayerJoin already
    // maintain WebSocketServerBehaviour.ConnectedPlayers, and the 2-player
    // auto-start (CheckGameStartConditions) can't fire with one player — so
    // this screen just polls the roster.
    //
    // The panel is authored on the menu canvas in MainMenu.unity and shown /
    // hidden like ConnectionMenu, so its look belongs to the scene: this script
    // only drives the status text, the QR texture and the countdown. Nothing
    // here assumes a layout — style the hierarchy freely, keep the wiring.
    public class TutorialConnectScreen : MonoBehaviour
    {
        public int countdownSeconds = 5;

        [Header("Wiring")]
        [Tooltip("Status line — cycles through 'Waiting for your phone…', the resonance prompt, then the countdown.")]
        [SerializeField] private TMP_Text statusLabel;

        [Tooltip("The generated QR texture is written to this RawImage each time the screen opens.")]
        [SerializeField] private RawImage qrImage;

        [Tooltip("Optional. The editor/dev-only 'Start without app' button — shown only in the editor and " +
                 "development builds, hidden automatically in release. Wire its onClick to StartWithoutApp().")]
        [SerializeField] private GameObject devSkipButton;

        // Mirrors the lobby QR's knobs (QRCodeDisplay), so the tutorial code can be
        // styled to match it. Defaults are GenerateQR's own: plain black on white.
        [Header("QR appearance")]
        [Tooltip("Colour of the dark modules — the actual QR pattern. This is what carries the code's look.")]
        [SerializeField] private Color qrDarkColor = Color.black;

        [Tooltip("Colour of the light modules / background. Set the alpha to 0 for a transparent background " +
                 "so art behind the RawImage shows through.")]
        [SerializeField] private Color qrLightColor = Color.white;

        [Tooltip("Pixels rendered per QR module. Higher = crisper/bigger texture.")]
        [Range(4, 40)]
        [SerializeField] private int qrPixelsPerModule = 20;

        [Tooltip("Draw the quiet-zone border around the code. Keep ON — scanners are far more reliable with " +
                 "it. If OFF, leave visible padding around the RawImage yourself.")]
        [SerializeField] private bool qrDrawQuietZones = true;

        private Coroutine countdown;

        // Set by Show() before it activates the object, so the Awake fired by
        // that SetActive(true) can tell a real open apart from scene load.
        private bool showing;

        private void Awake()
        {
            if (statusLabel == null || qrImage == null)
            {
                Debug.LogError("[Tutorial] TutorialConnectScreen is missing its Status Label / QR Image " +
                               "wiring — the connect screen can't run.", this);
            }

            // The panel is authored active so it can be styled in the editor;
            // hide it before the first frame draws. Skipped when Show() is what
            // woke it, or it would immediately undo itself.
            if (!showing) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // Loading TutorialScene destroys this screen (it lives in the menu
            // scene). The player is already in ConnectedPlayers by then, so drop
            // the flag; the tutorial match runs with no lobby listening, like a
            // normal GameScene.
            SetLobbyOpen(false);
        }

        public void Show()
        {
            showing = true;
            gameObject.SetActive(true);

            if (devSkipButton != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                devSkipButton.SetActive(true);
#else
                devSkipButton.SetActive(false);
#endif
            }

            // Register as a menu-less lobby so the socket registers the join and
            // resonance pick into ConnectedPlayers (otherwise gated on the real
            // ConnectionMenu, which this flow never opens).
            SetLobbyOpen(true);

            string ip = QRCodeDisplay.GetLocalIP();
            string url = $"nfcgame://connect?ws=ws://{ip}:8080/Game?id=1&lobbyType={LobbyType.BLIND_PICK}";
            if (qrImage != null)
            {
                qrImage.texture = QRCodeDisplay.GenerateQR(
                    url, qrDarkColor, qrLightColor, qrPixelsPerModule, qrDrawQuietZones);
            }
            Debug.Log($"[Tutorial] Connect QR: {url}");

            SetStatus("Waiting for your phone…");
        }

        // Wire the Cancel button's onClick here.
        public void Hide()
        {
            if (countdown != null)
            {
                StopCoroutine(countdown);
                countdown = null;
            }

            SetLobbyOpen(false);
            showing = false;
            gameObject.SetActive(false);
        }

        // Wire the dev "Start without app" button's onClick here: skips the phone
        // entirely, and the debug overlay drives the match once the scene is up.
        public void StartWithoutApp()
        {
            SceneManager.LoadScene(TutorialLauncher.SceneName);
        }

        private void Update()
        {
            if (countdown != null) return; // the countdown owns the status line

            PlayerData player = FindHumanPlayer();
            if (player == null || !player.isConnected)
            {
                SetStatus(WebSocketServerBehaviour.Instance == null
                    ? "No connection server is running — restart the game."
                    : "Waiting for your phone…");
            }
            else if (player.resonances == null || player.resonances.Count < 3)
            {
                SetStatus("Now pick your resonances in the app:\nDEATH, HOLY and PLAGUE");
            }
            else
            {
                countdown = StartCoroutine(CountdownAndLoad());
            }
        }

        private static PlayerData FindHumanPlayer()
        {
            return WebSocketServerBehaviour.Instance == null
                ? null
                : WebSocketServerBehaviour.Instance.ConnectedPlayers.Find(p => p.id == 1);
        }

        private IEnumerator CountdownAndLoad()
        {
            for (int t = countdownSeconds; t > 0; t--)
            {
                SetStatus($"Starting in {t}…");
                yield return new WaitForSeconds(1f);

                PlayerData player = FindHumanPlayer();
                if (player == null || !player.isConnected)
                {
                    countdown = null; // dropped mid-countdown — back to waiting
                    yield break;
                }
            }

            // No INITIATE_GAME_STATE here: TutorialBootstrap broadcasts it once
            // the scene is up, so the app gets exactly one signal.
            SceneManager.LoadScene(TutorialLauncher.SceneName);
        }

        private static void SetLobbyOpen(bool open)
        {
            if (WebSocketServerBehaviour.Instance != null)
            {
                WebSocketServerBehaviour.Instance.acceptLobbyConnections = open;
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }
    }
}

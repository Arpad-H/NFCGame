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
    // this screen just polls the roster. Self-built grey-box UI (Announcer
    // pattern), own canvas WITH a raycaster (the menu scene's EventSystem
    // feeds the buttons); the opaque backdrop doubles as a click-blocker for
    // the menu behind it.
    public class TutorialConnectScreen : MonoBehaviour
    {
        public int countdownSeconds = 5;

        private TMP_Text statusLabel;
        private RawImage qrImage;
        private Coroutine countdown;

        private void Awake()
        {
            BuildUi();

            // Register as a menu-less lobby so the socket registers the join and
            // resonance pick into ConnectedPlayers (otherwise gated on the real
            // ConnectionMenu, which this flow never opens).
            if (WebSocketServerBehaviour.Instance != null)
            {
                WebSocketServerBehaviour.Instance.acceptLobbyConnections = true;
            }

            string ip = QRCodeDisplay.GetLocalIP();
            string url = $"nfcgame://connect?ws=ws://{ip}:8080/Game?id=1&lobbyType={LobbyType.BLIND_PICK}";
            qrImage.texture = QRCodeDisplay.GenerateQR(url);
            Debug.Log($"[Tutorial] Connect QR: {url}");
        }

        private void OnDestroy()
        {
            // Loading TutorialScene destroys this screen (it lives in the menu
            // scene). The player is already in ConnectedPlayers by then, so drop
            // the flag; the tutorial match runs with no lobby listening, like a
            // normal GameScene.
            if (WebSocketServerBehaviour.Instance != null)
            {
                WebSocketServerBehaviour.Instance.acceptLobbyConnections = false;
            }
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
                SetStatus($"Connected: {player.name}\nNow pick your resonances in the app:\nDEATH, HOLY and PLAGUE");
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

        private void SetStatus(string text)
        {
            statusLabel.text = text;
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        // ── Grey-box UI ──────────────────────────────────────────────────────

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Opaque-ish backdrop; raycast target so the menu underneath is dead.
            var backdrop = CreateChild<Image>(transform, "Backdrop");
            Stretch((RectTransform)backdrop.transform);
            backdrop.color = new Color(0.03f, 0.04f, 0.06f, 0.93f);
            backdrop.raycastTarget = true;

            var panel = CreateChild<Image>(transform, "Panel");
            panel.color = new Color(0.08f, 0.09f, 0.12f, 0.97f);
            panel.raycastTarget = false;
            var panelRect = (RectTransform)panel.transform;
            panelRect.sizeDelta = new Vector2(760f, 100f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 28, 32);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(panelRect, "Title", "TUTORIAL", 52f, FontStyles.Bold);
            CreateLabel(panelRect, "Instructions",
                "Scan the code with the companion app on your phone.\n" +
                "When the app asks for your resonances, pick DEATH, HOLY and PLAGUE.",
                27f, FontStyles.Normal);

            qrImage = CreateChild<RawImage>(panelRect, "QR");
            var qrElement = qrImage.gameObject.AddComponent<LayoutElement>();
            qrElement.preferredWidth = 360f;
            qrElement.preferredHeight = 360f;
            qrImage.raycastTarget = false;

            statusLabel = CreateLabel(panelRect, "Status", "Waiting for your phone…", 27f, FontStyles.Bold);
            statusLabel.color = new Color(1f, 0.84f, 0.29f);

            var buttonRow = new GameObject("Buttons", typeof(RectTransform)).transform;
            buttonRow.SetParent(panelRect, false);
            var rowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;

            CreateButton((RectTransform)buttonRow, "Cancel", Close);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Editor/dev iteration without a phone: the debug overlay drives the
            // match once the scene is up.
            CreateButton((RectTransform)buttonRow, "Start without app (dev)",
                () => SceneManager.LoadScene(TutorialLauncher.SceneName), 340f);
#endif
        }

        private static T CreateChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static TMP_Text CreateLabel(RectTransform parent, string name, string text,
            float size, FontStyles style)
        {
            var label = CreateChild<TextMeshProUGUI>(parent, name);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 660f;
            return label;
        }

        private static void CreateButton(RectTransform parent, string caption,
            UnityEngine.Events.UnityAction onClick, float width = 220f)
        {
            var image = CreateChild<Image>(parent, $"Button {caption}");
            image.color = new Color(0.22f, 0.24f, 0.30f);
            var rect = (RectTransform)image.transform;
            rect.sizeDelta = new Vector2(width, 58f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var label = CreateChild<TextMeshProUGUI>(rect, "Label");
            Stretch((RectTransform)label.transform);
            label.text = caption;
            label.fontSize = 26f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

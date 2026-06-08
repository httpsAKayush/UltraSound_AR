using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetaXR.LofiStudy.ARFoundation
{
    /// <summary>
    /// Builds and manages the MicrUs control panels on both sides of the feed screen.
    /// Sends UDP commands to the PC command receiver when buttons are pressed.
    ///
    /// Setup:
    ///   1. Add this script to the root empty object (same as CameraFeedSpawner).
    ///   2. Set PC IP and command port (default 5001).
    ///   3. The panels are created automatically when the feed screen spawns.
    ///   4. Call BuildControlPanels(feedScreenRoot) from CameraFeedSpawner after spawning.
    /// </summary>
    public class MicrusControlPanel : MonoBehaviour
    {
        [Header("Network")]
        [Tooltip("PC IP address — same as CameraFeedReceiver server IP.")]
        public string pcIP         = "192.168.x.x";
        public int    commandPort  = 5001;

        [Header("Panel Appearance")]
        public Color  panelBgColor    = new Color(0.15f, 0.15f, 0.15f, 0.92f);
        public Color  buttonColor     = new Color(0.25f, 0.25f, 0.28f, 1f);
        public Color  buttonHighlight = new Color(0.35f, 0.55f, 0.80f, 1f);
        public Color  labelColor      = Color.white;
        public float  fontSize        = 14f;

        // UDP client (fire and forget)
        UdpClient m_Udp;
        IPEndPoint m_Endpoint;

        void Awake()
        {
            m_Udp      = new UdpClient();
            m_Endpoint = new IPEndPoint(IPAddress.Parse(pcIP), commandPort);
        }

        void OnDestroy()
        {
            m_Udp?.Close();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Call this after spawning the feed screen to attach control panels to it.
        /// feedRoot is the root empty object of the feed prefab.
        /// </summary>
        public void BuildControlPanels(GameObject feedRoot)
        {
            // Feed quad child scale is (1.60, 1, 1), root scale (0.5,0.5,0.5) or (1,1,1)
            // Left panel sits at local X = -(quadScaleX/2 + panelHalfWidth)
            // Right panel sits at local X = +(quadScaleX/2 + panelHalfWidth)
            float quadX      = 1.60f;   // Quad child X scale (matches aspect ratio)
            float panelW     = 0.55f;   // panel width in local units
            float panelH     = 1.0f;    // panel height in local units
            float offsetX    = (quadX / 2f) + (panelW / 2f) + 0.02f; // small gap

            BuildLeftPanel (feedRoot, new Vector3(-offsetX, 0f, 0f), panelW, panelH);
            BuildRightPanel(feedRoot, new Vector3( offsetX, 0f, 0f), panelW, panelH);
        }

        // ── Panel builders ───────────────────────────────────────────────────────

        void BuildLeftPanel(GameObject parent, Vector3 localPos, float w, float h)
        {
            var panel = CreatePanel(parent, "LeftControlPanel", localPos, w, h);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing            = 4f;
            layout.padding            = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = false;
            layout.childControlWidth  = true;
            layout.childForceExpandHeight = false;

            float btnH = 0.07f; // button height in layout units

            AddLabel(panel,  "── FOCUS ──");
            AddButtonRow(panel, "focus_dec", "< Focus",  "focus_inc", "Focus >");

            AddLabel(panel,  "── DEPTH ──");
            AddButtonRow(panel, "depth_dec", "< Depth",  "depth_inc", "Depth >");

            AddLabel(panel,  "── GAIN ──");
            AddButtonRow(panel, "gain_dec",  "< Gain",   "gain_inc",  "Gain >");

            AddLabel(panel,  "── DYN RANGE ──");
            AddButtonRow(panel, "dynrange_dec", "< DR",  "dynrange_inc", "DR >");

            AddLabel(panel,  "── POWER ──");
            AddButtonRow(panel, "power_dec", "< Pwr",   "power_inc",  "Pwr >");

            AddLabel(panel,  "── FREQUENCY ──");
            AddButtonRow(panel, "freq_dec",  "< Freq",  "freq_inc",   "Freq >");

            AddLabel(panel,  "── ANGLE ──");
            AddButtonRow(panel, "angle_dec", "< Angle", "angle_inc",  "Angle >");

            AddSingleButton(panel, "scan_dir", "Scan Direction");
        }

        void BuildRightPanel(GameObject parent, Vector3 localPos, float w, float h)
        {
            var panel = CreatePanel(parent, "RightControlPanel", localPos, w, h);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing            = 4f;
            layout.padding            = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = false;
            layout.childControlWidth  = true;
            layout.childForceExpandHeight = false;

            AddLabel(panel, "── F KEYS ──");

            // F1-F6 row
            AddFKeyRow(panel, 1, 6);
            // F7-F12 row
            AddFKeyRow(panel, 7, 12);

            AddLabel(panel, "── MEASURE ──");
            AddButtonRow(panel, "distance",  "Distance", "length",    "Length");
            AddButtonRow(panel, "area",      "Area",     "trace",     "Trace");
            AddButtonRow(panel, "angle",     "Angle",    "angle2",    "Angle2");
            AddButtonRow(panel, "volume",    "Volume",   "volume2",   "Vol2");
            AddButtonRow(panel, "stenosis",  "Sten%",    "stenosis2", "Sten2");
            AddButtonRow(panel, "ab_ratio",  "A/B",      "ab_ratio2", "A/B2");

            AddLabel(panel, "── CONTROL ──");
            AddSingleButton(panel, "freeze", "❄ FREEZE");

            AddLabel(panel, "── MODES ──");
            // Mode buttons 1-5
            AddModeRow(panel, 1, 5);
            // Mode buttons 6-9
            AddModeRow(panel, 6, 9);
        }

        // ── UI helpers ───────────────────────────────────────────────────────────

        // GameObject CreatePanel(GameObject parent, string name, Vector3 localPos, float w, float h)
        // {
        //     var go  = new GameObject(name);
        //     go.transform.SetParent(parent.transform, false);
        //     go.transform.localPosition = localPos;
        //     // go.transform.localRotation = Quaternion.identity;
        //     // After: go.transform.localRotation = Quaternion.identity;
        //     //Change to:
        //     go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        //     go.transform.localScale    = Vector3.one;

        //     // Canvas
        //     var canvas = go.AddComponent<Canvas>();
        //     canvas.renderMode = RenderMode.WorldSpace;
        //     var rt = go.GetComponent<RectTransform>();
        //     rt.sizeDelta = new Vector2(w * 1000f, h * 1000f); // canvas units
        //     rt.localScale = new Vector3(0.001f, 0.001f, 0.001f); // 1 canvas unit = 1mm

        //     // go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        //     // Replace with:
        //     go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        //     // Background image
        //     var bg = new GameObject("Background");
        //     bg.transform.SetParent(go.transform, false);
        //     var bgRect = bg.AddComponent<RectTransform>();
        //     bgRect.anchorMin = Vector2.zero;
        //     bgRect.anchorMax = Vector2.one;
        //     bgRect.offsetMin = Vector2.zero;
        //     bgRect.offsetMax = Vector2.zero;
        //     var bgImg = bg.AddComponent<Image>();
        //     bgImg.color = panelBgColor;

        //     return go;
        // }
        GameObject CreatePanel(GameObject parent, string name, Vector3 localPos, float w, float h)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale    = Vector3.one;

            // Put panel on UI layer so grab collider doesn't interfere
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // CRITICAL: assign the camera
            canvas.worldCamera = Camera.main;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w * 1000f, h * 1000f);
            rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            // TrackedDeviceGraphicRaycaster for XR ray
            var raycaster = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor3DOcclusion = false; // don't let 3D colliders block it

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            bg.layer = LayerMask.NameToLayer("UI");
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = panelBgColor;

            return go;
        }
        void AddLabel(GameObject panel, string text)
        {
            var go  = new GameObject("Label_" + text);
            go.transform.SetParent(panel.transform, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize - 2f;
            tmp.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
        }

        void AddButtonRow(GameObject panel,
                          string cmd1, string label1,
                          string cmd2, string label2)
        {
            var row = new GameObject("Row_" + cmd1);
            row.transform.SetParent(panel.transform, false);
            var rt  = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 42);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing            = 4f;
            hlg.childControlWidth  = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            CreateButton(row, cmd1, label1);
            CreateButton(row, cmd2, label2);
        }

        void AddSingleButton(GameObject panel, string cmd, string label)
        {
            var row = new GameObject("Row_" + cmd);
            row.transform.SetParent(panel.transform, false);
            var rt  = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 42);
            CreateButton(row, cmd, label);
        }

        void AddFKeyRow(GameObject panel, int from, int to)
        {
            var row = new GameObject($"FKeyRow_{from}_{to}");
            row.transform.SetParent(panel.transform, false);
            var rt  = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 42);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing           = 3f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            for (int i = from; i <= to; i++)
                CreateButton(row, $"f{i}", $"F{i}");
        }

        void AddModeRow(GameObject panel, int from, int to)
        {
            var row = new GameObject($"ModeRow_{from}_{to}");
            row.transform.SetParent(panel.transform, false);
            var rt  = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 42);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing           = 3f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            for (int i = from; i <= to; i++)
                CreateButton(row, $"mode{i}", $"{i}");
        }

        void CreateButton(GameObject parent, string command, string label)
        {
            var go  = new GameObject("Btn_" + command);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = buttonColor;

            var btn = go.AddComponent<Button>();

            // Highlight colors
            var colors        = btn.colors;
            colors.normalColor    = buttonColor;
            colors.highlightedColor = buttonHighlight;
            colors.pressedColor   = new Color(0.15f, 0.35f, 0.60f, 1f);
            colors.selectedColor  = buttonHighlight;
            btn.colors = colors;

            // Label
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(2, 2);
            txtRt.offsetMax = new Vector2(-2, -2);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = fontSize;
            tmp.color     = labelColor;
            tmp.alignment = TextAlignmentOptions.Center;

            // Click handler — capture command in closure
            // string cmd = command;
            // btn.onClick.AddListener(() => SendCommand(cmd));
            string cmd = command;
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[BUTTON CLICKED] {cmd}");
                SendCommand(cmd);
            });
        }

        // ── Network ──────────────────────────────────────────────────────────────

        void SendCommand(string command)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(command);
                m_Udp.Send(data, data.Length, m_Endpoint);
                Debug.Log($"[MicrusControlPanel] Sent command: {command}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicrusControlPanel] Failed to send '{command}': {e.Message}");
            }
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MetaXR.LofiStudy.ARFoundation
{
    public class MicrusControlPanel : MonoBehaviour
    {
        [Header("Network")]
        public string pcIP        = "192.168.x.x";
        public int    commandPort = 5001;

        [Header("Panel Appearance")]
        public Color panelBgColor    = new Color(0.15f, 0.15f, 0.15f, 0.92f);
        public Color buttonColor     = new Color(0.25f, 0.25f, 0.28f, 1f);
        public Color buttonHighlight = new Color(0.35f, 0.55f, 0.80f, 1f);
        public Color scaleButtonColor = new Color(0.20f, 0.45f, 0.20f, 1f);   // green — visually distinct
        public Color scaleButtonHighlight = new Color(0.30f, 0.65f, 0.30f, 1f);
        public Color labelColor      = Color.white;
        public float fontSize        = 14f;

        [Header("Scale Settings")]
        public float scaleStep    = 0.1f;   // how much to scale per button press
        public float scaleMin     = 0.3f;   // minimum screen scale
        public float scaleMax     = 3.0f;   // maximum screen scale

        // References set by BuildControlPanels
        GameObject m_FeedRoot;
        GameObject m_LeftPanel;
        GameObject m_RightPanel;
        float      m_QuadX    = 1.60f;
        float      m_PanelW   = 0.55f;

        // UDP
        UdpClient  m_Udp;
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

        public void BuildControlPanels(GameObject feedRoot)
        {
            m_FeedRoot = feedRoot;

            float offsetX = (m_QuadX / 2f) + (m_PanelW / 2f) + 0.02f;

            m_LeftPanel  = BuildLeftPanel (feedRoot, new Vector3(-offsetX, 0f, 0.01f), m_PanelW, 1.0f);
            m_RightPanel = BuildRightPanel(feedRoot, new Vector3( offsetX, 0f, 0.01f), m_PanelW, 1.0f);
        }

        // ── Scale controls ───────────────────────────────────────────────────────

        void ScaleScreen(float delta)
        {
            if (m_FeedRoot == null) return;

            var current = m_FeedRoot.transform.localScale;
            float newScale = Mathf.Clamp(current.x + delta, scaleMin, scaleMax);
            m_FeedRoot.transform.localScale = new Vector3(newScale, newScale, newScale);

            // Reposition panels to stay at screen edges
            RepositionPanels(newScale);
        }

        void RepositionPanels(float rootScale)
        {
            // Panel local position is relative to root, so we divide by root scale
            // to keep panels at a fixed world-space offset from screen edge
            float worldEdge  = (m_QuadX * rootScale) / 2f;
            float panelOffset = worldEdge / rootScale + m_PanelW / 2f + 0.02f;

            if (m_LeftPanel  != null)
                m_LeftPanel.transform.localPosition  = new Vector3(-panelOffset, 0f, 0.01f);
            if (m_RightPanel != null)
                m_RightPanel.transform.localPosition = new Vector3( panelOffset, 0f, 0.01f);
        }

        // ── Panel builders ───────────────────────────────────────────────────────

        GameObject BuildLeftPanel(GameObject parent, Vector3 localPos, float w, float h)
        {
            var panel = CreatePanel(parent, "LeftControlPanel", localPos, w, h);
            var layout = AddVerticalLayout(panel);

            AddScaleButtons(panel);   // scale buttons at top — visually distinct

            AddLabel(panel, "── FOCUS ──");
            AddButtonRow(panel, "focus_dec", "< Focus", "focus_inc", "Focus >");

            AddLabel(panel, "── DEPTH ──");
            AddButtonRow(panel, "depth_dec", "< Depth", "depth_inc", "Depth >");

            AddLabel(panel, "── GAIN ──");
            AddButtonRow(panel, "gain_dec", "< Gain", "gain_inc", "Gain >");

            AddLabel(panel, "── DYN RANGE ──");
            AddButtonRow(panel, "dynrange_dec", "< DR", "dynrange_inc", "DR >");

            AddLabel(panel, "── POWER ──");
            AddButtonRow(panel, "power_dec", "< Pwr", "power_inc", "Pwr >");

            AddLabel(panel, "── FREQUENCY ──");
            AddButtonRow(panel, "freq_dec", "< Freq", "freq_inc", "Freq >");

            AddLabel(panel, "── ANGLE ──");
            AddButtonRow(panel, "angle_dec", "< Angle", "angle_inc", "Angle >");

            AddSingleButton(panel, "scan_dir", "Scan Direction");

            return panel;
        }

        GameObject BuildRightPanel(GameObject parent, Vector3 localPos, float w, float h)
        {
            var panel = CreatePanel(parent, "RightControlPanel", localPos, w, h);
            var layout = AddVerticalLayout(panel);

            AddLabel(panel, "── F KEYS ──");
            AddFKeyRow(panel, 1, 6);
            AddFKeyRow(panel, 7, 12);

            AddLabel(panel, "── MEASURE ──");
            AddButtonRow(panel, "distance", "Distance", "length",    "Length");
            AddButtonRow(panel, "area",     "Area",     "trace",     "Trace");
            AddButtonRow(panel, "angle",    "Angle",    "angle2",    "Angle2");
            AddButtonRow(panel, "volume",   "Volume",   "volume2",   "Vol2");
            AddButtonRow(panel, "stenosis", "Sten%",    "stenosis2", "Sten2");
            AddButtonRow(panel, "ab_ratio", "A/B",      "ab_ratio2", "A/B2");

            AddLabel(panel, "── CONTROL ──");
            AddSingleButton(panel, "freeze", "❄ FREEZE");

            AddLabel(panel, "── MODES ──");
            AddModeRow(panel, 1, 5);
            AddModeRow(panel, 6, 9);

            return panel;
        }

        // ── Scale button row ─────────────────────────────────────────────────────

        void AddScaleButtons(GameObject panel)
        {
            // Separator label
            AddLabel(panel, "── SCREEN SIZE ──");

            var row = new GameObject("Row_Scale");
            row.transform.SetParent(panel.transform, false);
            var rt  = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 50);   // slightly taller than normal buttons
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing           = 6f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;

            CreateScaleButton(row, -scaleStep, "▼  Shrink");
            CreateScaleButton(row, +scaleStep, "▲  Grow");
        }

        void CreateScaleButton(GameObject parent, float delta, string label)
        {
            var go  = new GameObject("ScaleBtn_" + label);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = scaleButtonColor;

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = scaleButtonColor;
            colors.highlightedColor = scaleButtonHighlight;
            colors.pressedColor     = new Color(0.15f, 0.50f, 0.15f, 1f);
            btn.colors = colors;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(2, 2);
            txtRt.offsetMax = new Vector2(-2, -2);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = fontSize + 2f;   // slightly bigger text for scale buttons
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            float d = delta;
            btn.onClick.AddListener(() => ScaleScreen(d));
        }

        // ── UI helpers ───────────────────────────────────────────────────────────

        VerticalLayoutGroup AddVerticalLayout(GameObject panel)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing               = 4f;
            layout.padding               = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight    = false;
            layout.childControlWidth     = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        GameObject CreatePanel(GameObject parent, string name, Vector3 localPos, float w, float h)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta  = new Vector2(w * 1000f, h * 1000f);
            rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            var raycaster = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor3DOcclusion = false;

            var bg     = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg  = bg.AddComponent<Image>();
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

        void AddButtonRow(GameObject panel, string cmd1, string label1, string cmd2, string label2)
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
            hlg.spacing            = 3f;
            hlg.childControlWidth  = true;
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
            hlg.spacing            = 3f;
            hlg.childControlWidth  = true;
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

            var img    = go.AddComponent<Image>();
            img.color  = buttonColor;

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = buttonColor;
            colors.highlightedColor = buttonHighlight;
            colors.pressedColor     = new Color(0.15f, 0.35f, 0.60f, 1f);
            btn.colors = colors;

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
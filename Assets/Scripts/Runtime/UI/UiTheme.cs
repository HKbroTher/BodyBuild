using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>IMGUI styles for the gritty underground look. Must be built from inside OnGUI.</summary>
    public sealed class UiTheme
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        public static readonly Color Accent = new Color(0.95f, 0.15f, 0.1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.2f);
        public static readonly Color Muted = new Color(0.6f, 0.6f, 0.6f);
        public static readonly Color Research = new Color(0.3f, 0.9f, 1f);

        public Texture2D White { get; private set; }
        public GUIStyle Label { get; private set; }
        public GUIStyle Small { get; private set; }
        public GUIStyle Header { get; private set; }
        public GUIStyle Big { get; private set; }
        public GUIStyle Center { get; private set; }
        public GUIStyle Floating { get; private set; }
        public GUIStyle Button { get; private set; }
        public GUIStyle BigButton { get; private set; }
        public GUIStyle ListItem { get; private set; }
        public GUIStyle ListItemSelected { get; private set; }
        public GUIStyle Tooltip { get; private set; }

        public bool IsBuilt => White != null;

        public void Build()
        {
            White = Solid(Color.white);

            Label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, richText = true };
            Label.normal.textColor = new Color(0.9f, 0.9f, 0.88f);
            Small = new GUIStyle(Label) { fontSize = 16 };
            Small.normal.textColor = new Color(0.75f, 0.75f, 0.72f);
            Header = new GUIStyle(Label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = false };
            Big = new GUIStyle(Label) { fontSize = 44, fontStyle = FontStyle.Bold, wordWrap = false };
            Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Floating = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };

            Button = MakeButton(20, new Color(0.18f, 0.18f, 0.18f), new Color(0.28f, 0.28f, 0.28f), new Color(0.4f, 0.1f, 0.08f));
            BigButton = MakeButton(30, new Color(0.45f, 0.06f, 0.05f), new Color(0.65f, 0.1f, 0.08f), new Color(0.85f, 0.2f, 0.1f));
            BigButton.fontStyle = FontStyle.Bold;

            ListItem = MakeButton(17, new Color(0.11f, 0.11f, 0.11f, 0.9f), new Color(0.2f, 0.2f, 0.2f, 0.95f), new Color(0.3f, 0.08f, 0.06f));
            ListItem.alignment = TextAnchor.MiddleLeft;
            ListItem.richText = true;
            ListItem.padding = new RectOffset(12, 8, 4, 4);
            ListItemSelected = new GUIStyle(ListItem);
            ListItemSelected.normal.background = Solid(new Color(0.42f, 0.08f, 0.06f, 0.95f));
            ListItemSelected.hover.background = ListItemSelected.normal.background;

            Tooltip = new GUIStyle(Small) { padding = new RectOffset(10, 10, 8, 8) };
            Tooltip.normal.background = Solid(new Color(0f, 0f, 0f, 0.92f));
            Tooltip.normal.textColor = Color.white;
        }

        private GUIStyle MakeButton(int fontSize, Color normal, Color hover, Color active)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = true,
                border = new RectOffset(0, 0, 0, 0)
            };
            style.normal.background = Solid(normal);
            style.hover.background = Solid(hover);
            style.active.background = Solid(active);
            style.focused.background = style.normal.background;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        public void Rect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = previous;
        }

        public void Panel(Rect rect)
        {
            Rect(rect, PanelColor);
            Rect(new Rect(rect.x, rect.y, rect.width, 3), Accent);
        }

        public void Shadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            var previous = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, color.a * 0.9f);
            GUI.Label(new Rect(rect.x + 3, rect.y + 3, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }

        public static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}

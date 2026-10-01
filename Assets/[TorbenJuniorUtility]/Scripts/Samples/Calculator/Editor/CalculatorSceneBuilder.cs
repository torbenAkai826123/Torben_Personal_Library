using TMPro;
using TorbenJuniorUtility.Calculator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TorbenJuniorUtility.Calculator.Editor
{
    public static class CalculatorSceneBuilder
    {
        const string ScenePath = "Assets/[TorbenJuniorUtility]/ExampleScene/Calculator.unity";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        static TMP_FontAsset _font;

        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_font == null)
                throw new System.InvalidOperationException($"Calculator font not found: {FontPath}");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorFromHex("101522");
            camera.orthographic = true;

            var canvasObject = new GameObject("Calculator Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var panel = CreatePanel("Calculator Panel", canvasObject.transform, new Vector2(570, 830),
                new Vector2(0, 0), ColorFromHex("1D2638"));
            CreateText("Title", panel, "CALCULATOR", 35, TextAlignmentOptions.Center,
                new Vector2(520, 55), new Vector2(0, 355), ColorFromHex("DDE7F5"));
            CreateText("Hint", panel, "FOUR OPERATIONS", 18, TextAlignmentOptions.Center,
                new Vector2(520, 35), new Vector2(0, 313), ColorFromHex("8393AC"));

            var displayPanel = CreatePanel("Display Panel", panel, new Vector2(520, 135),
                new Vector2(0, 207), ColorFromHex("101827"));
            var display = CreateText("DisplayValue", displayPanel, "0", 72, TextAlignmentOptions.Right,
                new Vector2(480, 105), new Vector2(0, 0), ColorFromHex("FFFFFF"));
            display.enableAutoSizing = true;
            display.fontSizeMin = 35;
            display.fontSizeMax = 72;

            var digits = new Button[10];
            float[] x = { -195, -65, 65, 195 };
            float[] y = { 65, -60, -185, -310 };
            string[,] names =
            {
                { "Digit7", "Digit8", "Digit9", "Divide" },
                { "Digit4", "Digit5", "Digit6", "Multiply" },
                { "Digit1", "Digit2", "Digit3", "Subtract" },
                { "Clear", "Digit0", "Equals", "Add" }
            };
            string[,] labels =
            {
                { "7", "8", "9", "÷" },
                { "4", "5", "6", "×" },
                { "1", "2", "3", "-" },
                { "C", "0", "=", "+" }
            };

            Button add = null, subtract = null, multiply = null, divide = null, equals = null, clear = null;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    string name = names[row, column];
                    Color color = name == "Equals" ? ColorFromHex("19A899") :
                        name == "Clear" ? ColorFromHex("A94C5B") :
                        column == 3 ? ColorFromHex("D78C48") : ColorFromHex("344158");
                    var button = CreateButton(name, labels[row, column], panel, new Vector2(x[column], y[row]), color);
                    if (name.StartsWith("Digit", System.StringComparison.Ordinal))
                        digits[int.Parse(name.Substring(5), System.Globalization.CultureInfo.InvariantCulture)] = button;
                    else if (name == "Add") add = button;
                    else if (name == "Subtract") subtract = button;
                    else if (name == "Multiply") multiply = button;
                    else if (name == "Divide") divide = button;
                    else if (name == "Equals") equals = button;
                    else if (name == "Clear") clear = button;
                }
            }

            var view = panel.gameObject.AddComponent<CalculatorView>();
            view.Configure(display, digits, add, subtract, multiply, divide, equals, clear);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException($"Failed to save scene: {ScenePath}");

            string capturePath = GetArgument("-capturePath");
            if (!string.IsNullOrEmpty(capturePath))
                Capture(camera, capturePath);

            Debug.Log($"Calculator scene saved: {ScenePath}");
        }

        static RectTransform CreatePanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        static TMP_Text CreateText(string name, Transform parent, string value, float fontSize,
            TextAlignmentOptions alignment, Vector2 size, Vector2 position, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(string name, string label, Transform parent, Vector2 position, Color color)
        {
            var panel = CreatePanel(name, parent, new Vector2(115, 105), position, color);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = ColorFromHex("D8E4F2");
            colors.pressedColor = ColorFromHex("A9B8CA");
            button.colors = colors;
            CreateText("Label", panel, label, 47, TextAlignmentOptions.Center,
                new Vector2(110, 100), Vector2.zero, Color.white);
            return button;
        }

        static Color ColorFromHex(string rgb)
        {
            if (!ColorUtility.TryParseHtmlString("#" + rgb, out Color color))
                throw new System.ArgumentException($"Invalid color: {rgb}");
            return color;
        }

        static string GetArgument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                    return args[index + 1];
            }
            return null;
        }

        static void Capture(Camera camera, string outputPath)
        {
            const int width = 1080;
            const int height = 1920;
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(outputPath, image.EncodeToPNG());
                Debug.Log($"Calculator preview saved: {outputPath}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }
    }
}

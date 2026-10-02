#if UNITY_EDITOR
using NUnit.Framework;
using Torben.Calculator;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Torben.Calculator.Tests
{
    public sealed class CalculatorScenePlayModeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator SceneInitializesAndButtonsUpdateDisplay()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/[TorbenJuniorUtility]/ExampleScene/Calculator.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            var view = Object.FindAnyObjectByType<CalculatorView>();
            Assert.IsNotNull(view);
            Assert.AreEqual("0", view.Display);

            Click("Digit9");
            Click("Multiply");
            Click("Digit4");
            Click("Equals");
            Assert.AreEqual("36", view.Display);

            Click("Clear");
            Assert.AreEqual("0", view.Display);
            LogAssert.NoUnexpectedReceived();
        }

        static void Click(string buttonName)
        {
            var button = GameObject.Find(buttonName)?.GetComponent<Button>();
            Assert.IsNotNull(button, $"Missing button: {buttonName}");
            button.onClick.Invoke();
        }
    }
}
#endif

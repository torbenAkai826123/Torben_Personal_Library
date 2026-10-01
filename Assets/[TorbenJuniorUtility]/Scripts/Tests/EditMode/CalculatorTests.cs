using NUnit.Framework;
using TMPro;
using Torben.Calculator;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Torben.Tests
{
    public sealed class CalculatorTests
    {
        const string ScenePath = "Assets/[TorbenJuniorUtility]/ExampleScene/Calculator.unity";

        [TestCase(CalculatorOperation.Add, "12")]
        [TestCase(CalculatorOperation.Subtract, "2")]
        [TestCase(CalculatorOperation.Multiply, "35")]
        [TestCase(CalculatorOperation.Divide, "1.4")]
        public void ModelEvaluatesFourOperations(CalculatorOperation operation, string expected)
        {
            var model = new CalculatorModel();
            model.PressDigit(7);
            model.PressOperation(operation);
            model.PressDigit(5);
            model.PressEquals();
            Assert.AreEqual(expected, model.Display);
        }

        [Test]
        public void ModelHandlesChainingAndClear()
        {
            var model = new CalculatorModel();
            model.PressDigit(8);
            model.PressOperation(CalculatorOperation.Add);
            model.PressDigit(2);
            model.PressOperation(CalculatorOperation.Multiply);
            model.PressDigit(3);
            model.PressEquals();
            Assert.AreEqual("30", model.Display);
            model.Clear();
            Assert.AreEqual("0", model.Display);
        }

        [Test]
        public void ModelRecoversFromDivisionByZero()
        {
            var model = new CalculatorModel();
            model.PressDigit(8);
            model.PressOperation(CalculatorOperation.Divide);
            model.PressDigit(0);
            model.PressEquals();
            Assert.AreEqual("Error", model.Display);
            model.PressDigit(4);
            Assert.AreEqual("4", model.Display);
        }

        [Test]
        public void SceneButtonsUpdateVisibleDisplay()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var view = Object.FindAnyObjectByType<CalculatorView>();
            Assert.IsNotNull(view);
            view.Initialize();

            Assert.AreEqual("0", view.Display);
            Click("Digit9");
            Click("Multiply");
            Click("Digit4");
            Click("Equals");
            Assert.AreEqual("36", view.Display);
            Click("Clear");
            Assert.AreEqual("0", view.Display);
        }

        [Test]
        public void SceneContainsReadableLabelsAndDigits()
        {
            EditorSceneManager.OpenScene(ScenePath);
            string[] buttonNames = { "Digit0", "Digit1", "Digit2", "Digit3", "Digit4", "Digit5",
                "Digit6", "Digit7", "Digit8", "Digit9", "Add", "Subtract", "Multiply",
                "Divide", "Equals", "Clear" };

            foreach (string name in buttonNames)
            {
                var button = GameObject.Find(name)?.GetComponent<Button>();
                Assert.IsNotNull(button, $"Missing button: {name}");
                var label = button.GetComponentInChildren<TMP_Text>();
                Assert.IsNotNull(label, $"Missing label: {name}");
                Assert.IsFalse(string.IsNullOrEmpty(label.text), $"Empty label: {name}");
                Assert.IsNotNull(label.font, $"Missing font: {name}");
                foreach (char character in label.text)
                    Assert.IsTrue(label.font.HasCharacter(character), $"Font lacks '{character}' on {name}");
            }

            var display = GameObject.Find("DisplayValue")?.GetComponent<TMP_Text>();
            Assert.IsNotNull(display);
            Assert.AreEqual("0", display.text);
            Assert.IsNotNull(display.font);
        }

        static void Click(string buttonName)
        {
            var button = GameObject.Find(buttonName)?.GetComponent<Button>();
            Assert.IsNotNull(button, $"Missing button: {buttonName}");
            button.onClick.Invoke();
        }
    }
}

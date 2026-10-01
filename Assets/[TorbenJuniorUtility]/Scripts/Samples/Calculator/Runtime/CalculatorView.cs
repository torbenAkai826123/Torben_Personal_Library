using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TorbenJuniorUtility.Calculator
{
    public sealed class CalculatorView : MonoBehaviour
    {
        [SerializeField] TMP_Text displayText;
        [SerializeField] Button[] digitButtons;
        [SerializeField] Button addButton;
        [SerializeField] Button subtractButton;
        [SerializeField] Button multiplyButton;
        [SerializeField] Button divideButton;
        [SerializeField] Button equalsButton;
        [SerializeField] Button clearButton;

        // 場景中的 CalculatorModel 實例
        CalculatorModel _model;

        public string Display => displayText == null ? string.Empty : displayText.text;

        public void Configure(TMP_Text display, Button[] digits, Button add, Button subtract,
            Button multiply, Button divide, Button equals, Button clear)
        {
            displayText = display;
            digitButtons = digits;
            addButton = add;
            subtractButton = subtract;
            multiplyButton = multiply;
            divideButton = divide;
            equalsButton = equals;
            clearButton = clear;
        }

        void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_model != null)
                return;

            _model = new CalculatorModel();

            for (int digit = 0; digit < digitButtons.Length; digit++)
            {
                int capturedDigit = digit;
                digitButtons[digit].onClick.AddListener(() => ApplyDigit(capturedDigit));
            }

            addButton.onClick.AddListener(() => ApplyOperation(CalculatorOperation.Add));
            subtractButton.onClick.AddListener(() => ApplyOperation(CalculatorOperation.Subtract));
            multiplyButton.onClick.AddListener(() => ApplyOperation(CalculatorOperation.Multiply));
            divideButton.onClick.AddListener(() => ApplyOperation(CalculatorOperation.Divide));
            equalsButton.onClick.AddListener(ApplyEquals);
            clearButton.onClick.AddListener(ApplyClear);
            RefreshDisplay();
        }

        void ApplyDigit(int digit)
        {
            _model.PressDigit(digit);
            RefreshDisplay();
        }

        void ApplyOperation(CalculatorOperation operation)
        {
            _model.PressOperation(operation);
            RefreshDisplay();
        }

        void ApplyEquals()
        {
            _model.PressEquals();
            RefreshDisplay();
        }

        void ApplyClear()
        {
            _model.Clear();
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            displayText.text = _model.Display;
        }
    }
}

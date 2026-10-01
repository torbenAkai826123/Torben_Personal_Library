using System.Globalization;

namespace Torben.Calculator
{
    public enum CalculatorOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public sealed class CalculatorModel
    {
        const int MaxInputDigits = 12;

        decimal _storedValue;
        CalculatorOperation? _pendingOperation;
        bool _startNewInput = true;
        bool _isError;

        public string Display { get; private set; } = "0";

        public void PressDigit(int digit)
        {
            if (digit < 0 || digit > 9)
                throw new System.ArgumentOutOfRangeException(nameof(digit));

            if (_isError)
                Clear();

            if (_startNewInput)
            {
                Display = digit.ToString(CultureInfo.InvariantCulture);
                _startNewInput = false;
            }
            else if (Display == "0")
            {
                Display = digit.ToString(CultureInfo.InvariantCulture);
            }
            else if (Display.Length < MaxInputDigits)
            {
                Display += digit.ToString(CultureInfo.InvariantCulture);
            }
        }

        public void PressOperation(CalculatorOperation operation)
        {
            if (_isError)
                return;

            if (_pendingOperation.HasValue && !_startNewInput)
            {
                if (!Evaluate())
                    return;
            }
            else if (!_pendingOperation.HasValue)
            {
                _storedValue = ParseDisplay();
            }

            _pendingOperation = operation;
            _startNewInput = true;
        }

        public void PressEquals()
        {
            if (_isError || !_pendingOperation.HasValue || _startNewInput)
                return;

            Evaluate();
            _pendingOperation = null;
            _startNewInput = true;
        }

        public void Clear()
        {
            Display = "0";
            _storedValue = 0;
            _pendingOperation = null;
            _startNewInput = true;
            _isError = false;
        }

        decimal ParseDisplay()
        {
            return decimal.Parse(Display, CultureInfo.InvariantCulture);
        }

        bool Evaluate()
        {
            decimal right = ParseDisplay();

            if (_pendingOperation == CalculatorOperation.Divide && right == 0)
            {
                SetError();
                return false;
            }

            try
            {
                decimal result = _pendingOperation switch
                {
                    CalculatorOperation.Add => _storedValue + right,
                    CalculatorOperation.Subtract => _storedValue - right,
                    CalculatorOperation.Multiply => _storedValue * right,
                    CalculatorOperation.Divide => _storedValue / right,
                    _ => right
                };

                _storedValue = result;
                Display = result.ToString("0.##########", CultureInfo.InvariantCulture);
                return true;
            }
            catch (System.OverflowException)
            {
                SetError();
                return false;
            }
        }

        void SetError()
        {
            Display = "Error";
            _storedValue = 0;
            _pendingOperation = null;
            _startNewInput = true;
            _isError = true;
        }
    }
}

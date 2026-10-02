using NUnit.Framework;
using Torben.Calculator;

namespace Torben.Calculator.Tests
{
    public sealed class CalculatorModelTests
    {
        [TestCase(CalculatorOperation.Add, "12")]
        [TestCase(CalculatorOperation.Subtract, "2")]
        [TestCase(CalculatorOperation.Multiply, "35")]
        [TestCase(CalculatorOperation.Divide, "1.4")]
        public void EvaluatesFourOperations(CalculatorOperation operation, string expected)
        {
            var model = new CalculatorModel();
            model.PressDigit(7);
            model.PressOperation(operation);
            model.PressDigit(5);
            model.PressEquals();
            Assert.AreEqual(expected, model.Display);
        }

        [Test]
        public void DivideByZeroShowsErrorAndNextDigitRecovers()
        {
            var model = new CalculatorModel();
            model.PressDigit(1);
            model.PressOperation(CalculatorOperation.Divide);
            model.PressDigit(0);
            model.PressEquals();
            Assert.AreEqual("Error", model.Display);

            model.PressDigit(3);
            Assert.AreEqual("3", model.Display);
        }

        [Test]
        public void RejectsDigitOutOfRange()
        {
            var model = new CalculatorModel();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => model.PressDigit(10));
        }
    }
}

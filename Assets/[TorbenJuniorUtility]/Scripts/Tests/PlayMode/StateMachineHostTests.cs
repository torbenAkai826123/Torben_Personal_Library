using NUnit.Framework;
using Torben.StateMachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Torben.StateMachine.Tests
{
    public sealed class StateMachineHostTests
    {
        GameObject _hostObject;

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (_hostObject != null) Object.Destroy(_hostObject);
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator UpdateConsumesRequestsAndDefersNextExecute()
        {
            var host = CreateProbe();
            Assert.AreEqual(StateMachineStatus.Running, host.Machine.Status);
            Assert.AreEqual(1, host.InitialEnterCount);
            Assert.AreEqual(1L, host.Machine.CurrentSerialNumber);

            Assert.AreEqual(RequestResult.Accepted,
                host.Machine.Request(new TransitionRequest<bool>(new TransitionId("go"), false)));
            yield return Until(() => host.GuardCount == 1);
            Assert.AreEqual(new StateId("initial"), host.Machine.CurrentStateId);
            Assert.AreEqual(0, host.InitialExitCount);
            Assert.AreEqual(0, host.InitialExecuteCount);
            Assert.AreEqual(1L, host.Machine.CurrentSerialNumber);
            Assert.IsNull(host.Machine.LastResult);

            host.Machine.Request(new TransitionRequest<bool>(new TransitionId("go"), true));
            yield return Until(() => host.Machine.CurrentStateId == new StateId("next"));
            Assert.AreEqual(1, host.InitialExitCount);
            Assert.AreEqual(0, host.NextExecuteCount);
            Assert.AreEqual(new StateResult<string>(new StateId("next"), 2, "done"), host.Machine.LastResult);
            yield return Until(() => host.NextExecuteCount > 0);
            Assert.AreEqual(1, host.NextExecuteCount);
            host.enabled = false;
            Assert.AreEqual(1, host.NextExitCount);
            Assert.AreEqual(StateMachineStatus.Stopped, host.Machine.Status);
            Assert.IsNull(host.Machine.CurrentStateId);
            Assert.AreEqual("done", host.Machine.LastResult.Value.Output);
        }

        [UnityTest]
        public System.Collections.IEnumerator DisableAndReenableStopOnceAndKeepSerialCounter()
        {
            var host = CreateProbe();
            yield return Until(() => host.InitialExecuteCount > 0);
            host.enabled = false;
            Assert.AreEqual(1, host.InitialExitCount);
            Assert.AreEqual(StateMachineStatus.Stopped, host.Machine.Status);
            Assert.AreEqual(RequestResult.Ignored,
                host.Machine.Request(new TransitionRequest<bool>(new TransitionId("go"), true)));
            int count = host.InitialExecuteCount;
            yield return null;
            Assert.AreEqual(count, host.InitialExecuteCount);

            host.enabled = true;
            Assert.AreEqual(2, host.InitialEnterCount);
            Assert.AreEqual(2L, host.Machine.CurrentSerialNumber);
            Assert.AreEqual(StateMachineStatus.Running, host.Machine.Status);
            Assert.IsNull(host.Machine.LastResult);
        }

        [UnityTest]
        public System.Collections.IEnumerator FaultStopsAutomaticTicksAndDisableDoesNotExitAgain()
        {
            var host = CreateProbe();
            host.ThrowOnExecute = true;
            LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("InvalidOperationException: Expected host fault\\."));
            yield return Until(() => host.Machine.Status == StateMachineStatus.Faulted);
            int count = host.InitialExecuteCount;
            yield return null;
            yield return null;
            Assert.AreEqual(count, host.InitialExecuteCount);
            Assert.AreEqual(0, host.InitialExitCount);
            host.enabled = false;
            Assert.AreEqual(0, host.InitialExitCount);
            Assert.AreEqual(StateMachineStatus.Stopped, host.Machine.Status);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public System.Collections.IEnumerator HooksRunInLifecycleOrderWithUsableMachine()
        {
            _hostObject = new GameObject("State machine host hook test");
            var host = _hostObject.AddComponent<StateMachineHookProbeHost>();
            CollectionAssert.AreEqual(new[] { "awake:Stopped", "enabled:Running" }, host.Calls);

            yield return null;
            host.enabled = false;
            host.enabled = true;
            CollectionAssert.AreEqual(
                new[] { "awake:Stopped", "enabled:Running", "disabled:Running", "enabled:Running" },
                host.Calls);
            Assert.AreEqual(StateMachineStatus.Running, host.Machine.Status);
        }

        [UnityTest]
        public System.Collections.IEnumerator ShadowedAwakeStillCreatesAndStartsMachine()
        {
            _hostObject = new GameObject("State machine host shadowed awake test");
            var host = _hostObject.AddComponent<StateMachineAwakeShadowProbeHost>();
            Assert.IsTrue(host.ShadowAwakeCalled);
            Assert.IsNotNull(host.Machine);
            Assert.AreEqual(StateMachineStatus.Running, host.Machine.Status);

            yield return null;
            host.enabled = false;
            Assert.AreEqual(StateMachineStatus.Stopped, host.Machine.Status);
        }

        StateMachineProbeHost CreateProbe()
        {
            _hostObject = new GameObject("State machine host integration test");
            return _hostObject.AddComponent<StateMachineProbeHost>();
        }

        static System.Collections.IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => condition() || Time.realtimeSinceStartup >= deadline);
            Assert.IsTrue(condition(), "Timed out waiting for a Unity Update.");
        }
    }
}

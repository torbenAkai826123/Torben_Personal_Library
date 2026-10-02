using NUnit.Framework;
using Torben.StateMachine;
using Torben.StateMachine.Samples;
using UnityEngine;
using UnityEngine.TestTools;

namespace Torben.StateMachine.Tests
{
    public sealed class DoorStateMachineHostTests
    {
        GameObject _hostObject;

        [UnityTearDown]
        public System.Collections.IEnumerator TearDown()
        {
            if (_hostObject != null) Object.Destroy(_hostObject);
            yield return null;
        }

        [UnityTest]
        public System.Collections.IEnumerator DoorSampleRunsWithUnityHost()
        {
            _hostObject = new GameObject("Door state machine sample");
            var host = _hostObject.AddComponent<DoorStateMachineHost>();
            Assert.AreEqual(RequestResult.Accepted, host.RequestOpen(false));
            int requestFrame = Time.frameCount;
            yield return Until(() => Time.frameCount > requestFrame);
            Assert.AreEqual(new StateId("idle"), host.Machine.CurrentStateId);
            Assert.IsNull(host.Machine.LastResult);

            Assert.AreEqual(RequestResult.Accepted, host.RequestOpen(true));
            yield return Until(() => host.Machine.CurrentStateId == new StateId("door"));
            Assert.AreEqual("Door opened", host.Machine.LastResult.Value.Output.Message);
            host.enabled = false;
            Assert.AreEqual(StateMachineStatus.Stopped, host.Machine.Status);
            Assert.AreEqual("Door opened", host.Machine.LastResult.Value.Output.Message);
        }

        static System.Collections.IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => condition() || Time.realtimeSinceStartup >= deadline);
            Assert.IsTrue(condition(), "Timed out waiting for a Unity Update.");
        }
    }
}

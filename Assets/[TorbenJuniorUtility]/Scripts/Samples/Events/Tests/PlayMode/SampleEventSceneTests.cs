#if UNITY_EDITOR
using NUnit.Framework;
using Torben.Events.Samples;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Torben.Events.Samples.Tests
{
    public sealed class SampleEventSceneTests
    {
        bool savedPrintLog;
        bool savedGuiLog;

        [SetUp]
        public void SetUp()
        {
            savedPrintLog = Log.isPrintLog;
            savedGuiLog = DebugLogs.isGUILog;
        }

        [TearDown]
        public void TearDown()
        {
            // 範例的 Installer 會打開 Log 與畫面視窗, 測試後恢復, 不影響其他測試
            DebugLogs.isGUILog = savedGuiLog;
            Log.isPrintLog = savedPrintLog;
        }

        [UnityTest]
        public System.Collections.IEnumerator DoorOpened_AllSubscribersReact_CounterSelfDestructs()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/[TorbenJuniorUtility]/ExampleScene/EventDoor.unity",
                new LoadSceneParameters(LoadSceneMode.Single));

            var installer = Object.FindAnyObjectByType<SampleEventInstaller>();
            var door = Object.FindAnyObjectByType<SampleDoor>();
            var doorLight = Object.FindAnyObjectByType<SampleDoorLight>();
            var doorSound = Object.FindAnyObjectByType<SampleDoorSound>();
            var doorCounter = Object.FindAnyObjectByType<SampleDoorCounter>();
            Assert.IsNotNull(installer);
            Assert.IsNotNull(door);
            Assert.IsNotNull(doorLight);
            Assert.IsNotNull(doorSound);
            Assert.IsNotNull(doorCounter);
            Assert.AreEqual(3, installer.DoorOpenedSubscriberCount);
            Assert.IsFalse(doorLight.IsOn);

            door.Open();
            Assert.IsTrue(doorLight.IsOn);
            Assert.AreEqual(1, doorSound.PlayCount);
            Assert.AreEqual(1, doorCounter.Count);

            door.Open();
            door.Open();
            Assert.AreEqual(3, doorCounter.Count);
            yield return null;  // Destroy 在這一幀結束時生效

            Assert.IsTrue(doorCounter == null, "The counter should destroy itself after 3 openings.");
            Assert.AreEqual(2, installer.DoorOpenedSubscriberCount);

            door.Open();
            Assert.AreEqual(4, doorSound.PlayCount);
            Assert.IsTrue(doorLight.IsOn);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif

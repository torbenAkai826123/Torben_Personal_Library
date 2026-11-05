using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Torben.Events.Samples.Editor
{
    /// <summary>建立 Event 範例場景 (門打開 → 燈、音效、計數器反應).</summary>
    public static class SampleEventSceneBuilder
    {
        /// <summary>範例場景的存檔位置.</summary>
        public const string ScenePath = "Assets/[TorbenJuniorUtility]/ExampleScene/EventDoor.unity";

        /// <summary>選單: 建立並開啟範例場景 (已存在時覆寫).</summary>
        [MenuItem("TorbenTools/Events/Build Door Sample Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>建立範例場景並存檔; 建立後場景保持開啟. 也可由 -executeMethod 呼叫.</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -6f), Quaternion.Euler(5f, 0f, 0f));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.13f);

            var sunObject = new GameObject("Directional Light", typeof(Light));
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.6f;

            // 發布者: 門
            var doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.name = "Door";
            doorObject.transform.position = new Vector3(-1.5f, 1f, 0f);
            doorObject.transform.localScale = new Vector3(1f, 2f, 0.15f);
            var door = doorObject.AddComponent<SampleDoor>();

            // 訂閱者 1: 燈
            var lampObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampObject.name = "Door Light";
            lampObject.transform.position = new Vector3(1.5f, 2f, 0f);
            lampObject.transform.localScale = Vector3.one * 0.5f;
            var pointObject = new GameObject("Point Light", typeof(Light));
            pointObject.transform.SetParent(lampObject.transform, false);
            var point = pointObject.GetComponent<Light>();
            point.type = LightType.Point;
            point.range = 6f;
            point.intensity = 3f;
            point.color = new Color(1f, 0.85f, 0.3f);
            point.enabled = false;
            var doorLight = lampObject.AddComponent<SampleDoorLight>();
            doorLight.Configure(lampObject.GetComponent<Renderer>(), point);

            // 訂閱者 2: 音效 (沒有指定音效檔時只以 Log 顯示)
            var soundObject = new GameObject("Door Sound", typeof(AudioSource));
            soundObject.GetComponent<AudioSource>().playOnAwake = false;
            var doorSound = soundObject.AddComponent<SampleDoorSound>();

            // 訂閱者 3: 計數器 (第 3 次後 Destroy 自己)
            var counterObject = new GameObject("Door Counter");
            var doorCounter = counterObject.AddComponent<SampleDoorCounter>();

            // Composition Root
            var installerObject = new GameObject("Event Installer");
            installerObject.AddComponent<SampleEventInstaller>().Configure(door, doorLight, doorSound, doorCounter);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException($"Failed to save scene: {ScenePath}");
            Debug.Log($"Event sample scene saved: {ScenePath}");
        }
    }
}

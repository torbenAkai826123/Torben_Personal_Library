using UnityEngine;

namespace Torben
{
    // CrossObject 是一個單例類別，用於管理跨場景的靜態物件。它確保在整個應用程式中只有一個實例存在，並提供初始化和日誌記錄功能。
    public sealed class CrossObject : MonoBehaviour
    {
        public static bool IsReady { get; private set; } = false;
        public static CrossObject Instance
        {
            get
            {
                // 只有當 instance 為 null 時，才會嘗試尋找或創建 GroundStaticObject 實例。
                if (instance == null)
                {
                    if (IsReady)
                        instance = FindAnyObjectByType<CrossObject>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject(nameof(CrossObject));
                        instance = go.AddComponent<CrossObject>();
                        instance.Initialized();
                    }
                    else
                    {
                        instance.Initialized();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }
        static CrossObject instance;
        static SystemCategory crossObjectCategory;  // Log Category.


        // Open API

        // 檢查 GroundStaticObject 是否已經準備好，並且有實例存在。
        public void PrintInfo()
        {
            if (Torben.Log.isPrintLog && Torben.Log.HasSink)
            {
                Torben.Log.Information("GroundStaticObject is ready.", crossObjectCategory ?? null);
            }
            else
            {
                string status = "Instance: " + (instance != null ? "Exists" : "Null") + ", IsReady: " + IsReady;
                Debug.Log("[LogOff] " + status);
            }
        } 

        // Internal Methods

        void Initialized()
        {
            if (IsReady && instance != null) return;
            Object.DontDestroyOnLoad(this.gameObject);
            IsReady = true;
            crossObjectCategory = new SystemCategory(this, "DOL", "Text");
            crossObjectCategory.tagColorHex = "#379761"; // Set the color for the log category.    
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                IsReady = false;
                // 需要確認其他物件是否依賴於這個單例，如果有，可能需要在這裡做一些清理工作。 
                instance = null;
            }
        }
    }
}
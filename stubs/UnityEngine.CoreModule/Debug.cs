// 컴파일 전용 스텁입니다. 게임에는 포함되지 않으며, 실행 시에는 Unity의 실제 어셈블리가 쓰입니다.
// 이 모드가 쓰는 Unity 공개 API 시그니처만 옮겼습니다.
namespace UnityEngine
{
    public class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public class Object
    {
        public string name { get { return null; } set { } }
        public static Object Instantiate(Object original, Transform parent) { return null; }
        public static Object Instantiate(Object original) { return null; }
        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object obj) { }
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
        public float x { get { return 0f; } set { } }
        public float y { get { return 0f; } set { } }
        public float width { get { return 0f; } set { } }
        public float height { get { return 0f; } set { } }
    }

    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1 }
    public enum TextureFormat { RGBA32 = 4 }
    public enum RenderTextureFormat { ARGB32 = 0 }
    public enum RenderTextureReadWrite { Default = 0, Linear = 1, sRGB = 2 }

    public class Texture : Object
    {
        public virtual int width { get { return 0; } set { } }
        public virtual int height { get { return 0; } set { } }
        public FilterMode filterMode { get { return default; } set { } }
        public TextureWrapMode wrapMode { get { return default; } set { } }
    }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { }
        public void ReadPixels(Rect source, int destX, int destY) { }
        public Color32[] GetPixels32() { return null; }
        public void SetPixels32(Color32[] colors) { }
        public void Apply() { }
    }

    public class RenderTexture : Texture
    {
        public static RenderTexture active { get { return null; } set { } }
        public static RenderTexture GetTemporary(int width, int height, int depthBuffer, RenderTextureFormat format, RenderTextureReadWrite readWrite) { return null; }
        public static void ReleaseTemporary(RenderTexture temp) { }
    }

    public class Graphics
    {
        public static void Blit(Texture source, RenderTexture dest) { }
    }

    public sealed class Sprite : Object
    {
        public Rect rect { get { return default; } }
        public Rect textureRect { get { return default; } }
        public Vector2 pivot { get { return default; } }
        public float pixelsPerUnit { get { return 0f; } }
        public Texture2D texture { get { return null; } }
        public bool packed { get { return false; } }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) { return null; }
    }

    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
        public Component GetComponent(System.Type type) { return null; }
        public Component[] GetComponents(System.Type type) { return null; }
    }

    public class Behaviour : Component
    {
        public bool enabled { get { return false; } set { } }
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public class ScriptableObject : Object
    {
        public static ScriptableObject CreateInstance(System.Type type) { return null; }
    }

    public class Transform : Component
    {
        public Transform parent { get { return null; } set { } }
        public Vector3 localPosition { get { return default; } set { } }
        public Vector3 position { get { return default; } set { } }
        public Quaternion rotation { get { return default; } set { } }
        public Vector3 lossyScale { get { return default; } }
        public Vector3 localEulerAngles { get { return default; } set { } }
        public Vector3 localScale { get { return default; } set { } }
        public int GetSiblingIndex() { return 0; }
        public void SetSiblingIndex(int index) { }
        public bool IsChildOf(Transform parent) { return false; }
        public int childCount { get { return 0; } }
        public Transform GetChild(int index) { return null; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public Vector3 InverseTransformPoint(Vector3 position) { return default; }
    }

    public sealed class GameObject : Object
    {
        public GameObject(string name, params System.Type[] components) { }
        public int layer { get { return 0; } set { } }
        public Component AddComponent(System.Type componentType) { return null; }
        public Component GetComponent(System.Type type) { return null; }
        public Transform transform { get { return null; } }
        public bool activeSelf { get { return false; } }
        public void SetActive(bool value) { }
    }

    public struct Vector2
    {
        public float x;
        public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public sealed class RectTransform : Transform
    {
        public Vector2 offsetMin { get { return default; } set { } }
        public Vector2 offsetMax { get { return default; } set { } }
        public void GetWorldCorners(Vector3[] fourCornersArray) { }
        public Vector2 anchorMin { get { return default; } set { } }
        public Vector2 anchorMax { get { return default; } set { } }
        public Vector2 pivot { get { return default; } set { } }
        public Vector2 anchoredPosition { get { return default; } set { } }
        public Vector2 sizeDelta { get { return default; } set { } }
    }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
}

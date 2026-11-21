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
    }

    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
    }

    public class Transform : Component
    {
        public Transform parent { get { return null; } set { } }
        public int GetSiblingIndex() { return 0; }
        public void SetSiblingIndex(int index) { }
        public bool IsChildOf(Transform parent) { return false; }
    }

    public sealed class GameObject : Object
    {
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

    public sealed class RectTransform : Transform
    {
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

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 길어져서 화면 밖으로 넘치는 목록을 스크롤 영역으로 감쌉니다.
    ///
    /// 게임의 목록 컨테이너(세로 레이아웃)를 그대로 둔 채, 그 자리에 '보이는 영역'을 만들어 그 안으로 옮기고
    /// Unity 의 ScrollRect·RectMask2D·Scrollbar 를 붙입니다. 보이는 영역은 목록의 윗변에서 시작해,
    /// 아래쪽에 있는 다른 UI(장애물)의 윗변 또는 부모의 아랫변까지입니다.
    ///
    /// Unity UI 타입(UnityEngine.UI 어셈블리)은 이름으로 찾아 씁니다. 필요한 것을 하나라도 못 찾거나
    /// 배치가 예상과 다르면 아무것도 바꾸지 않고 false 를 돌려줍니다.
    /// </summary>
    internal static class ScrollWrap
    {
        public const string ViewportName = "LimitOpenSepiria_Scroll";

        private static bool resolved;
        private static bool available;
        private static Type scrollRect, rectMask, scrollbar, image, layoutGroup, sizeFitter, canvas;

        /// <summary>아래쪽 한계로 쓸 UI. Gap 은 그 윗변에서 추가로 띄울 간격(부모 좌표 단위)입니다.</summary>
        public struct Obstacle
        {
            public object Target;
            public float Gap;

            public Obstacle(object target, float gap)
            {
                Target = target;
                Gap = gap;
            }
        }

        private static bool Resolve()
        {
            if (resolved)
                return available;
            resolved = true;

            scrollRect = Type.GetType("UnityEngine.UI.ScrollRect, UnityEngine.UI", false);
            rectMask = Type.GetType("UnityEngine.UI.RectMask2D, UnityEngine.UI", false);
            scrollbar = Type.GetType("UnityEngine.UI.Scrollbar, UnityEngine.UI", false);
            image = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", false);
            layoutGroup = Type.GetType("UnityEngine.UI.LayoutGroup, UnityEngine.UI", false);
            sizeFitter = Type.GetType("UnityEngine.UI.ContentSizeFitter, UnityEngine.UI", false);
            canvas = Type.GetType("UnityEngine.Canvas, UnityEngine.UIModule", false);

            available = scrollRect != null && rectMask != null && scrollbar != null && image != null
                        && layoutGroup != null && sizeFitter != null;
            if (!available)
                Log.Warn("Unity UI 타입을 찾지 못해 넘치는 목록에 스크롤을 붙일 수 없습니다.");
            return available;
        }

        public static bool IsWrapped(Transform content)
        {
            return !GameReflection.IsNull(content) && !GameReflection.IsNull(content.parent) && content.parent.name == ViewportName;
        }

        /// <summary>
        /// content 를 스크롤 영역으로 감쌉니다. 이미 감싸져 있으면 true.
        /// padX 는 보이는 영역을 좌우로 넓히는 폭(내용이 컨테이너보다 넓게 그려지는 경우용)입니다.
        /// </summary>
        public static bool Wrap(Transform contentTransform, float padX, IList<Obstacle> obstacles, string label)
        {
            if (IsWrapped(contentTransform))
                return true;
            if (!Resolve())
                return false;

            var content = contentTransform as RectTransform;
            var parent = content != null ? content.parent as RectTransform : null;
            if (GameReflection.IsNull(content) || GameReflection.IsNull(parent))
                return Skip(label, "컨테이너가 RectTransform 이 아닙니다");

            // 부모가 자식을 직접 배치하는 레이아웃이면 사이에 끼어들 수 없습니다.
            if (!GameReflection.IsNull(parent.GetComponent(layoutGroup)))
                return Skip(label, "부모가 레이아웃 그룹입니다");
            // 내용 높이가 자식 수에 따라 늘어나야 스크롤 범위가 생깁니다.
            if (GameReflection.IsNull(content.GetComponent(layoutGroup)))
                return Skip(label, "컨테이너에 레이아웃 그룹이 없습니다");

            ForceLayout();

            // 모든 좌표를 부모의 로컬 좌표(피벗 기준)로 바꿔 계산합니다.
            float left, right, bottom, top;
            LocalBounds(parent, content, out left, out right, out bottom, out top);
            float parentLeft, parentRight, parentBottom, parentTop;
            LocalBounds(parent, parent, out parentLeft, out parentRight, out parentBottom, out parentTop);

            float limit = parentBottom;
            if (obstacles != null)
            {
                foreach (Obstacle obstacle in obstacles)
                {
                    RectTransform rect = ToRect(obstacle.Target);
                    if (rect == null || rect.IsChildOf(content))
                        continue;
                    float l, r, b, t;
                    LocalBounds(parent, rect, out l, out r, out b, out t);
                    // 목록 윗변보다 아래에서 시작하는 것만 한계로 봅니다.
                    if (t < top - 1f && t + obstacle.Gap > limit)
                        limit = t + obstacle.Gap;
                }
            }

            float height = top - limit - 2f;
            float width = right - left;
            if (height < 24f || width < 4f)
                return Skip(label, "보이는 영역을 정할 수 없습니다 (높이 " + height + ", 폭 " + width + ")");

            // 여기서부터 화면을 바꿉니다.
            int layer = parent.gameObject.layer;
            int siblingIndex = content.GetSiblingIndex();

            RectTransform viewport = NewRect(ViewportName, parent, layer);
            viewport.SetSiblingIndex(siblingIndex);
            Place(viewport, parent.pivot, new Vector2(0.5f, 1f), (left + right) * 0.5f, top, width + padX * 2f, height);
            viewport.gameObject.AddComponent(rectMask);

            content.SetParent(viewport, false);
            var topCenter = new Vector2(0.5f, 1f);
            content.anchorMin = topCenter;
            content.anchorMax = topCenter;
            content.pivot = topCenter;
            content.anchoredPosition = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(width, Math.Max(1f, top - bottom));

            Component fitter = content.GetComponent(sizeFitter);
            if (GameReflection.IsNull(fitter))
                fitter = content.gameObject.AddComponent(sizeFitter);
            SetEnum(fitter, "verticalFit", 2); // PreferredSize

            // 스크롤 막대: 트랙 > 슬라이딩 영역 > 손잡이
            RectTransform track = NewRect(ViewportName + "bar", parent, layer);
            track.SetSiblingIndex(siblingIndex + 1);
            Place(track, parent.pivot, new Vector2(0f, 1f), right + 3f, top, 4f, height);
            Component trackImage = track.gameObject.AddComponent(image);
            Set(trackImage, "color", new Color(0f, 0f, 0f, 0.55f));

            RectTransform slidingArea = NewRect("SlidingArea", track, layer);
            Stretch(slidingArea);
            RectTransform handle = NewRect("Handle", slidingArea, layer);
            Stretch(handle);
            Component handleImage = handle.gameObject.AddComponent(image);
            Set(handleImage, "color", new Color(0.72f, 0.74f, 0.86f, 1f));

            Component bar = track.gameObject.AddComponent(scrollbar);
            Set(bar, "targetGraphic", handleImage);
            Set(bar, "handleRect", handle);
            SetEnum(bar, "direction", 2); // BottomToTop

            Component scroll = viewport.gameObject.AddComponent(scrollRect);
            Set(scroll, "content", content);
            Set(scroll, "viewport", viewport);
            Set(scroll, "horizontal", false);
            Set(scroll, "vertical", true);
            SetEnum(scroll, "movementType", 2); // Clamped
            Set(scroll, "inertia", false);
            Set(scroll, "scrollSensitivity", 24f);
            Set(scroll, "verticalScrollbar", bar);
            SetEnum(scroll, "verticalScrollbarVisibility", 1); // AutoHide

            Log.Info(label + " 에 스크롤 적용 (보이는 높이 " + height.ToString("0") + ", 폭 " + width.ToString("0") + ")");
            return true;
        }

        private static bool Skip(string label, string reason)
        {
            Log.Warn(label + " 에 스크롤을 붙이지 못했습니다: " + reason);
            return false;
        }

        private static RectTransform NewRect(string name, Transform parent, int layer)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // anchor 를 부모의 피벗에 두면 anchoredPosition 이 부모 로컬 좌표와 같아집니다.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, float x, float y, float width, float height)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
        }

        private static void LocalBounds(Transform space, RectTransform rect, out float left, out float right, out float bottom, out float top)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            left = bottom = float.MaxValue;
            right = top = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = space.InverseTransformPoint(corners[i]);
                if (local.x < left) left = local.x;
                if (local.x > right) right = local.x;
                if (local.y < bottom) bottom = local.y;
                if (local.y > top) top = local.y;
            }
        }

        private static RectTransform ToRect(object value)
        {
            if (GameReflection.IsNull(value))
                return null;
            var component = value as Component;
            if (component != null)
                return component.transform as RectTransform;
            var gameObject = value as GameObject;
            return gameObject != null ? gameObject.transform as RectTransform : null;
        }

        private static void ForceLayout()
        {
            try
            {
                MethodInfo force = canvas != null ? canvas.GetMethod("ForceUpdateCanvases", BindingFlags.Public | BindingFlags.Static) : null;
                if (force != null)
                    force.Invoke(null, null);
            }
            catch (Exception)
            {
                // 레이아웃 강제 갱신은 실패해도 진행합니다.
            }
        }

        private static void Set(object target, string property, object value)
        {
            PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (info == null)
                throw new MissingMemberException(target.GetType().Name, property);
            info.SetValue(target, value, null);
        }

        private static void SetEnum(object target, string property, int value)
        {
            PropertyInfo info = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (info == null)
                throw new MissingMemberException(target.GetType().Name, property);
            info.SetValue(target, Enum.ToObject(info.PropertyType, value), null);
        }

        /// <summary>화면 구조를 로그로 남깁니다(배치 조정용 진단).</summary>
        public static string Describe(Transform root, int maxDepth, int maxChildren)
        {
            var text = new StringBuilder();
            int lines = 0;
            Describe(root, 0, maxDepth, maxChildren, text, ref lines);
            return text.ToString();
        }

        private static void Describe(Transform node, int depth, int maxDepth, int maxChildren, StringBuilder text, ref int lines)
        {
            if (GameReflection.IsNull(node) || lines > 300)
                return;
            lines++;

            text.Append(' ', depth * 2).Append(node.name);
            if (!node.gameObject.activeSelf)
                text.Append(" (off)");
            var rect = node as RectTransform;
            if (rect != null)
            {
                text.Append(" pos=").Append(rect.anchoredPosition.x.ToString("0")).Append(',').Append(rect.anchoredPosition.y.ToString("0"))
                    .Append(" size=").Append(rect.sizeDelta.x.ToString("0")).Append(',').Append(rect.sizeDelta.y.ToString("0"))
                    .Append(" anchor=").Append(rect.anchorMin.x.ToString("0.#")).Append(',').Append(rect.anchorMin.y.ToString("0.#"))
                    .Append('~').Append(rect.anchorMax.x.ToString("0.#")).Append(',').Append(rect.anchorMax.y.ToString("0.#"))
                    .Append(" pivot=").Append(rect.pivot.x.ToString("0.#")).Append(',').Append(rect.pivot.y.ToString("0.#"));
            }

            Component[] components = node.GetComponents(typeof(Component));
            if (components != null)
            {
                text.Append(" [");
                bool first = true;
                foreach (Component component in components)
                {
                    if (GameReflection.IsNull(component) || component is Transform)
                        continue;
                    if (!first) text.Append(' ');
                    first = false;
                    text.Append(component.GetType().Name);
                }
                text.Append(']');
            }
            text.Append('\n');

            if (depth >= maxDepth)
                return;
            int count = node.childCount;
            for (int i = 0; i < count && i < maxChildren; i++)
                Describe(node.GetChild(i), depth + 1, maxDepth, maxChildren, text, ref lines);
            if (count > maxChildren)
                text.Append(' ', (depth + 1) * 2).Append("… +").Append(count - maxChildren).Append('\n');
        }
    }
}

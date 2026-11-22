using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 손에 든 칼의 전용 애니메이션 프레임을 만듭니다.
    ///
    /// 카타나의 모습은 AnimationSet(상태별 그림 묶음)을 Animator2D 가 재생하는 방식입니다.
    /// 휘두르는 동작과 정확히 맞아야 하므로, 원본 프레임 하나하나를 읽어 서리단조 배색으로 다시 칠한
    /// 새 그림을 만들고, 그 그림들로 이루어진 새 AnimationSet 을 이 무기의 프리팹에만 넣습니다.
    /// 게임 원본 그림과 다른 무기는 바뀌지 않으며, 게임 그림을 모드에 담지도 않습니다(실행 중에 만듭니다).
    /// </summary>
    internal static class FrameRecolor
    {
        private static readonly Dictionary<Texture2D, Color32[]> Pixels = new Dictionary<Texture2D, Color32[]>();
        private static readonly Dictionary<Sprite, Sprite> Sprites = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> Sets = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        private static int failed;

        /// <summary>무기 프리팹의 칼·칼집 애니메이터에 전용 프레임을 넣습니다. 성공한 프레임 수를 돌려줍니다.</summary>
        public static int Apply(Component weapon)
        {
            failed = 0;
            int sets = 0;
            foreach (string field in new[] { "katanaAnimator", "scabbardAnimator" })
            {
                object animator = Reflect.Get(weapon, field);
                if (Reflect.IsNull(animator))
                    continue;
                var original = Reflect.Get(animator, "currentSet") as UnityEngine.Object;
                if (Reflect.IsNull(original))
                    continue;
                UnityEngine.Object replaced;
                if (!Sets.TryGetValue(original, out replaced))
                {
                    replaced = BuildSet(original);
                    Sets[original] = replaced;
                }
                if (replaced != null && Reflect.Set(animator, "currentSet", replaced))
                    sets++;
            }

            int made = Sprites.Count;
            Log.Info("전용 프레임: 애니메이션 묶음 " + sets + "개, 그림 " + made + "장 생성" + (failed > 0 ? ", " + failed + "장은 읽지 못해 원본 사용" : ""));
            Pixels.Clear(); // 원본 픽셀 사본은 더 필요 없습니다.
            return made;
        }

        private static UnityEngine.Object BuildSet(UnityEngine.Object original)
        {
            UnityEngine.Object copy = UnityEngine.Object.Instantiate(original);
            copy.name = original.name + "_FrostforgedCrescent";
            Reflect.Set(copy, "hideFlags", 61);

            var states = Reflect.Get(copy, "sprites") as IEnumerable;
            if (states == null)
                return null;
            foreach (object state in states)
            {
                var timeline = Reflect.Get(state, "timeline") as IEnumerable;
                if (timeline == null)
                    continue;
                foreach (object frame in timeline)
                {
                    var sprite = Reflect.Get(frame, "sprite") as Sprite;
                    if (Reflect.IsNull(sprite))
                        continue;
                    Sprite recolored = Recolor(sprite);
                    if (recolored != null)
                        Reflect.Set(frame, "sprite", recolored);
                }
            }
            return copy;
        }

        private static Sprite Recolor(Sprite source)
        {
            Sprite done;
            if (Sprites.TryGetValue(source, out done))
                return done;
            try
            {
                Texture2D texture = source.texture;
                Rect area = source.packed ? source.textureRect : source.rect;
                int x = (int)Math.Round(area.x), y = (int)Math.Round(area.y);
                int width = (int)Math.Round(area.width), height = (int)Math.Round(area.height);
                Color32[] all = ReadAll(texture);
                int textureWidth = texture.width;
                if (width <= 0 || height <= 0 || x < 0 || y < 0 || x + width > textureWidth || (y + height) * textureWidth > all.Length)
                    throw new InvalidOperationException("그림 영역이 텍스처를 벗어남");

                var pixels = new Color32[width * height];
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < width; column++)
                        pixels[row * width + column] = Palette.Map(all[(y + row) * textureWidth + x + column]);

                var created = new Texture2D(width, height, TextureFormat.RGBA32, false);
                created.filterMode = FilterMode.Point;
                created.wrapMode = TextureWrapMode.Clamp;
                created.SetPixels32(pixels);
                created.Apply();
                Reflect.Set(created, "hideFlags", 61);

                // 원본과 같은 크기·기준점이라 같은 자리에 그려집니다.
                Rect sourceRect = source.rect;
                Vector2 pivot = source.pivot;
                var normalized = new Vector2(pivot.x / sourceRect.width, pivot.y / sourceRect.height);
                Sprite sprite = Sprite.Create(created, new Rect(0f, 0f, width, height), normalized, source.pixelsPerUnit);
                sprite.name = source.name + "_FC";
                Reflect.Set(sprite, "hideFlags", 61);
                Sprites[source] = sprite;
                return sprite;
            }
            catch (Exception e)
            {
                if (failed == 0)
                    Log.Warn("전용 프레임: '" + source.name + "' 을(를) 읽지 못했습니다 — " + e.Message);
                failed++;
                return null;
            }
        }

        // 게임 텍스처는 보통 직접 읽을 수 없으므로, 화면 밖 버퍼에 한 번 그린 뒤 읽습니다.
        private static Color32[] ReadAll(Texture2D texture)
        {
            Color32[] cached;
            if (Pixels.TryGetValue(texture, out cached))
                return cached;

            int width = texture.width, height = texture.height;
            RenderTexture previous = RenderTexture.active;
            RenderTexture buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(texture, buffer);
                RenderTexture.active = buffer;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                copy.Apply();
                cached = copy.GetPixels32();
                UnityEngine.Object.Destroy(copy);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
            }
            Pixels[texture] = cached;
            return cached;
        }
    }

    /// <summary>서리단조 배색: 윤곽은 짙은 남색, 칼몸·칼집은 남색→하늘색→흰색, 금속 장식은 옅은 금색.</summary>
    internal static class Palette
    {
        private static readonly float[] Stops = { 0f, 0.18f, 0.42f, 0.68f, 0.88f, 1f };
        private static readonly int[][] Colors =
        {
            new[] { 10, 14, 30 }, new[] { 20, 32, 78 }, new[] { 44, 112, 204 },
            new[] { 112, 210, 252 }, new[] { 222, 240, 250 }, new[] { 255, 255, 255 }
        };

        public static Color32 Map(Color32 c)
        {
            if (c.a == 0)
                return c;
            int max = Math.Max(c.r, Math.Max(c.g, c.b));
            float level = max / 255f;

            // 따뜻한 색(손잡이 감개·코등이의 갈색, 금색)은 옅은 금색으로.
            if (c.r > c.b + 24 && c.r >= c.g && level > 0.2f)
                return new Color32((byte)(60 + 180 * level), (byte)(48 + 152 * level), (byte)(24 + 84 * level), c.a);

            for (int i = 1; i < Stops.Length; i++)
            {
                if (level > Stops[i] && i < Stops.Length - 1)
                    continue;
                float t = (level - Stops[i - 1]) / (Stops[i] - Stops[i - 1]);
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;
                return new Color32(Mix(Colors[i - 1][0], Colors[i][0], t), Mix(Colors[i - 1][1], Colors[i][1], t), Mix(Colors[i - 1][2], Colors[i][2], t), c.a);
            }
            return c;
        }

        private static byte Mix(int from, int to, float t)
        {
            return (byte)Math.Round(from + (to - from) * t);
        }
    }
}

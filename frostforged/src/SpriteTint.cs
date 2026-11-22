using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 게임에 있는 그림을 실행 중에 읽어 푸른 색조로 다시 칠한 새 그림을 만듭니다.
    /// 게임 그림을 모드에 담지 않기 위한 방식이며, 원본 그림과 다른 무기는 바뀌지 않습니다.
    /// </summary>
    internal static class SpriteTint
    {
        private static readonly Dictionary<Texture2D, Color32[]> Pixels = new Dictionary<Texture2D, Color32[]>();

        /// <summary>source 를 푸르게 칠한 그림. pivot 은 0~1 로 정규화한 기준점이고, null 이면 원본 것을 씁니다.</summary>
        public static Sprite Bluer(Sprite source, Vector2? pivot, float scale)
        {
            return Remap(source, pivot, scale, "_FFCBlue", (pixels, width, height) =>
            {
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Shift(pixels[i]);
                return pixels;
            });
        }

        /// <summary>source 의 픽셀을 process 로 바꾼 새 그림. 크기는 같고, pivot 이 null 이면 기준점도 같습니다.</summary>
        public static Sprite Remap(Sprite source, Vector2? pivot, float scale, string suffix, Func<Color32[], int, int, Color32[]> process)
        {
            if (Reflect.IsNull(source))
                return null;
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
                        pixels[row * width + column] = all[(y + row) * textureWidth + x + column];
                pixels = process(pixels, width, height);

                var created = new Texture2D(width, height, TextureFormat.RGBA32, false);
                created.filterMode = FilterMode.Point;
                created.wrapMode = TextureWrapMode.Clamp;
                created.SetPixels32(pixels);
                created.Apply();
                Reflect.Set(created, "hideFlags", 61);

                Rect sourceRect = source.rect;
                Vector2 own = source.pivot;
                Vector2 normalized = pivot ?? new Vector2(own.x / sourceRect.width, own.y / sourceRect.height);
                Sprite sprite = Sprite.Create(created, new Rect(0f, 0f, width, height), normalized, source.pixelsPerUnit / (scale > 0f ? scale : 1f));
                sprite.name = source.name + suffix;
                Reflect.Set(sprite, "hideFlags", 61);
                return sprite;
            }
            catch (Exception e)
            {
                Log.Warn("그림 '" + source.name + "' 을(를) 다시 칠하지 못했습니다 — " + e.Message);
                return null;
            }
        }

        public static void Release()
        {
            Pixels.Clear();
        }

        // 윤곽(아주 어두운 색)은 두고, 나머지는 붉은 기를 빼고 푸른 기를 더합니다.
        private static Color32 Shift(Color32 c)
        {
            if (c.a == 0 || Math.Max(c.r, Math.Max(c.g, c.b)) <= 60)
                return c;
            int r = (int)(c.r * 0.62f);
            int g = (int)(c.g * 0.86f);
            int b = c.b + (int)((255 - c.b) * 0.45f);
            return new Color32((byte)r, (byte)g, (byte)Math.Min(255, b), c.a);
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
}

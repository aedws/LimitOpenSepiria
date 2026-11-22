using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 모드 폴더의 PNG 를 Unity 스프라이트로 읽습니다.
    /// 기준점(pivot)은 대체할 원본 스프라이트에서 비율로 가져오고, 화면에서의 크기가 원본과 같아지도록
    /// 픽셀당 단위를 맞춥니다. 그래서 원본과 같은 방향으로 그린 그림이면 해상도가 달라도 같은 자리에 같은 크기로 나옵니다.
    /// Texture2D·Sprite 관련 타입은 Unity 모듈 어셈블리에 있어 이름으로 접근합니다.
    /// </summary>
    internal static class SpriteLoader
    {
        public static object Load(string path, object original, string label)
        {
            if (!File.Exists(path))
            {
                Log.Info(label + ": 그림 파일이 없어 원본을 씁니다 (" + Path.GetFileName(path) + "). 원본 " + Describe(original));
                return null;
            }

            try
            {
                Type coreAssemblyType = typeof(UnityEngine.Object);
                Type textureType = coreAssemblyType.Assembly.GetType("UnityEngine.Texture2D");
                Type spriteType = coreAssemblyType.Assembly.GetType("UnityEngine.Sprite");
                Type rectType = coreAssemblyType.Assembly.GetType("UnityEngine.Rect");
                Type conversion = Reflect.UnityType("UnityEngine.ImageConversion", "UnityEngine.ImageConversionModule");
                if (textureType == null || spriteType == null || rectType == null || conversion == null)
                {
                    Log.Warn(label + ": 이미지 읽기에 필요한 Unity 타입을 찾지 못했습니다.");
                    return null;
                }

                object texture = Activator.CreateInstance(textureType, 2, 2);
                MethodInfo loadImage = conversion.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "LoadImage" && m.GetParameters().Length == 2
                                && m.GetParameters()[1].ParameterType == typeof(byte[])); // Unity 6 에는 ReadOnlySpan 판도 있습니다
                if (!(bool)loadImage.Invoke(null, new[] { texture, File.ReadAllBytes(path) }))
                {
                    Log.Warn(label + ": PNG 를 읽지 못했습니다 (" + Path.GetFileName(path) + ").");
                    return null;
                }
                Reflect.Set(texture, "filterMode", 0); // Point: 픽셀 아트가 번지지 않게
                Reflect.Set(texture, "wrapMode", 1);   // Clamp

                int width = (int)Reflect.Get(texture, "width");
                int height = (int)Reflect.Get(texture, "height");

                // 기준점과 크기 기준은 원본에서 가져옵니다. 원본이 없으면 가운데, 16픽셀 = 1단위.
                var pivot = new Vector2(0.5f, 0.5f);
                float pixelsPerUnit = 16f;
                if (!Reflect.IsNull(original))
                {
                    object rect = Reflect.Get(original, "rect");
                    float originalWidth = (float)Reflect.Get(rect, "width");
                    float originalHeight = (float)Reflect.Get(rect, "height");
                    var originalPivot = (Vector2)Reflect.Get(original, "pivot");
                    if (originalWidth > 0f && originalHeight > 0f)
                        pivot = new Vector2(originalPivot.x / originalWidth, originalPivot.y / originalHeight);
                    pixelsPerUnit = (float)Reflect.Get(original, "pixelsPerUnit");
                    // 새 그림이 원본보다 세밀하면 그만큼 픽셀당 단위를 올려 화면 크기를 원본에 맞춥니다.
                    if (originalWidth > 0f && originalHeight > 0f)
                        pixelsPerUnit *= Math.Max(width / originalWidth, height / originalHeight);
                }

                object area = Activator.CreateInstance(rectType, 0f, 0f, (float)width, (float)height);
                MethodInfo create = spriteType.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m =>
                {
                    ParameterInfo[] p = m.GetParameters();
                    return m.Name == "Create" && p.Length == 4 && p[3].ParameterType == typeof(float);
                });
                object sprite = create.Invoke(null, new[] { texture, area, pivot, pixelsPerUnit });
                ((UnityEngine.Object)sprite).name = "FrostforgedCrescent_" + Path.GetFileNameWithoutExtension(path);
                Log.Info(label + ": " + Path.GetFileName(path) + " 적용 (" + width + "x" + height + "px). 원본 " + Describe(original));
                return sprite;
            }
            catch (Exception e)
            {
                Log.Warn(label + ": 그림 적용 실패 — " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 원본 무기 그림이 어느 쪽을 향하는지(손잡이 → 칼끝)를 크기와 기준점으로 추정합니다.
        /// "right", "left", "up", "down" 중 하나, 판단할 수 없으면(대각선 그림 등) null.
        /// </summary>
        public static string GuessDirection(object original)
        {
            if (Reflect.IsNull(original))
                return null;
            try
            {
                object rect = Reflect.Get(original, "rect");
                float width = (float)Reflect.Get(rect, "width");
                float height = (float)Reflect.Get(rect, "height");
                var pivot = (Vector2)Reflect.Get(original, "pivot");
                if (width <= 0f || height <= 0f)
                    return null;
                // 손잡이(쥐는 곳)는 기준점 쪽에 있습니다.
                if (width >= height * 1.3f)
                    return pivot.x / width <= 0.5f ? "right" : "left";
                if (height >= width * 1.3f)
                    return pivot.y / height <= 0.5f ? "up" : "down";
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>원본 스프라이트의 크기와 기준점. 새 그림을 그릴 때의 기준입니다.</summary>
        public static string Describe(object sprite)
        {
            if (Reflect.IsNull(sprite))
                return "(없음)";
            try
            {
                object rect = Reflect.Get(sprite, "rect");
                var pivot = (Vector2)Reflect.Get(sprite, "pivot");
                return "'" + ((UnityEngine.Object)sprite).name + "' " + ((float)Reflect.Get(rect, "width")).ToString("0") + "x"
                       + ((float)Reflect.Get(rect, "height")).ToString("0") + "px, 기준점 (" + pivot.x.ToString("0.#") + ", " + pivot.y.ToString("0.#")
                       + ")px, " + ((float)Reflect.Get(sprite, "pixelsPerUnit")).ToString("0.#") + "px = 1단위";
            }
            catch (Exception)
            {
                return "(읽기 실패)";
            }
        }
    }
}

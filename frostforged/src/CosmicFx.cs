using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 우주 속성 이펙트. Sprites/Fx 의 그림(프레임을 가로로 이어 붙인 한 줄)을 읽어 FxPlayer 로 재생합니다.
    /// 화면 표시만 하며, 만드는 쪽(호스트)의 화면에 보입니다.
    /// </summary>
    internal static class CosmicFx
    {
        // 그림 이름 → 프레임 수
        private static readonly Dictionary<string, int> Sheets = new Dictionary<string, int>
        {
            { "ignorance", 6 }, { "void", 8 }, { "spear_blue", 4 }, { "spear_red", 4 },
            { "cut_black", 12 }, { "cut_white", 12 }, { "needle", 6 }, { "ring", 5 },
        };

        private static readonly Dictionary<string, Sprite[]> Loaded = new Dictionary<string, Sprite[]>();
        private static int sortingLayer;
        private static bool hasLayer, loggedFailure;

        private static Sprite[] Frames(string name)
        {
            Sprite[] frames;
            if (Loaded.TryGetValue(name, out frames))
                return frames;
            frames = new Sprite[0];
            try
            {
                string path = Path.Combine(Entry.packageRoot ?? "", "Sprites", "Fx", name + ".png");
                int count;
                Type conversion = Reflect.UnityType("UnityEngine.ImageConversion", "UnityEngine.ImageConversionModule");
                if (File.Exists(path) && Sheets.TryGetValue(name, out count) && conversion != null)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    MethodInfo load = conversion.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .First(m => m.Name == "LoadImage" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(byte[]));
                    if ((bool)load.Invoke(null, new object[] { texture, File.ReadAllBytes(path) }))
                    {
                        texture.filterMode = FilterMode.Point;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        Reflect.Set(texture, "hideFlags", 61);
                        int width = texture.width / count, height = texture.height;
                        frames = new Sprite[count];
                        for (int i = 0; i < count; i++)
                        {
                            frames[i] = Sprite.Create(texture, new Rect(i * width, 0f, width, height), new Vector2(0.5f, 0.5f), 16f);
                            Reflect.Set(frames[i], "hideFlags", 61);
                        }
                    }
                }
                if (frames.Length == 0)
                    Log.Warn("이펙트 그림 '" + name + "' 을(를) 읽지 못했습니다. 이 이펙트는 표시되지 않습니다.");
            }
            catch (Exception e)
            {
                Log.Warn("이펙트 그림 '" + name + "' 읽기 실패: " + e.Message);
                frames = new Sprite[0];
            }
            Loaded[name] = frames;
            return frames;
        }

        // 이펙트가 캐릭터와 같은 층에 그려지도록, 기준이 될 캐릭터의 그리기 층을 한 번 알아 둡니다.
        public static void LearnLayer(object avatar)
        {
            if (hasLayer || Reflect.IsNull(avatar))
                return;
            try
            {
                MethodInfo find = typeof(Component).GetMethod("GetComponentInChildren", new[] { typeof(Type) });
                var renderer = find == null ? null : find.Invoke(avatar, new object[] { typeof(SpriteRenderer) }) as SpriteRenderer;
                if (renderer != null)
                {
                    sortingLayer = renderer.sortingLayerID;
                    hasLayer = true;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>이펙트를 하나 만듭니다. angle 은 도(시계 반대 방향), scale 은 가로·세로 배율입니다.</summary>
        public static FxPlayer Spawn(string name, Vector3 position, float angle, float scaleX, float scaleY, Color color, float framesPerSecond, bool loop, float life)
        {
            try
            {
                Sprite[] frames = Frames(name);
                if (frames.Length == 0)
                    return null;
                var holder = new GameObject("FFC_Fx_" + name);
                holder.transform.position = position;
                holder.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                holder.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                var renderer = (SpriteRenderer)holder.AddComponent(typeof(SpriteRenderer));
                renderer.sprite = frames[0];
                renderer.color = color;
                if (hasLayer)
                    renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = 30000;
                var player = (FxPlayer)holder.AddComponent(typeof(FxPlayer));
                player.frames = frames;
                player.framesPerSecond = framesPerSecond;
                player.loop = loop;
                player.life = life;
                player.target = renderer;
                return player;
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("이펙트 생성 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
                return null;
            }
        }
    }
}

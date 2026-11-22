using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 칼을 뽑아 든 동안 허리에 보이는 빈 칼집.
    ///
    /// 게임의 카타나에는 허리 칼집이 따로 없어서, 캐릭터(무기 조작부) 아래에 그림 하나를 붙입니다.
    /// 캐릭터가 좌우로 돌면 함께 뒤집힙니다. 위치·각도는 Sprites/scabbard.txt 에서 읽으므로
    /// 다시 빌드하지 않고 고칠 수 있습니다.
    /// </summary>
    internal static class HipScabbard
    {
        private static object sprite;
        private static bool spriteTried;

        public static GameObject Create(Component controller, Component weapon)
        {
            try
            {
                if (Reflect.IsNull(controller) || string.IsNullOrEmpty(Entry.packageRoot))
                    return null;
                if (!spriteTried || Reflect.IsNull(sprite))
                {
                    spriteTried = true;
                    sprite = SpriteLoader.Load(Path.Combine(Entry.packageRoot, "Sprites", "scabbard.png"), null, "허리 칼집");
                }
                if (Reflect.IsNull(sprite))
                    return null;

                float x = -0.4f, y = 0.45f, angle = 150f, scale = 1f;
                int order = 0;
                ReadSettings(ref x, ref y, ref angle, ref scale, ref order);

                Type rendererType = typeof(Component).Assembly.GetType("UnityEngine.SpriteRenderer");
                if (rendererType == null)
                    return null;
                var holder = new GameObject("FrostforgedCrescent_HipScabbard");
                holder.layer = controller.gameObject.layer;
                holder.transform.SetParent(controller.transform, false);
                holder.transform.localPosition = new Vector3(x, y, 0f);
                holder.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                holder.transform.localScale = new Vector3(scale, scale, 1f);

                Component renderer = holder.AddComponent(rendererType);
                Reflect.Set(renderer, "sprite", sprite);

                // 칼 그림과 같은 재질·정렬 층을 써서 화면의 밝기·앞뒤 순서가 캐릭터와 어울리게 합니다.
                object body = Reflect.Get(Reflect.Get(weapon, "mainWeaponBody"), "weaponSpriteRenderer");
                if (!Reflect.IsNull(body))
                {
                    Reflect.Set(renderer, "sharedMaterial", Reflect.Get(body, "sharedMaterial"));
                    Reflect.Set(renderer, "sortingLayerID", Reflect.Get(body, "sortingLayerID"));
                }
                Reflect.Set(renderer, "sortingOrder", order);
                Log.Info("허리 칼집 생성 (x=" + x + ", y=" + y + ", angle=" + angle + ", scale=" + scale + ", order=" + order + ")");
                return holder;
            }
            catch (Exception e)
            {
                Log.Warn("허리 칼집을 만들지 못했습니다: " + e.Message);
                return null;
            }
        }

        public static void Destroy(GameObject holder)
        {
            if (!Reflect.IsNull(holder))
                UnityEngine.Object.Destroy(holder);
        }

        private static void ReadSettings(ref float x, ref float y, ref float angle, ref float scale, ref int order)
        {
            try
            {
                string path = Path.Combine(Entry.packageRoot, "Sprites", "scabbard.txt");
                if (!File.Exists(path))
                    return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Split('#')[0];
                    int equals = line.IndexOf('=');
                    if (equals < 0)
                        continue;
                    string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                    float value;
                    if (!float.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        continue;
                    if (key == "x") x = value;
                    else if (key == "y") y = value;
                    else if (key == "angle") angle = value;
                    else if (key == "scale") scale = value;
                    else if (key == "order") order = (int)value;
                }
            }
            catch (Exception)
            {
            }
        }
    }
}

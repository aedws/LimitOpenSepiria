using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 칼을 뽑아 든 동안 허리에 보이는 빈 칼집.
    ///
    /// 칼집은 캐릭터의 허리에 고정됩니다. 어느 쪽 허리인지는 손에 든 칼이 쉬고 있는 쪽을 보고 정합니다
    /// (칼과 같은 쪽에 있어야 칼집에서 뽑는 것처럼 보입니다). 공격 중에는 쪽을 바꾸지 않습니다.
    /// 위치·각도는 Sprites/scabbard.txt 에서 읽습니다.
    /// </summary>
    internal sealed class HipScabbard
    {
        private const float IdleSeconds = 0.3f; // 이만큼 공격이 없어야 '쉬는 자리'로 봅니다

        private static object sprite;

        private GameObject holder;
        private Transform anchor;     // 캐릭터(무기 조작부)
        private Transform blade;      // 손에 든 칼 그림
        private object controller;
        private float x = 0.4f, y = 0.45f, angle = 150f, scale = 1f;
        private int side;             // -1 또는 +1 (캐릭터 기준 좌우)
        private int idleSince = Environment.TickCount;

        public static HipScabbard Create(Component controller, Component weapon)
        {
            try
            {
                if (Reflect.IsNull(controller) || string.IsNullOrEmpty(Entry.packageRoot))
                    return null;
                var body = Reflect.Get(Reflect.Get(weapon, "mainWeaponBody"), "weaponSpriteRenderer") as Component;
                if (Reflect.IsNull(body))
                    return null;
                if (Reflect.IsNull(sprite))
                    sprite = SpriteLoader.Load(Path.Combine(Entry.packageRoot, "Sprites", "scabbard.png"), null, "칼집 그림");
                if (Reflect.IsNull(sprite))
                    return null;
                Type rendererType = typeof(Component).Assembly.GetType("UnityEngine.SpriteRenderer");
                if (rendererType == null)
                    return null;

                var created = new HipScabbard { controller = controller, anchor = controller.transform, blade = body.transform };
                int order = 0;
                created.ReadSettings(ref order);
                created.holder = new GameObject("FrostforgedCrescent_Scabbard");
                created.holder.layer = body.gameObject.layer;
                created.holder.transform.SetParent(created.anchor, false);

                Component renderer = created.holder.AddComponent(rendererType);
                Reflect.Set(renderer, "sprite", sprite);
                Reflect.Set(renderer, "sharedMaterial", Reflect.Get(body, "sharedMaterial"));
                Reflect.Set(renderer, "sortingLayerID", Reflect.Get(body, "sortingLayerID"));
                Reflect.Set(renderer, "sortingOrder", order);
                created.Place(created.BladeSide());
                Log.Info("허리 칼집 생성 (x=" + created.x + ", y=" + created.y + ", angle=" + created.angle + ", order=" + order + ", 칼 쪽=" + created.side + ")");
                return created;
            }
            catch (Exception e)
            {
                Log.Warn("칼집을 만들지 못했습니다: " + e.Message);
                return null;
            }
        }

        /// <summary>매 프레임(애니메이션 뒤). sheathed: 납도 상태면 손의 칼이 칼집째 그림이므로 숨깁니다.</summary>
        public void Update(bool sheathed)
        {
            if (Reflect.IsNull(holder) || Reflect.IsNull(blade) || Reflect.IsNull(anchor))
                return;
            bool show = !sheathed;
            if (holder.activeSelf != show)
                holder.SetActive(show);

            // 공격이 한동안 없을 때만 칼이 쉬는 쪽을 다시 봅니다(연속 공격 중에는 그대로).
            if (Convert.ToInt32(Reflect.Get(controller, "currentWeaponSwing")) != -1)
                idleSince = Environment.TickCount;
            else if (show && unchecked(Environment.TickCount - idleSince) > IdleSeconds * 1000f)
                Place(BladeSide());
        }

        // 손에 든 칼이 캐릭터의 어느 쪽에 있는지. 거의 가운데면 지금 쪽을 유지합니다.
        private int BladeSide()
        {
            float local = anchor.InverseTransformPoint(blade.position).x;
            if (local > 0.05f) return 1;
            if (local < -0.05f) return -1;
            return side == 0 ? -1 : side;
        }

        private void Place(int newSide)
        {
            if (newSide == side)
                return;
            side = newSide;
            Transform self = holder.transform;
            // 설정값은 왼쪽(-1) 기준. 오른쪽이면 좌우로 뒤집어 놓습니다.
            self.localPosition = new Vector3(side * Math.Abs(x), y, 0f);
            self.localEulerAngles = new Vector3(0f, 0f, side < 0 ? angle : -angle);
            self.localScale = new Vector3(side < 0 ? scale : -scale, scale, 1f);
        }

        public void Destroy()
        {
            if (!Reflect.IsNull(holder))
                UnityEngine.Object.Destroy(holder);
            holder = null;
        }

        private void ReadSettings(ref int order)
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

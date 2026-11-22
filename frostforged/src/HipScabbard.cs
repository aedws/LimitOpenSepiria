using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 칼을 뽑아 든 동안 보이는 빈 칼집.
    ///
    /// 칼집은 '칼이 쉬고 있는 자리'에 놓입니다. 공격하지 않는 동안에는 손에 든 칼의 위치·각도를 그대로 따라가
    /// 도신을 덮고, 공격이 시작되면 그 자리(캐릭터 기준)에 남습니다. 그래서 칼이 칼집에서 뽑혀 나가는 것처럼 보입니다.
    /// 칼집 그림은 칼 그림과 같은 크기·같은 기준점이라 겹쳐 놓으면 정확히 맞습니다.
    /// </summary>
    internal sealed class HipScabbard
    {
        private static object sprite;

        private GameObject holder;
        private Transform anchor;     // 캐릭터(무기 조작부)
        private Transform blade;      // 손에 든 칼 그림
        private object controller;
        private object renderer;
        private object group;        // 칼 그림의 정렬 묶음(캐릭터 앞뒤로 오갈 때 순서가 바뀝니다)
        private int offset;
        private int lastOrder = int.MinValue;

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
                    sprite = SpriteLoader.Load(Path.Combine(Entry.packageRoot, "Sprites", "scabbard.png"), FrameSet.Reference, "칼집 그림");
                if (Reflect.IsNull(sprite))
                    return null;

                Type rendererType = typeof(Component).Assembly.GetType("UnityEngine.SpriteRenderer");
                if (rendererType == null)
                    return null;

                var created = new HipScabbard { controller = controller, anchor = controller.transform, blade = body.transform };
                created.holder = new GameObject("FrostforgedCrescent_Scabbard");
                created.holder.layer = body.gameObject.layer;
                created.holder.transform.SetParent(created.anchor, false);

                Component renderer = created.holder.AddComponent(rendererType);
                Reflect.Set(renderer, "sprite", sprite);
                Reflect.Set(renderer, "sharedMaterial", Reflect.Get(body, "sharedMaterial"));
                Reflect.Set(renderer, "sortingLayerID", Reflect.Get(body, "sortingLayerID"));
                created.renderer = renderer;
                created.group = Reflect.Get(Reflect.Get(weapon, "mainWeaponBody"), "sortingGroup");
                created.offset = ReadOrder();
                created.Follow();
                Log.Info("칼집 생성 (칼이 쉬는 자리를 따라감)");
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
            if (!show)
                return;
            // 공격 중이 아닐 때만 칼을 따라갑니다. 공격 중에는 마지막 자리(캐릭터 기준)에 남습니다.
            if (Convert.ToInt32(Reflect.Get(controller, "currentWeaponSwing")) == -1)
                Follow();
        }

        private void Follow()
        {
            // 칼 바로 위에 그려지도록 칼의 정렬 순서 + 1 (+ 설정값).
            int order = (Reflect.IsNull(group) ? 0 : Convert.ToInt32(Reflect.Get(group, "sortingOrder"))) + 1 + offset;
            if (order != lastOrder)
            {
                Reflect.Set(renderer, "sortingOrder", order);
                lastOrder = order;
            }
            Transform self = holder.transform;
            self.position = blade.position;
            self.rotation = blade.rotation;
            Vector3 target = blade.lossyScale, parent = anchor.lossyScale;
            self.localScale = new Vector3(Divide(target.x, parent.x), Divide(target.y, parent.y), 1f);
        }

        private static float Divide(float value, float by)
        {
            return Math.Abs(by) < 0.0001f ? value : value / by;
        }

        public void Destroy()
        {
            if (!Reflect.IsNull(holder))
                UnityEngine.Object.Destroy(holder);
            holder = null;
        }

        // Sprites/scabbard.txt 의 order = N (칼 기준 앞뒤 순서 보정). 없으면 0.
        private static int ReadOrder()
        {
            try
            {
                string path = Path.Combine(Entry.packageRoot, "Sprites", "scabbard.txt");
                if (!File.Exists(path))
                    return 0;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Split('#')[0];
                    int equals = line.IndexOf('=');
                    float value;
                    if (equals > 0 && line.Substring(0, equals).Trim().ToLowerInvariant() == "order"
                        && float.TryParse(line.Substring(equals + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        return (int)value;
                }
            }
            catch (Exception)
            {
            }
            return 0;
        }
    }
}

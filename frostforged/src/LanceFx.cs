using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 블루비틀의 공격 이펙트(찌르기·휩쓸기 궤적)를 얼음 궤적으로 바꿉니다.
    ///
    /// 궤적의 모양과 박자는 기오로이드의 것과 정확히 같아야 하므로, 이펙트의 각 프레임을 실행 중에 읽어
    /// 얼음 궤적으로 다시 칠한 전용 프레임을 만듭니다: 굵은 몸통은 흰색 그대로, 가는 줄기와 꼬리는 얼음빛
    /// ('무녀의 예언'의 궤적에서 뽑은 두 가지 색). 블루비틀의 이펙트가 화면에 있는 동안만 그 프레임으로 바꿔 그리고,
    /// 이펙트가 사라지면 원래대로 돌아가므로 다른 무기의 이펙트는 바뀌지 않습니다.
    /// 화면 표시만 바꾸므로 이 모드를 쓰는 각자의 화면에 적용됩니다.
    /// </summary>
    internal static class LanceFx
    {
        // 기수별 궤적 색: [넓은 면, 가는 줄기]. 순서는 LanceAddon 의 종류 번호(4기사는 원래 흰 궤적 그대로).
        private static readonly Color32[][] Palettes =
        {
            null,
            new[] { new Color32(208, 232, 248, 255), new Color32(152, 200, 248, 255) }, // 블루비틀: 얼음빛
            new[] { new Color32(255, 222, 150, 255), new Color32(255, 150, 70, 255) },  // 화염기수: 불빛
            new[] { new Color32(255, 247, 170, 255), new Color32(190, 150, 255, 255) }, // 뇌격기수: 번개빛
            // 종말기수: 우주 속성. 굵은 몸통까지 물들입니다(세 번째 색) — 몸통은 옅은 별빛, 줄기는 보라, 가는 줄기는 짙은 보라.
            new[] { new Color32(168, 133, 255, 255), new Color32(104, 64, 200, 255), new Color32(226, 212, 255, 255) },
            // 흑요도 : 참월 II형: 검은 궤적. 몸통은 칠흑, 줄기는 보라, 가는 줄기는 옅은 별빛.
            new[] { new Color32(120, 84, 214, 255), new Color32(214, 196, 255, 255), new Color32(20, 14, 34, 255) },
        };
        /// <summary>흑요도의 궤적 색 번호(기수 번호 뒤에 이어 붙였습니다)</summary>
        public const int ObsidianPalette = 5;
        private static int painting; // 지금 만들고 있는 프레임의 종류

        private static bool HasPalette(int kind)
        {
            return kind > 0 && kind < Palettes.Length && Palettes[kind] != null;
        }
        private const int CoreDepth = 4;   // 가장자리에서 이만큼 들어간 곳이 근처에 있으면 '굵은 몸통'
        private const int CoreReach = 3;

        private static readonly Dictionary<Sprite, Sprite>[] Frames = Enumerable.Range(0, 6).Select(_ => new Dictionary<Sprite, Sprite>()).ToArray();
        private static readonly HashSet<object>[] PreparedSets = Enumerable.Range(0, 6).Select(_ => new HashSet<object>()).ToArray();
        private static readonly Dictionary<object, int> Active = new Dictionary<object, int>(); // 지금 전용 프레임으로 그릴 애니메이터 → 종류
        private static int capturedKind;

        // 블루비틀 전용으로 복제한 공격 데이터(이것으로 만들어진 이펙트만 물들입니다)
        private static readonly Dictionary<object, int> FireData = new Dictionary<object, int>();
        private static List<object> captured;
        private static bool logged, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type fire = Reflect.GameType("NewWeaponFireData");
            Type melee = Reflect.GameType("MeleeCollision");
            Type fx = Reflect.GameType("SpriteFx");
            MethodInfo create = fire == null ? null : AccessTools.GetDeclaredMethods(fire).FirstOrDefault(m => m.Name == "CreateSwingFx");
            MethodInfo spawn = melee == null ? null : AccessTools.GetDeclaredMethods(melee).FirstOrDefault(m =>
                m.Name.StartsWith("UserCode_RpcSpawn") && m.GetParameters().Length > 0 && m.GetParameters()[0].Name == "owner");
            MethodInfo onSpawn = fx == null ? null : AccessTools.DeclaredMethod(fx, "OnSpawn");
            if (onSpawn == null || (create == null && spawn == null))
            {
                Log.Warn("공격 이펙트 지점을 찾지 못해 블루비틀의 이펙트 색은 바뀌지 않습니다.");
                return;
            }
            Type renderer = Reflect.GameType("Animator2D_SpriteRenderer");
            MethodInfo setSprite = renderer == null ? null : AccessTools.GetDeclaredMethods(renderer).FirstOrDefault(m =>
                m.Name == "SetSprite" && m.GetParameters().Length == 1 && m.GetParameters()[0].Name == "sprite");
            MethodInfo onDespawn = AccessTools.DeclaredMethod(fx, "OnDespawn");
            if (setSprite == null || onDespawn == null)
            {
                Log.Warn("이펙트 그리는 지점을 찾지 못해 블루비틀의 이펙트는 원래 모습으로 나옵니다.");
                return;
            }
            Type self = typeof(LanceFx);
            harmony.Patch(setSprite, prefix: new HarmonyMethod(self, nameof(SetSpritePrefix)));
            harmony.Patch(onDespawn, postfix: new HarmonyMethod(self, nameof(DespawnPostfix)));
            harmony.Patch(onSpawn, postfix: new HarmonyMethod(self, nameof(SpawnPostfix)));
            if (create != null)
                harmony.Patch(create, prefix: new HarmonyMethod(self, nameof(CreatePrefix)), postfix: new HarmonyMethod(self, nameof(Paint)));
            if (spawn != null)
                harmony.Patch(spawn, prefix: new HarmonyMethod(self, nameof(MeleePrefix)), postfix: new HarmonyMethod(self, nameof(Paint)));
        }

        /// <summary>이 공격 데이터로 만들어지는 이펙트도 얼음 궤적으로 그립니다.</summary>
        public static void Claim(UnityEngine.Object fireData, int kind)
        {
            FireData[fireData] = kind;
        }

        /// <summary>이 공격 데이터가 어느 기수의 것인지(이 모드의 것이 아니면 -1)</summary>
        public static int KindOf(object fireData)
        {
            int kind;
            return fireData != null && FireData.TryGetValue(fireData, out kind) ? kind : -1;
        }

        /// <summary>무기 프리팹의 공격 데이터를 이 무기 전용으로 복제해 둡니다(내용은 그대로).</summary>
        public static void Claim(Component weapon, int kind)
        {
            foreach (string field in new[] { "basicComboAttacks", "dashAttacks", "specialAttacks", "doubleBasicComboAttacks" })
            {
                var list = Reflect.Get(weapon, field) as IList;
                if (list == null)
                    continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var data = list[i] as UnityEngine.Object;
                    if (Reflect.IsNull(data))
                        continue;
                    UnityEngine.Object copy = UnityEngine.Object.Instantiate(data);
                    copy.name = data.name;
                    Reflect.Set(copy, "hideFlags", 61);
                    list[i] = copy;
                    FireData[copy] = kind;
                }
            }
        }

        private static void CreatePrefix(object __instance, out bool __state)
        {
            int kind = KindOf(__instance);
            __state = HasPalette(kind);
            capturedKind = kind;
            captured = __state ? new List<object>() : null;
        }

        // MeleeCollision 이 직접 만드는 궤적: 블루비틀을 든 캐릭터의 무기 공격일 때만
        private static void MeleePrefix(object __instance, object owner, out bool __state)
        {
            __state = false;
            try
            {
                capturedKind = CrescentAddon.KindOf(owner) == CrescentAddon.Obsidian ? ObsidianPalette : LanceAddon.KindOf(owner);
                __state = HasPalette(capturedKind) && Convert.ToString(Reflect.Get(__instance, "fromType")) == "DirectAttack";
            }
            catch (Exception)
            {
            }
            captured = __state ? new List<object>() : null;
        }

        private static void SpawnPostfix(object __instance)
        {
            if (captured != null)
                captured.Add(__instance);
        }

        private static void Paint(bool __state)
        {
            List<object> list = captured;
            int kind = capturedKind;
            captured = null;
            if (!__state || list == null || !HasPalette(kind))
                return;
            try
            {
                foreach (object fx in list)
                    foreach (object animator in Animators(fx))
                    {
                        Prepare(animator, kind);
                        Active[animator] = kind;
                        // 첫 프레임은 이미 그려졌으니 지금 바꿔 줍니다.
                        object renderer = Reflect.Get(animator, "spriteRenderer");
                        var current = Reflect.IsNull(renderer) ? null : Reflect.Get(renderer, "sprite") as Sprite;
                        Sprite frame;
                        if (!Reflect.IsNull(current) && Frames[kind].TryGetValue(current, out frame))
                            Reflect.Set(renderer, "sprite", frame);
                    }
                if (!logged && list.Count > 0)
                {
                    logged = true;
                    Log.Info("기수 전용 궤적 적용 확인 (종류 " + kind + ", 전용 프레임 " + Frames[kind].Count + "장)");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static void Fail(Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("블루비틀 이펙트 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }

        private static IEnumerable<object> Animators(object fx)
        {
            object main = Reflect.Get(fx, "animator2D");
            if (!Reflect.IsNull(main))
                yield return main;
            var subs = Reflect.Get(fx, "subAnimator2D") as IEnumerable;
            if (subs != null)
                foreach (object sub in subs)
                    if (!Reflect.IsNull(sub))
                        yield return sub;
        }

        // 이 애니메이터가 쓰는 그림 묶음의 모든 프레임을 한 번에 얼음 궤적으로 만들어 둡니다.
        private static void Prepare(object animator, int kind)
        {
            object set = Reflect.Get(animator, "currentSet");
            if (Reflect.IsNull(set) || !PreparedSets[kind].Add(set))
                return;
            painting = kind;
            var states = Reflect.Get(set, "sprites") as IEnumerable;
            if (states == null)
                return;
            foreach (object state in states)
            {
                var timeline = Reflect.Get(state, "timeline") as IEnumerable;
                if (timeline == null)
                    continue;
                foreach (object key in timeline)
                {
                    var sprite = Reflect.Get(key, "sprite") as Sprite;
                    if (Reflect.IsNull(sprite) || Frames[kind].ContainsKey(sprite))
                        continue;
                    Sprite frame = SpriteTint.Remap(sprite, null, 1f, "_FFC" + kind, IceTrail);
                    if (frame != null)
                        Frames[kind][sprite] = frame;
                }
            }
            SpriteTint.Release();
        }

        private static void SetSpritePrefix(object __instance, ref Sprite sprite)
        {
            int kind;
            if (Active.Count == 0 || Reflect.IsNull(sprite) || !Active.TryGetValue(__instance, out kind))
                return;
            Sprite frame;
            if (Frames[kind].TryGetValue(sprite, out frame))
                sprite = frame;
        }

        private static void DespawnPostfix(object __instance)
        {
            if (Active.Count == 0)
                return;
            try
            {
                foreach (object animator in Animators(__instance))
                    Active.Remove(animator);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>
        /// 흰 궤적 한 장을 얼음 궤적으로: 굵은 몸통(가장자리에서 CoreDepth 이상 들어간 곳이 가까이 있는 부분)은 그대로 두고,
        /// 가는 줄기는 얼음빛으로, 아주 가는(1~2픽셀) 줄기는 진한 하늘색으로 칠합니다. 밝지 않은 픽셀과 투명도는 그대로입니다.
        /// </summary>
        private static Color32[] IceTrail(Color32[] pixels, int width, int height)
        {
            int count = pixels.Length;
            var depth = new int[count];
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                int x = i % width, y = i / width;
                if (pixels[i].a == 0)
                {
                    depth[i] = 0;
                    queue.Enqueue(i);
                }
                else if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    depth[i] = 1;
                    queue.Enqueue(i);
                }
                else
                {
                    depth[i] = int.MaxValue;
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % width, y = i / width, next = depth[i] + 1;
                if (next > CoreDepth)
                    continue;
                if (x > 0 && depth[i - 1] > next) { depth[i - 1] = next; queue.Enqueue(i - 1); }
                if (x < width - 1 && depth[i + 1] > next) { depth[i + 1] = next; queue.Enqueue(i + 1); }
                if (y > 0 && depth[i - width] > next) { depth[i - width] = next; queue.Enqueue(i - width); }
                if (y < height - 1 && depth[i + width] > next) { depth[i + width] = next; queue.Enqueue(i + width); }
            }

            var result = (Color32[])pixels.Clone();
            for (int i = 0; i < count; i++)
            {
                Color32 c = pixels[i];
                if (c.a == 0 || Math.Min(c.r, Math.Min(c.g, c.b)) < 180)
                    continue;
                int x = i % width, y = i / width, deepest = 0;
                for (int dy = -CoreReach; dy <= CoreReach && deepest < CoreDepth; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= height)
                        continue;
                    for (int dx = -CoreReach; dx <= CoreReach; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= width || pixels[yy * width + xx].a == 0)
                            continue;
                        int d = Math.Min(depth[yy * width + xx], CoreDepth);
                        if (d > deepest)
                            deepest = d;
                    }
                }
                if (deepest >= CoreDepth)
                {
                    if (Palettes[painting].Length > 2) // 몸통 색이 정해진 기수만 몸통도 칠합니다
                        result[i] = new Color32(Palettes[painting][2].r, Palettes[painting][2].g, Palettes[painting][2].b, c.a);
                    continue; // 그 밖에는 굵은 몸통은 흰색 그대로
                }
                Color32 ice = deepest <= 1 ? Palettes[painting][1] : Palettes[painting][0];
                result[i] = new Color32(ice.r, ice.g, ice.b, c.a);
            }
            return result;
        }
    }
}

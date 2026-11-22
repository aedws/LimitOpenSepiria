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
    /// 블루비틀의 공격 이펙트(찌르기·휩쓸기 궤적)를 얼음빛으로 물들입니다.
    /// 이펙트 그림은 기오로이드의 것을 그대로 쓰고, 만들어지는 순간 색만 입힙니다(사라질 때 게임이 원래 색으로 되돌립니다).
    /// 색은 '무녀의 예언'의 공격 이펙트에 지정된 색을 따르고, 따로 지정된 색이 없으면 기본 얼음빛을 씁니다.
    /// 화면 표시만 바꾸므로 이 모드를 쓰는 각자의 화면에 적용됩니다.
    /// </summary>
    internal static class LanceFx
    {
        public static Color Tint = new Color(0.55f, 0.8f, 1f, 1f);

        // 블루비틀 전용으로 복제한 공격 데이터(이것으로 만들어진 이펙트만 물들입니다)
        private static readonly HashSet<object> FireData = new HashSet<object>();
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
            Type self = typeof(LanceFx);
            harmony.Patch(onSpawn, postfix: new HarmonyMethod(self, nameof(SpawnPostfix)));
            if (create != null)
                harmony.Patch(create, prefix: new HarmonyMethod(self, nameof(CreatePrefix)), postfix: new HarmonyMethod(self, nameof(Paint)));
            if (spawn != null)
                harmony.Patch(spawn, prefix: new HarmonyMethod(self, nameof(MeleePrefix)), postfix: new HarmonyMethod(self, nameof(Paint)));
        }

        /// <summary>무기 프리팹의 공격 데이터를 이 무기 전용으로 복제해 둡니다(내용은 그대로).</summary>
        public static void Claim(Component weapon)
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
                    FireData.Add(copy);
                }
            }
        }

        /// <summary>'무녀의 예언'의 공격 이펙트에 지정된 색이 있으면 그 색을 씁니다.</summary>
        public static void LearnTint(Component lookWeapon)
        {
            try
            {
                var list = Reflect.IsNull(lookWeapon) ? null : Reflect.Get(lookWeapon, "basicComboAttacks") as IList;
                if (list == null)
                    return;
                foreach (object data in list)
                {
                    if (Reflect.IsNull(data))
                        continue;
                    var prefabs = new List<object> { Reflect.Get(data, "swingFxPrefab") };
                    var projectile = Reflect.Get(data, "projectilePrefab") as GameObject;
                    Type melee = Reflect.GameType("MeleeCollision");
                    if (!Reflect.IsNull(projectile) && melee != null && !Reflect.IsNull(projectile.GetComponent(melee)))
                        prefabs.Add(Reflect.Get(projectile.GetComponent(melee), "swingFxPrefab"));
                    foreach (object prefab in prefabs)
                    {
                        var fxObject = prefab as GameObject;
                        Component fx = Reflect.IsNull(fxObject) ? null : fxObject.GetComponent(Reflect.GameType("SpriteFx"));
                        object animator = Reflect.IsNull(fx) ? null : Reflect.Get(fx, "animator2D");
                        if (Reflect.IsNull(animator))
                            continue;
                        var color = (Color)Reflect.Call(animator, null, "GetColor");
                        if (color.r > 0.97f && color.g > 0.97f && color.b > 0.97f)
                            continue; // 흰색이면 색이 그림에 들어 있는 것이라 가져올 수 없습니다
                        Tint = new Color(color.r, color.g, color.b, 1f);
                        Log.Info("블루비틀 이펙트 색: 무녀의 예언의 이펙트 색 (" + color.r.ToString("0.##") + ", " + color.g.ToString("0.##") + ", " + color.b.ToString("0.##") + ")");
                        return;
                    }
                }
                Log.Info("블루비틀 이펙트 색: 무녀의 예언의 이펙트에 따로 지정된 색이 없어 기본 얼음빛을 씁니다.");
            }
            catch (Exception e)
            {
                Log.Warn("무녀의 예언의 이펙트 색을 읽지 못해 기본 얼음빛을 씁니다: " + e.Message);
            }
        }

        private static void CreatePrefix(object __instance, out bool __state)
        {
            __state = FireData.Contains(__instance);
            captured = __state ? new List<object>() : null;
        }

        // MeleeCollision 이 직접 만드는 궤적: 블루비틀을 든 캐릭터의 무기 공격일 때만
        private static void MeleePrefix(object __instance, object owner, out bool __state)
        {
            __state = false;
            try
            {
                __state = LanceAddon.IsWielding(owner) && Convert.ToString(Reflect.Get(__instance, "fromType")) == "DirectAttack";
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
            captured = null;
            if (!__state || list == null)
                return;
            try
            {
                foreach (object fx in list)
                {
                    object animator = Reflect.Get(fx, "animator2D");
                    if (Reflect.IsNull(animator))
                        continue;
                    var current = (Color)Reflect.Call(animator, null, "GetColor");
                    Reflect.Call(fx, null, "SetColor", new Color(current.r * Tint.r, current.g * Tint.g, current.b * Tint.b, current.a));
                }
                if (!logged && list.Count > 0)
                {
                    logged = true;
                    Log.Info("블루비틀 이펙트 색 적용 확인");
                }
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("블루비틀 이펙트 색 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
            }
        }
    }
}

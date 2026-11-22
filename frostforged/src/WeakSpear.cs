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
    /// 약화된 볼루스파: 절반 크기·절반 피해로, 충전 시간과 관계없이 조준 방향에 발동합니다.
    /// 게임의 볼루스파 발동 경로를 그대로 쓰므로 얼음 무구 추가 발동 효과가 적용됩니다.
    /// 약화 여부는 피해 배율 값으로 구분합니다(추가 발동에도 같은 배율이 전달됩니다).
    /// 크기 축소는 서버(호스트)가 만드는 창에 적용됩니다.
    /// </summary>
    internal static class WeakSpear
    {
        /// <summary>약화 발동의 피해 배율. 0.5 에 표식용 소수점을 붙인 값입니다.</summary>
        public const float DamageMultiplier = 0.50001f;
        private const float SizeScale = 0.5f;

        private static List<Component> captured;
        private static readonly Dictionary<Component, Vector3> Original = new Dictionary<Component, Vector3>();
        private static bool ready, logged, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type spear = Reflect.GameType("Charm_IceSpear");
            Type bullet = Reflect.GameType("Bullet");
            MethodInfo coroutine = spear != null ? AccessTools.GetDeclaredMethods(spear).FirstOrDefault(m => m.Name == "FireSpearCoroutine") : null;
            MethodInfo spawn = bullet != null ? AccessTools.DeclaredMethod(bullet, "OnSpawn") : null;
            MethodInfo despawn = bullet != null ? AccessTools.DeclaredMethod(bullet, "OnDespawn") : null;
            if (coroutine == null || spawn == null || despawn == null
                || !coroutine.GetParameters().Any(p => p.Name == "damageMultiplier" && p.ParameterType == typeof(float)))
            {
                Log.Warn("볼루스파 발사 지점을 찾지 못해 약화된 볼루스파의 크기는 줄어들지 않습니다.");
                return;
            }
            harmony.Patch(coroutine, postfix: new HarmonyMethod(typeof(WeakSpear), nameof(CoroutinePostfix)));
            harmony.Patch(spawn, postfix: new HarmonyMethod(typeof(WeakSpear), nameof(SpawnPostfix)));
            harmony.Patch(despawn, postfix: new HarmonyMethod(typeof(WeakSpear), nameof(DespawnPostfix)));
            ready = true;

            // 무녀의 예언과 같은 자리: 볼루스파가 주인의 적중을 받는 곳. 블루비틀을 들고 있으면 적중마다 약화 발동합니다.
            MethodInfo onAttack = AccessTools.GetDeclaredMethods(spear).FirstOrDefault(m => m.Name == "OnAttackUnit" && m.GetParameters().Length == 2);
            noCount = Reflect.GameType("ChargingCharm") == null ? null
                : Reflect.GameType("ChargingCharm").GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "ActivateChargingCharmNoCount" && m.GetParameters().Length == 3);
            if (onAttack == null || noCount == null)
                Log.Warn("볼루스파의 적중 지점을 찾지 못해 블루비틀의 볼루스파 발동이 동작하지 않습니다.");
            else
                harmony.Patch(onAttack, postfix: new HarmonyMethod(typeof(WeakSpear), nameof(AttackPostfix)));
        }

        private static MethodInfo noCount;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Stamp> LastFired
            = new System.Runtime.CompilerServices.ConditionalWeakTable<object, Stamp>();
        private sealed class Stamp { public long Ticks; }
        private const long MinIntervalTicks = 1000000; // 0.1초: 한 번 찌를 때 여러 적이 맞아도 한 번만

        // Charm_IceSpear.OnAttackUnit(맞은 대상, 피해). 서버에서 불립니다.
        private static void AttackPostfix(object __instance, object instance)
        {
            try
            {
                if (instance == null || Convert.ToString(Reflect.Get(instance, "fromType")) != "DirectAttack")
                    return;
                if (!LanceAddon.IsWielding(Reflect.Get(__instance, "NetworkAvatar")))
                    return;
                Stamp last = LastFired.GetOrCreateValue(__instance);
                long now = DateTime.UtcNow.Ticks;
                if (now - last.Ticks < MinIntervalTicks)
                    return;
                last.Ticks = now;
                object charging = Reflect.Get(__instance, "chargingCharm");
                if (Reflect.IsNull(charging))
                    return;
                noCount.Invoke(charging, new object[] { DamageMultiplier, true, "WEAPON" });
                if (!logged)
                {
                    logged = true;
                    Log.Info("약화된 볼루스파 발동 확인" + (ready ? "" : " (크기 축소는 적용되지 않음)"));
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
            Log.Error("약화된 볼루스파 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }

        private static void CoroutinePostfix(ref IEnumerator __result, float damageMultiplier)
        {
            if (__result != null && Math.Abs(damageMultiplier - DamageMultiplier) < 0.000001f)
                __result = Wrap(__result);
        }

        // 창은 이 코루틴이 한 걸음 진행할 때마다 만들어집니다. 그동안 생긴 탄환을 모아 크기를 줄입니다.
        private static IEnumerator Wrap(IEnumerator inner)
        {
            while (true)
            {
                bool more;
                var mine = new List<Component>();
                List<Component> previous = captured;
                captured = mine;
                try
                {
                    more = inner.MoveNext();
                }
                finally
                {
                    captured = previous;
                }
                try
                {
                    foreach (Component bullet in mine)
                    {
                        if (Reflect.IsNull(bullet) || Original.ContainsKey(bullet))
                            continue;
                        Vector3 scale = bullet.transform.localScale;
                        Original[bullet] = scale;
                        bullet.transform.localScale = new Vector3(scale.x * SizeScale, scale.y * SizeScale, scale.z);
                    }
                }
                catch (Exception e)
                {
                    Fail(e);
                }
                if (!more)
                    yield break;
                yield return inner.Current;
            }
        }

        private static void SpawnPostfix(Component __instance)
        {
            if (captured != null)
                captured.Add(__instance);
        }

        // 탄환은 재사용되므로, 사라질 때 원래 크기로 돌려놓습니다.
        private static void DespawnPostfix(Component __instance)
        {
            Vector3 scale;
            if (Original.Count == 0 || !Original.TryGetValue(__instance, out scale))
                return;
            Original.Remove(__instance);
            try
            {
                if (!Reflect.IsNull(__instance))
                    __instance.transform.localScale = scale;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}

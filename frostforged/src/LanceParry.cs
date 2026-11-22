using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 블루비틀의 우클릭: 단검의 패링 + 패링 성공 시 돌파 찌르기.
    ///
    /// 패링 판정은 무기 종류와 상관없는 게임의 공용 기능(WeaponControllerSimple.StartParryInvincible)을 그대로 씁니다.
    /// 단검과 같이 MP 를 쓰고, 성공하면 절반을 돌려받습니다. 패링에 성공하면(게임이 "PARRY!" 를 띄우는 그 순간)
    /// 조준 방향으로 돌진하며 창격의 마지막 찌르기를 강화해 내지릅니다. 모두 서버(호스트)에서 처리합니다.
    /// 봉류에는 패링 동작 애니메이션이 없어, 패링 중에는 게임의 패링 빛(캐릭터 테두리)만 표시됩니다.
    /// </summary>
    internal static class LanceParry
    {
        public const int ParryCost = 6;            // 단검 패링과 같은 MP
        public const float ParryWindow = 0.3f;     // 패링 판정 시간(초)
        public const float Cooldown = 0.8f;        // 다시 쓸 수 있을 때까지(초)
        public const float CounterDamage = 2.5f;   // 돌파 찌르기 피해 배율(마지막 찌르기 대비)
        public const float DashSpeed = 26f;        // 돌파 속도(칸/초)
        public const float DashTime = 0.18f;       // 돌파 시간(초)
        public const float CounterInvulnerable = 0.4f;

        private sealed class State { public long LastTicks; public bool Paid; }

        private static readonly ConditionalWeakTable<object, State> States = new ConditionalWeakTable<object, State>();
        private static bool loggedParry, loggedCounter, loggedFailure;

        public static void Apply(Harmony harmony)
        {
            Type staff = Reflect.GameType("WeaponSimple_QuartterStaff");
            Type unit = Reflect.GameType("UnitAvatar");
            MethodInfo sub = staff == null ? null : AccessTools.GetDeclaredMethods(staff).FirstOrDefault(m => m.Name == "SubAttackButtonDown" && m.GetParameters().Length == 0);
            MethodInfo parried = unit == null ? null : AccessTools.GetDeclaredMethods(unit).FirstOrDefault(m => m.Name == "RpcCreateParryFx" && m.GetParameters().Length == 2);
            if (sub == null || parried == null)
            {
                Log.Warn("우클릭·패링 지점을 찾지 못해 블루비틀의 패링은 동작하지 않습니다(원래 특수 공격이 나갑니다).");
                return;
            }
            harmony.Patch(sub, prefix: new HarmonyMethod(typeof(LanceParry), nameof(SubPrefix)));
            harmony.Patch(parried, prefix: new HarmonyMethod(typeof(LanceParry), nameof(ParriedPrefix)));
        }

        private static void Fail(string where, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("블루비틀 패링 처리 중 오류(" + where + ", 이후 같은 오류는 기록하지 않습니다): " + e);
        }

        // 우클릭: 원래 특수 공격 대신 패링을 겁니다. 서버에서 불립니다.
        private static bool SubPrefix(Component __instance)
        {
            try
            {
                if (Reflect.IsNull(__instance) || Reflect.IsNull(__instance.GetComponent(typeof(LanceAddon))))
                    return true;
                object controller = Reflect.Get(__instance, "Networkowner");
                object avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar) || !Equals(Reflect.Get(controller, "isServer"), true))
                    return false;
                if (Convert.ToInt32(Reflect.Get(controller, "currentWeaponSwing")) >= 10)
                    return false;

                State state = States.GetOrCreateValue(avatar);
                long now = DateTime.UtcNow.Ticks;
                if (now - state.LastTicks < (long)(Cooldown * TimeSpan.TicksPerSecond))
                    return false;
                if (Convert.ToInt32(Reflect.Get(avatar, "MP")) < ParryCost)
                    return false;

                Reflect.Call(avatar, null, "UseMp", ParryCost);
                Reflect.Call(controller, null, "StartParryInvincible", ParryWindow);
                state.LastTicks = now;
                state.Paid = true;
                if (!loggedParry)
                {
                    loggedParry = true;
                    Log.Info("블루비틀 패링 발동 확인 (MP " + ParryCost + ", 판정 " + ParryWindow + "초)");
                }
            }
            catch (Exception e)
            {
                Fail("우클릭", e);
            }
            return false;
        }

        // 패링 성공: 게임이 패링 이펙트를 띄우는 순간(서버). 블루비틀을 들고 있으면 돌파 찌르기를 내지릅니다.
        private static void ParriedPrefix(object __instance)
        {
            try
            {
                LanceAddon addon = LanceAddon.Find(__instance);
                if (addon == null || Reflect.IsNull(addon.parent))
                    return;
                State state = States.GetOrCreateValue(__instance);
                if (state.Paid)
                {
                    state.Paid = false;
                    Reflect.Call(__instance, null, "HealMp", ParryCost / 2);
                }
                state.LastTicks = 0; // 성공하면 바로 다시 쓸 수 있습니다
                Counter(addon.parent, __instance);
            }
            catch (Exception e)
            {
                Fail("패링 성공", e);
            }
        }

        private static void Counter(Component weapon, object avatar)
        {
            object controller = Reflect.Get(weapon, "Networkowner");
            var attacks = Reflect.Get(weapon, "basicComboAttacks") as IList;
            if (Reflect.IsNull(controller) || attacks == null || attacks.Count == 0)
                return;
            int idx = attacks.Count - 1; // 창격의 마지막(긴) 찌르기
            object data = attacks[idx];
            if (Reflect.IsNull(data))
                return;

            Vector3 origin = ((Component)avatar).transform.position;
            var aim = (Vector3)Reflect.Get(controller, "aimedPositionClientside");
            float dx = aim.x - origin.x, dy = aim.y - origin.y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f)
            {
                dx = 1f;
                dy = 0f;
                length = 1f;
            }
            float nx = dx / length, ny = dy / length;

            // 찌르기가 나가는 자리: 무기를 휘두르는 지점(없으면 캐릭터 위치)
            Vector3 position = origin;
            object shoulder = Reflect.Get(controller, "shoulder");
            var swingPoint = Reflect.IsNull(shoulder) ? null : Reflect.Get(shoulder, "swingPoint") as Transform;
            if (!Reflect.IsNull(swingPoint))
                position = swingPoint.position;

            MethodInfo create = typeof(WeaponSimple).GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "CreateBasicAttackProjectileFullyManual" && m.GetParameters().Length == 8);
            if (create == null)
                return;
            create.Invoke(weapon, new[] { data, idx, position, (object)new Vector3(dx, dy, 0f), null, 0f, 0, CounterDamage });

            // 돌파: 조준 방향으로 짧고 빠르게 돌진하고, 그동안 무적입니다.
            MethodInfo dash = avatar.GetType().GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "SetAttackDash" && m.GetParameters().Length == 3);
            if (dash != null)
                dash.Invoke(avatar, new[] { (object)new Vector3(nx * DashSpeed, ny * DashSpeed, 0f), Enum.ToObject(dash.GetParameters()[1].ParameterType, 1), DashTime }); // 1 = Uniform
            MethodInfo invulnerable = avatar.GetType().GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "StartTempInvulnerable" && m.GetParameters().Length == 1);
            if (invulnerable != null)
                invulnerable.Invoke(avatar, new object[] { CounterInvulnerable });

            // 찌르기 궤적(이 화면에만 표시됩니다)
            try
            {
                MethodInfo fx = typeof(WeaponSimple).GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "CreateBasicAttackSwingFxFullyManual" && m.GetParameters().Length == 1);
                if (fx != null && Equals(Reflect.Get(weapon, "isClient"), true))
                    fx.Invoke(weapon, new[] { data });
            }
            catch (Exception)
            {
            }

            if (!loggedCounter)
            {
                loggedCounter = true;
                Log.Info("블루비틀 돌파 찌르기 발동 확인 (피해 " + CounterDamage + "배)");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리기수 : 돌격창 블루비틀'에 붙는 무기 효과.
    ///  - 공격 속도 +20%
    ///  - 무기 공격이 적중하면 20% 확률로 동상 부여 (피해 처리 지점에서 호출: ItemEffects)
    ///  - 무기 공격이 적중하면 보유한 볼루스파가 약화된 형태로 발동 (볼루스파의 적중 처리에서 호출: WeakSpear)
    /// 창격 3·4·5타 반복과 대시 공격 금지는 LanceMotion 이 이 컴포넌트가 붙은 무기에만 적용합니다.
    /// </summary>
    public class LanceAddon : WeaponAddon
    {
        public const int AttackSpeedBonus = 20;
        public const int FrostbitePercent = 20;

        private static readonly System.Random Dice = new System.Random();
        private static readonly List<object> Wielders = new List<object>();
        private static bool loggedFrostbite, loggedFailure;

        private object avatar;
        private bool statApplied;

        protected override void OnEnableAddon()
        {
            base.OnEnableAddon();
            try
            {
                object controller = Reflect.Get(parent, "Networkowner");
                avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar))
                    return;
                if (!Wielders.Contains(avatar))
                    Wielders.Add(avatar);
                Addons.RemoveAll(x => Reflect.IsNull(x) || ReferenceEquals(x, this));
                Addons.Add(this);
                if (Equals(Reflect.Get(this, "isServer"), true))
                    statApplied = AddAttackSpeed(AttackSpeedBonus);
                Log.Info("블루비틀 무기 효과 활성화 (공격 속도 +" + AttackSpeedBonus + "% " + (statApplied ? "적용" : "미적용") + ")");
            }
            catch (Exception e)
            {
                Log.Warn("블루비틀 무기 효과 활성화 중 오류: " + e);
            }
        }

        protected override void OnDisableAddon()
        {
            base.OnDisableAddon();
            try
            {
                Wielders.Remove(avatar);
                Addons.Remove(this);
                LanceParry.Clear(avatar);
                SetDashBonus(0);
                if (statApplied)
                    AddAttackSpeed(-AttackSpeedBonus);
                statApplied = false;
            }
            catch (Exception e)
            {
                Log.Warn("블루비틀 무기 효과 해제 중 오류: " + e);
            }
        }

        // 전용 아티팩트의 '대시 회복 속도': 블루비틀을 들고 있는 동안만 실제 능력치에 반영합니다(서버).
        private int dashApplied;
        private float dashCheck;

        private void Update()
        {
            if (!statApplied || Reflect.IsNull(avatar))
                return;
            dashCheck -= Time.deltaTime;
            if (dashCheck > 0f)
                return;
            dashCheck = 0.25f;
            try
            {
                SetDashBonus(Math.Max(0, ItemEffects.Stat(avatar, ItemContent.LanceDash)));
            }
            catch (Exception)
            {
            }
        }

        private void SetDashBonus(int value)
        {
            if (value == dashApplied || Reflect.IsNull(avatar))
                return;
            if (AddStat("DashRecovery", value - dashApplied))
                dashApplied = value;
        }

        private bool AddAttackSpeed(int value)
        {
            return AddStat("AttackSpeed", value);
        }

        private bool AddStat(string name, int value)
        {
            Type stat = Reflect.GameType("ECustomStat");
            MethodInfo add = stat == null || Reflect.IsNull(avatar) ? null : avatar.GetType().GetMethod("AddCustomStat", new[] { stat, typeof(int) });
            if (add == null)
                return false;
            add.Invoke(avatar, new[] { Enum.Parse(stat, name), value });
            return true;
        }

        private static readonly List<LanceAddon> Addons = new List<LanceAddon>();

        /// <summary>avatar 가 들고 있는 블루비틀의 무기 효과(없으면 null)</summary>
        public static LanceAddon Find(object avatar)
        {
            foreach (LanceAddon addon in Addons)
                if (!Reflect.IsNull(addon) && ReferenceEquals(addon.avatar, avatar))
                    return addon;
            return null;
        }

        /// <summary>avatar 가 지금 블루비틀을 들고 있는가</summary>
        public static bool IsWielding(object avatar)
        {
            if (Wielders.Count == 0)
                return false;
            Wielders.RemoveAll(Reflect.IsNull);
            return Wielders.Contains(avatar);
        }

        /// <summary>서버의 피해 처리에서 불립니다: attacker 의 무기 공격이 victim 에게 적중함.</summary>
        public static void OnWeaponHit(object attacker, object victim, object damage)
        {
            try
            {
                if (!IsWielding(attacker) || Equals(Reflect.Get(damage, "ignoreDebuff"), true) || Dice.Next(100) >= FrostbitePercent)
                    return;
                object frostbite = Reflect.Call(null, Reflect.GameType("UnitDatabase"), "GetDebuff", "FROSTBITE");
                if (Reflect.IsNull(frostbite))
                    return;
                MethodInfo apply = victim.GetType().GetMethods(Reflect.Any).First(m => m.Name == "ApplyDebuff" && m.GetParameters().Length == 2);
                apply.Invoke(victim, new[] { frostbite, attacker });
                if (!loggedFrostbite)
                {
                    loggedFrostbite = true;
                    Log.Info("블루비틀 동상 부여 확인");
                }
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("블루비틀 적중 처리 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
            }
        }
    }
}

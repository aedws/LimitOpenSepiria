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

        // 무기 종류. 공용 능력(공격 속도, 창격 3·4·5타, 패링·퓨리·돌파)은 모두 같고, 기수마다 고유 효과가 더해집니다.
        public const int Knight = 0;   // 만변창 : 4기사
        public const int Blue = 1;     // 서리기수 : 돌격창 블루비틀
        public const int Flame = 2;    // 화염기수
        public const int Thunder = 3;  // 뇌격기수
        public const int Doom = 4;     // 종말기수

        /// <summary>이 무기의 종류. 프리팹을 만들 때 정해집니다.</summary>
        public int kind;

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
                // 종류는 프리팹에 적어 두지만, 혹시 값이 넘어오지 않았을 때를 대비해 무기 ID 로도 확인합니다.
                object entityId = Reflect.Get(parent, "entityId");
                int byId = entityId is int ? LanceFactory.KindOfId((int)entityId) : -1;
                if (byId >= 0)
                    kind = byId;
                if (!Wielders.Contains(avatar))
                    Wielders.Add(avatar);
                Addons.RemoveAll(x => Reflect.IsNull(x) || ReferenceEquals(x, this));
                Addons.Add(this);
                if (Equals(Reflect.Get(this, "isServer"), true))
                {
                    statApplied = AddAttackSpeed(AttackSpeedBonus);
                    if (kind == Doom) // 종말기수: 치명타 확률 +20%(능력치는 0.01% 단위), 치명타 피해 +50%
                    {
                        doomApplied = AddStat("Critical", 2000) & AddStat("CriticalDamageBonus", 50);
                        AddCosmic(CosmicBase); // 우주 속성 피해
                    }
                    Riders.OnEquip(avatar, kind);
                }
                Log.Info("기사창 무기 효과 활성화 (종류 " + kind + ", 공격 속도 +" + AttackSpeedBonus + "% " + (statApplied ? "적용" : "미적용") + ")");
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
                Riders.OnUnequip(avatar);
                SetDashBonus(0);
                if (doomApplied)
                {
                    AddStat("Critical", -2000);
                    AddStat("CriticalDamageBonus", -50);
                    AddCosmic(-CosmicBase);
                }
                doomApplied = false;
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
        private bool doomApplied;
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
                SetDashBonus(kind == Blue ? Math.Max(0, ItemEffects.Stat(avatar, ItemContent.LanceDash)) : 0);
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

        /// <summary>종말기수가 주는 우주 속성 피해</summary>
        public const int CosmicBase = 10;

        private void AddCosmic(int value)
        {
            MethodInfo add = Reflect.IsNull(avatar) ? null : avatar.GetType().GetMethod("AddCustomStatUnsafe", new[] { typeof(string), typeof(int) });
            if (add != null)
                add.Invoke(avatar, new object[] { ItemContent.Cosmic, value });
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

        /// <summary>avatar 가 들고 있는 기사창의 종류(없으면 -1)</summary>
        public static int KindOf(object avatar)
        {
            LanceAddon addon = Addons.Count == 0 ? null : Find(avatar);
            return addon == null ? -1 : addon.kind;
        }

        /// <summary>avatar 가 지금 블루비틀을 들고 있는가</summary>
        public static bool IsBlue(object avatar)
        {
            return KindOf(avatar) == Blue;
        }

        /// <summary>avatar 가 지금 기사창(종류 무관)을 들고 있는가</summary>
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
                int wielded = KindOf(attacker);
                if (wielded == Thunder)
                    Riders.ThunderHit(attacker, victim);
                if (wielded != Blue || Equals(Reflect.Get(damage, "ignoreDebuff"), true) || Dice.Next(100) >= FrostbitePercent)
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

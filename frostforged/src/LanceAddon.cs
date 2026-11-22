using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리기수 : 돌격창 블루비틀'에 붙는 무기 효과.
    ///  - 공격 속도 +20%
    ///  - 무기 공격이 적중하면 20% 확률로 동상 부여
    ///  - 무기 공격이 적중하면(한 번 휘두를 때 한 번) 보유한 볼루스파가 약화된 형태로 발동
    /// 창격 3·4·5타 반복은 LanceMotion 이 이 컴포넌트가 붙은 무기에만 적용합니다.
    /// </summary>
    public class LanceAddon : WeaponAddon
    {
        public const int AttackSpeedBonus = 20;
        public const int FrostbitePercent = 20;

        private static readonly System.Random Dice = new System.Random();

        private object controller, avatar;
        private Delegate hitHandler;
        private Action<int> beginHandler, specialHandler;
        private bool statApplied, firedThisSwing = true, loggedFrostbite, loggedFailure;

        private bool IsServer { get { return Equals(Reflect.Get(this, "isServer"), true); } }

        protected override void OnEnableAddon()
        {
            base.OnEnableAddon();
            try
            {
                controller = Reflect.Get(parent, "Networkowner");
                avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar) || !IsServer)
                    return;

                statApplied = AddAttackSpeed(AttackSpeedBonus);

                EventInfo attack = avatar.GetType().GetEvent("OnAttackUnit", Reflect.Any);
                MethodInfo handler = typeof(LanceAddon).GetMethod(nameof(OnHit), Reflect.Any);
                if (attack != null)
                {
                    hitHandler = Delegate.CreateDelegate(attack.EventHandlerType, this, handler);
                    attack.AddEventHandler(avatar, hitHandler);
                }
                else
                {
                    Log.Warn("적중 지점을 찾지 못해 블루비틀의 동상·볼루스파 발동이 동작하지 않습니다.");
                }

                // 휘두를 때마다 '이번 휘두르기에서 볼루스파를 쐈는지'를 초기화합니다.
                beginHandler = _ => firedThisSwing = false;
                specialHandler = _ => firedThisSwing = false;
                Combine("OnBeginAttackAnimation", beginHandler, true);
                Combine("OnSpecialAttackSwing", specialHandler, true);
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
                if (statApplied)
                    AddAttackSpeed(-AttackSpeedBonus);
                statApplied = false;
                if (hitHandler != null && !Reflect.IsNull(avatar))
                    avatar.GetType().GetEvent("OnAttackUnit", Reflect.Any).RemoveEventHandler(avatar, hitHandler);
                hitHandler = null;
                if (beginHandler != null)
                    Combine("OnBeginAttackAnimation", beginHandler, false);
                if (specialHandler != null)
                    Combine("OnSpecialAttackSwing", specialHandler, false);
                beginHandler = specialHandler = null;
            }
            catch (Exception e)
            {
                Log.Warn("블루비틀 무기 효과 해제 중 오류: " + e);
            }
        }

        private void Combine(string field, Action<int> handler, bool add)
        {
            if (Reflect.IsNull(controller))
                return;
            var current = Reflect.Get(controller, field) as Action<int>;
            Reflect.Set(controller, field, add ? (Action<int>)Delegate.Combine(current, handler) : (Action<int>)Delegate.Remove(current, handler));
        }

        private bool AddAttackSpeed(int value)
        {
            Type stat = Reflect.GameType("ECustomStat");
            MethodInfo add = stat == null ? null : avatar.GetType().GetMethod("AddCustomStat", new[] { stat, typeof(int) });
            if (add == null || Reflect.IsNull(avatar))
                return false;
            add.Invoke(avatar, new[] { Enum.Parse(stat, "AttackSpeed"), value });
            return true;
        }

        // UnitAvatar.OnAttackUnit(맞은 대상, 피해). 서버에서만 등록됩니다.
        private void OnHit(object victim, object damage)
        {
            try
            {
                if (Reflect.IsNull(victim) || damage == null || Convert.ToString(Reflect.Get(damage, "fromType")) != "DirectAttack")
                    return;

                if (!Equals(Reflect.Get(damage, "ignoreDebuff"), true) && Dice.Next(100) < FrostbitePercent)
                {
                    object frostbite = Reflect.Call(null, Reflect.GameType("UnitDatabase"), "GetDebuff", "FROSTBITE");
                    if (!Reflect.IsNull(frostbite))
                    {
                        MethodInfo apply = victim.GetType().GetMethods(Reflect.Any).First(m => m.Name == "ApplyDebuff" && m.GetParameters().Length == 2);
                        apply.Invoke(victim, new[] { frostbite, avatar });
                        if (!loggedFrostbite)
                        {
                            loggedFrostbite = true;
                            Log.Info("블루비틀 동상 부여 확인");
                        }
                    }
                }

                if (!firedThisSwing)
                {
                    firedThisSwing = true;
                    WeakSpear.Fire(avatar);
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

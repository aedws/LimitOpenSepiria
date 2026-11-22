using System;
using System.Text;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리단조 칼날 : 초승달 식'에 붙는 무기 효과.
    /// 이 컴포넌트가 붙어 있는 무기를 들고 있는지로 검집 발동 규칙을 바꿀지 판단합니다(ScabbardPatch).
    /// 효과 설명 문구도 이 컴포넌트의 effectText 로 표시됩니다.
    /// </summary>
    public class CrescentAddon : WeaponAddon
    {
        /// <summary>납도 상태에서의 이동 속도 증가량(배율에 더해집니다). 0.6 = 60%.</summary>
        public const float SheathMoveSpeedBonus = 0.6f;

        private Action<int> specialHandler;
        private GameObject hip;
        private const string SheathSpeedStat = "SPEEDSHEATH";
        private bool sheathSpeedApplied;
        private object avatar;
        private bool speedApplied;
        private bool loggedSpeed;
        private Action<int> beginHandler;
        private object controller;
        private int logged;

        protected override void OnEnableAddon()
        {
            base.OnEnableAddon();
            try
            {
                Diagnostics.LogOnce(this);

                // 공격 애니메이션 구조를 확인하기 위한 진단: 처음 몇 번의 공격 시작을 기록합니다.
                controller = Reflect.Get(parent, "Networkowner");
                if (Reflect.IsNull(controller))
                    return;
                avatar = Reflect.Get(controller, "unitAvatar");
                hip = HipScabbard.Create(controller as Component, parent as Component);
                // 납도 공격 간격이 공격 속도를 따르게 합니다(게임의 '납도 공격 속도' 능력치를 켭니다).
                if (IsServer && !sheathSpeedApplied && !Reflect.IsNull(avatar))
                {
                    Reflect.Call(avatar, null, "AddCustomStatUnsafe", SheathSpeedStat, 1);
                    sheathSpeedApplied = true;
                }
                Reflect.Set(parent, "sheathActionType", 0); // 특수 공격을 납도로
                specialHandler = OnSpecialSwing;
                var special = Reflect.Get(controller, "OnSpecialAttackSwing") as Action<int>;
                Reflect.Set(controller, "OnSpecialAttackSwing", (Action<int>)Delegate.Combine(special, specialHandler));
                beginHandler = OnBeginAttack;
                var current = Reflect.Get(controller, "OnBeginAttackAnimation") as Action<int>;
                Reflect.Set(controller, "OnBeginAttackAnimation", (Action<int>)Delegate.Combine(current, beginHandler));
            }
            catch (Exception e)
            {
                Log.Warn("무기 효과 활성화 중 오류: " + e);
            }
        }

        protected override void OnDisableAddon()
        {
            base.OnDisableAddon();
            try
            {
                SetSpeedBonus(false);
                if (sheathSpeedApplied && !Reflect.IsNull(avatar))
                    Reflect.Call(avatar, null, "AddCustomStatUnsafe", SheathSpeedStat, -1);
                sheathSpeedApplied = false;
                HipScabbard.Destroy(hip);
                hip = null;
                if (specialHandler != null && !Reflect.IsNull(controller))
                {
                    var special = Reflect.Get(controller, "OnSpecialAttackSwing") as Action<int>;
                    Reflect.Set(controller, "OnSpecialAttackSwing", (Action<int>)Delegate.Remove(special, specialHandler));
                }
                if (beginHandler != null && !Reflect.IsNull(controller))
                {
                    var current = Reflect.Get(controller, "OnBeginAttackAnimation") as Action<int>;
                    Reflect.Set(controller, "OnBeginAttackAnimation", (Action<int>)Delegate.Remove(current, beginHandler));
                }
            }
            catch (Exception e)
            {
                Log.Warn("무기 효과 비활성화 중 오류: " + e);
            }
            beginHandler = null;
            specialHandler = null;
            controller = null;
            avatar = null;
        }

        private bool IsServer { get { return Equals(Reflect.Get(this, "isServer"), true); } }

        private bool IsSheathed
        {
            get { return Equals(Reflect.Get(parent, "sheathStateEnabled"), true) && Equals(Reflect.Get(parent, "isBladeSheathed"), true); }
        }

        private void Update()
        {
            if (Reflect.IsNull(parent) || Reflect.IsNull(controller))
                return;
            try
            {
                // 계승한 무기 효과가 특수 공격을 튕겨내기로 바꾸므로, 납도로 유지합니다.
                if (Convert.ToInt32(Reflect.Get(parent, "sheathActionType")) != 0)
                    Reflect.Set(parent, "sheathActionType", 0);
                // 허리의 칼집은 칼을 뽑아 든 동안만 보입니다(납도하면 칼 그림이 칼집째로 바뀝니다).
                if (!Reflect.IsNull(hip))
                {
                    bool show = !Equals(Reflect.Get(parent, "isBladeSheathed"), true);
                    if (hip.activeSelf != show)
                        hip.SetActive(show);
                }
                if (IsServer)
                    SetSpeedBonus(IsSheathed);
            }
            catch (Exception)
            {
            }
        }

        // 납도 상태인 동안만 이동 속도 배율을 올립니다. 올린 만큼만 되돌립니다.
        private void SetSpeedBonus(bool on)
        {
            if (on == speedApplied || Reflect.IsNull(avatar))
                return;
            float current = Convert.ToSingle(Reflect.Get(avatar, "localMoveSpeedMultiplier"));
            if (!Reflect.Set(avatar, "NetworklocalMoveSpeedMultiplier", current + (on ? SheathMoveSpeedBonus : -SheathMoveSpeedBonus)))
                return;
            speedApplied = on;
            if (on && !loggedSpeed)
            {
                loggedSpeed = true;
                Log.Info("납도 이동 속도 증가 적용 (" + current.ToString("0.##") + " → " + (current + SheathMoveSpeedBonus).ToString("0.##") + ")");
            }
        }

        // 납도 상태에서 나간 공격(게임에서는 특수 공격으로 처리됩니다).
        private void OnSpecialSwing(int index)
        {
            if (!IsServer || !Equals(Reflect.Get(parent, "sheathStateEnabled"), true))
                return;
            CharmLink.OnSheathAttack(avatar);
        }

        private void OnBeginAttack(int fireId)
        {
            if (logged >= 12)
                return;
            logged++;
            try
            {
                var text = new StringBuilder("공격 시작 fireId=" + fireId);
                text.Append(" attackMoveSet=").Append(Reflect.Get(parent, "attackMoveSet"));
                text.Append(" finalComboIdx=").Append(Reflect.Get(parent, "finalComboIdx"));
                object driver = Reflect.Get(controller, "AnimationDriver");
                text.Append(" driver=").Append(driver == null ? "(없음)" : driver.GetType().Name);
                object animator = Reflect.Get(controller, "animator");
                if (!Reflect.IsNull(animator))
                    text.Append(' ').Append(Diagnostics.DescribeAnimator(animator));
                Log.Info(text.ToString());
            }
            catch (Exception e)
            {
                Log.Warn("공격 진단 중 오류: " + e.Message);
            }
        }
    }
}

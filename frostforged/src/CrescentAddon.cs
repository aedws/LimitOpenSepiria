using System;
using System.Text;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리단조 칼날 : 초승달 식'에 붙는 무기 효과.
    /// 이 컴포넌트가 붙어 있는 무기를 들고 있는지로 검집 발동 규칙을 바꿀지 판단합니다(ScabbardPatch).
    /// 효과 설명 문구도 이 컴포넌트의 effectText 로 표시됩니다.
    /// </summary>
    public class CrescentAddon : WeaponAddon
    {
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
            controller = null;
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

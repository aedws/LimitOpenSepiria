using System;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 태극 발동형 아티팩트(참월 흑·백, 섬멸 청, 명멸 적)의 아티팩트 컴포넌트.
    /// 동작은 게임의 능력치 아티팩트(Charm_StatusInstance) 그대로이고, 게임이 '발동형 아티팩트'를 가려내는 표식
    /// (IAttackableCharm)만 더했습니다. 이 표식이 있어야 나침반이 이 아티팩트에 연결되고 피해 증폭을 줍니다.
    /// 실제 발동과 피해 계산은 Cosmic 이 하며, 나침반의 피해 증폭도 거기서 읽어 곱합니다.
    /// </summary>
    public class TaegeukCharm : Charm_StatusInstance, IAttackableCharm
    {
        public bool IsAttackableCharm()
        {
            return true;
        }

        /// <summary>나침반 등이 보는 이 아티팩트의 기본 피해량(나침반 증폭 전)</summary>
        public float GetDamage(UnitAvatar avatar)
        {
            try
            {
                return Cosmic.BaseAmount(avatar, Cosmic.KindOfCharm(this));
            }
            catch (Exception)
            {
                return 0f;
            }
        }
    }
}

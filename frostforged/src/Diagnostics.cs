using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>게임 데이터가 예상과 같은지 확인하기 위한 로그. 다음 버전의 구현 근거로 씁니다.</summary>
    internal static class Diagnostics
    {
        public const int ScabbardItemId = 1014; // 축사의 검집 (Scabbard of Exorcism)

        private static bool logged;

        public static void LogOnce(CrescentAddon addon)
        {
            if (logged)
                return;
            logged = true;

            try
            {
                object entity = Reflect.Call(null, Reflect.GameType("ItemDatabase"), "FindItemById", ScabbardItemId);
                var prefab = Reflect.Get(entity, "resourcePrefab") as GameObject;
                if (Reflect.IsNull(prefab))
                {
                    Log.Warn("아이템 " + ScabbardItemId + " 의 프리팹을 찾지 못했습니다.");
                }
                else
                {
                    Log.Info("아이템 " + ScabbardItemId + " 프리팹 '" + prefab.name + "' 구성: " + ComponentNames(prefab));
                }
            }
            catch (Exception e)
            {
                Log.Warn("검집 아이템 확인 중 오류: " + e.Message);
            }

            try
            {
                var weapon = addon.parent as Component;
                Log.Info("무기 '" + weapon.gameObject.name + "' 구성: " + ComponentNames(weapon.gameObject));
                var combos = Reflect.Get(weapon, "basicComboAttacks") as IEnumerable;
                if (combos != null)
                    Log.Info("기본 공격 콤보: " + string.Join(", ", combos.Cast<object>().Select(c => Reflect.IsNull(c) ? "null" : ((UnityEngine.Object)c).name).ToArray()));
            }
            catch (Exception e)
            {
                Log.Warn("무기 확인 중 오류: " + e.Message);
            }
        }

        public static string ComponentNames(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponent(typeof(Transform)).GetComponents(typeof(Component));
            return string.Join(", ", components.Where(c => !Reflect.IsNull(c)).Select(c => c.GetType().Name).ToArray());
        }

        // Animator 는 UnityEngine.AnimationModule 에 있어 이름으로 접근합니다.
        public static string DescribeAnimator(object animator)
        {
            var text = new StringBuilder();
            object state = Reflect.Call(animator, null, "GetCurrentAnimatorStateInfo", 0);
            text.Append("state=").Append(Reflect.Get(state, "shortNameHash"))
                .Append(" t=").Append(((float)Reflect.Get(state, "normalizedTime")).ToString("0.00"));
            text.Append(" clip=").Append(ClipNames(Reflect.Call(animator, null, "GetCurrentAnimatorClipInfo", 0)));
            if ((bool)Reflect.Call(animator, null, "IsInTransition", 0))
            {
                object next = Reflect.Call(animator, null, "GetNextAnimatorStateInfo", 0);
                text.Append(" next=").Append(Reflect.Get(next, "shortNameHash"))
                    .Append(" nextClip=").Append(ClipNames(Reflect.Call(animator, null, "GetNextAnimatorClipInfo", 0)));
            }
            return text.ToString();
        }

        private static string ClipNames(object clipInfos)
        {
            var list = clipInfos as IEnumerable;
            if (list == null)
                return "?";
            return string.Join("+", list.Cast<object>().Select(info =>
            {
                var clip = Reflect.Get(info, "clip") as UnityEngine.Object;
                return Reflect.IsNull(clip) ? "?" : clip.name;
            }).ToArray());
        }
    }
}

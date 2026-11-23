using System;
using System.Collections;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 납도 상태일 때 캐릭터 테두리를 하늘색으로 빛나게 합니다.
    /// 게임이 '슈퍼 아머' 상태에서 캐릭터 테두리를 표시할 때 쓰는 효과(캐릭터 그림을 따라가는 테두리 그림)를
    /// 같은 방법으로 하나 더 붙이고, 색만 하늘색으로 바꿉니다.
    /// </summary>
    internal sealed class SheathAura
    {
        private readonly Color tint;

        private readonly object avatar;
        private GameObject instance;
        private Component renderer;
        private IList followers;   // 캐릭터 애니메이터가 그림을 함께 바꿔 주는 렌더러 목록
        private object bodyRenderer;
        private bool failed;

        public SheathAura(object avatar, Color tint)
        {
            this.avatar = avatar;
            this.tint = tint;
        }

        public void Set(bool on)
        {
            if (failed)
                return;
            try
            {
                if (on && Reflect.IsNull(instance))
                    Create();
                else if (!on && !Reflect.IsNull(instance))
                    Destroy();

                if (on && !Reflect.IsNull(renderer))
                {
                    // 피격 번쩍임 등으로 색이 덮여도 다시 하늘색으로.
                    Reflect.Set(renderer, "color", tint);
                    if (followers == null && !Reflect.IsNull(bodyRenderer))
                        Reflect.Set(renderer, "sprite", Reflect.Get(bodyRenderer, "sprite"));
                }
            }
            catch (Exception e)
            {
                failed = true;
                Log.Warn("납도 테두리 효과를 표시하지 못했습니다: " + e.Message);
            }
        }

        private void Create()
        {
            object combat = Reflect.GetStatic(Reflect.GameType("CombatManager"), "Instance");
            var prefab = Reflect.Get(combat, "superArmorFxPrefab") as UnityEngine.Object;
            object actor = Reflect.Get(avatar, "TopdownActor");
            var body = Reflect.Get(actor, "body") as Transform;
            Type rendererType = typeof(Component).Assembly.GetType("UnityEngine.SpriteRenderer");
            if (Reflect.IsNull(prefab) || Reflect.IsNull(body) || rendererType == null)
            {
                failed = true;
                Log.Warn("납도 테두리 효과에 쓸 게임 효과를 찾지 못했습니다.");
                return;
            }

            instance = (GameObject)UnityEngine.Object.Instantiate(prefab, body);
            instance.name = "FrostforgedCrescent_SheathAura";
            instance.transform.localPosition = new Vector3(0f, 0f, 0f);
            renderer = instance.GetComponent(rendererType);
            if (Reflect.IsNull(renderer))
            {
                Destroy();
                failed = true;
                return;
            }

            followers = Reflect.Get(Reflect.Get(actor, "animator"), "spriteRenderers") as IList;
            bodyRenderer = Reflect.Get(actor, "bodyRenderer");
            if (followers != null)
                followers.Add(renderer);
            Log.Info("납도 테두리 효과 표시");
        }

        public void Destroy()
        {
            try
            {
                if (followers != null && !Reflect.IsNull(renderer))
                    followers.Remove(renderer);
            }
            catch (Exception)
            {
            }
            if (!Reflect.IsNull(instance))
                UnityEngine.Object.Destroy(instance);
            instance = null;
            renderer = null;
            followers = null;
        }
    }
}

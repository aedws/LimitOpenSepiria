using System;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 이 모드가 직접 그린 스프라이트 애니메이션을 재생하는 작은 재생기.
    /// 프레임을 순서대로 보여 주고, 끝나면(또는 수명이 다하면) 스스로 사라집니다. 일정한 속도로 날아가게 할 수도 있습니다.
    /// 게임의 이펙트 풀을 쓰지 않으므로 다른 이펙트에 영향을 주지 않습니다.
    /// </summary>
    public class FxPlayer : MonoBehaviour
    {
        public Sprite[] frames;
        public float framesPerSecond = 16f;
        public bool loop;
        public float life = -1f;      // 0 이상이면 이 시간 뒤에 사라집니다
        public float velocityX, velocityY;
        public SpriteRenderer target;

        private float clock;
        private int shown = -1;

        private void Update()
        {
            try
            {
                float delta = Time.deltaTime;
                clock += delta;
                if (velocityX != 0f || velocityY != 0f)
                {
                    Vector3 p = transform.position;
                    transform.position = new Vector3(p.x + velocityX * delta, p.y + velocityY * delta, p.z);
                }
                int count = frames == null ? 0 : frames.Length;
                if (count > 0 && target != null)
                {
                    int index = (int)(clock * framesPerSecond);
                    if (index >= count)
                    {
                        if (!loop)
                        {
                            Destroy(gameObject);
                            return;
                        }
                        index %= count;
                    }
                    if (index != shown)
                    {
                        shown = index;
                        target.sprite = frames[index];
                    }
                }
                if (life >= 0f && clock >= life)
                    Destroy(gameObject);
            }
            catch (Exception)
            {
                Destroy(gameObject);
            }
        }
    }
}

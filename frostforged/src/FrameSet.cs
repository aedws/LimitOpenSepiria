using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 손에 든 칼의 전용 그림.
    ///
    /// 카타나의 모습은 AnimationSet(상태별 그림 묶음)을 Animator2D 가 재생합니다. 상태는 두 가지를 씁니다.
    ///  - IDLE   : 칼을 뽑아 든 모습          → Sprites/katana.png
    ///  - SHEATH : 납도 상태(칼집에 넣은 모습) → Sprites/katana_sheathed.png
    /// 게임이 납도 여부에 따라 IDLE ↔ SHEATH 를 오가므로(WeaponSimple_Katana.Start 의 전환 규칙),
    /// 이 무기 전용 묶음에 두 상태를 넣어 주면 납도할 때 칼집 모습으로 바뀝니다.
    /// 그림의 크기 기준과 손에 쥐는 위치(기준점)는 진화 전 무기의 그림에서 가져옵니다.
    /// </summary>
    internal static class FrameSet
    {
        public static void Apply(Component weapon, string packageRoot)
        {
            object animator = Reflect.Get(weapon, "katanaAnimator");
            var original = Reflect.IsNull(animator) ? null : Reflect.Get(animator, "currentSet") as UnityEngine.Object;
            if (Reflect.IsNull(original))
            {
                Log.Info("전용 그림: 이 무기에는 칼 애니메이션 묶음이 없어 원본 그림을 씁니다.");
                return;
            }

            UnityEngine.Object set = UnityEngine.Object.Instantiate(original);
            set.name = original.name + "_FrostforgedCrescent";
            Reflect.Set(set, "hideFlags", 61);
            var states = Reflect.Get(set, "sprites") as IList;
            if (states == null)
                return;

            // 기준이 될 원본 그림과, 이미 있는 상태들.
            object reference = null, idle = null, sheath = null;
            foreach (object state in states)
            {
                string name = Convert.ToString(Reflect.Get(state, "state")).ToUpperInvariant();
                if (name == "IDLE") idle = state;
                if (name == "SHEATH") sheath = state;
                if (reference == null)
                    reference = FirstSprite(state);
            }
            if (idle != null && FirstSprite(idle) != null)
                reference = FirstSprite(idle);

            object drawn = SpriteLoader.Load(Path.Combine(packageRoot, "Sprites", "katana.png"), reference, "칼 그림");
            object sheathed = SpriteLoader.Load(Path.Combine(packageRoot, "Sprites", "katana_sheathed.png"), reference, "납도 그림");
            if (drawn == null)
                return;

            foreach (object state in states)
                if (!ReferenceEquals(state, sheath))
                    SetSprites(state, drawn);

            if (sheathed != null)
            {
                if (sheath != null)
                {
                    SetSprites(sheath, sheathed);
                }
                else
                {
                    object added = NewState("SHEATH", sheathed);
                    if (added != null)
                        states.Add(added);
                }
            }

            Reflect.Set(animator, "currentSet", set);
            Log.Info("전용 그림 적용: 상태 " + states.Count + "개 (납도 그림 " + (sheathed != null ? "있음" : "없음") + ")");
        }

        private static object FirstSprite(object state)
        {
            var timeline = Reflect.Get(state, "timeline") as IEnumerable;
            if (timeline == null)
                return null;
            foreach (object frame in timeline)
            {
                object sprite = Reflect.Get(frame, "sprite");
                if (!Reflect.IsNull(sprite))
                    return sprite;
            }
            return null;
        }

        private static void SetSprites(object state, object sprite)
        {
            var timeline = Reflect.Get(state, "timeline") as IList;
            if (timeline == null)
                return;
            if (timeline.Count == 0)
            {
                object frame = NewFrame(sprite);
                if (frame != null)
                    timeline.Add(frame);
                return;
            }
            foreach (object frame in timeline)
                Reflect.Set(frame, "sprite", sprite);
        }

        private static object NewFrame(object sprite)
        {
            Type type = Reflect.GameType("AnimationSet+StateInfo+SpriteKeyFrame");
            if (type == null)
                return null;
            object frame = Activator.CreateInstance(type);
            Reflect.Set(frame, "frameIdx", 0);
            Reflect.Set(frame, "sprite", sprite);
            return frame;
        }

        private static object NewState(string name, object sprite)
        {
            Type type = Reflect.GameType("AnimationSet+StateInfo");
            if (type == null)
                return null;
            object state = Activator.CreateInstance(type);
            Reflect.Set(state, "state", name);
            Reflect.Set(state, "repeat", true);
            SetSprites(state, sprite);
            return state;
        }
    }
}

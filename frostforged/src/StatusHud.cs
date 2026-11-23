using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 이 모드 전용 상태 표시(화면 왼쪽 위의 숫자 달린 아이콘).
    /// 숫자를 띄울 수 있는 게임의 상태 표시를 틀로 복제해 이름·설명·아이콘만 바꾸고, 게임의 상태 표시 목록에 넣습니다.
    /// 목록은 월드에 들어갈 때마다 새로 만들어지므로 쓸 때마다 Ensure 로 확인합니다.
    /// </summary>
    internal static class StatusHud
    {
        private const string Template = "CHARGEDSWEEP";

        private sealed class Slot { public GameObject Prefab; public UnityEngine.Object Entity; }

        private static readonly Dictionary<string, Slot> Entries = new Dictionary<string, Slot>();
        private static GameObject holder;
        private static bool loggedLayout, loggedFailure;

        /// <summary>상태 표시 id 를 돌려줍니다. 전용 표시를 만들지 못하면 틀의 id 를 돌려줍니다.</summary>
        public static string Ensure(string id, string nameKey, string flavorKey, string iconFile)
        {
            try
            {
                Type database = Reflect.GameType("EffectHUDDatabase");
                var all = Reflect.GetStatic(database, "effectHUDDictionary") as IDictionary;
                if (all == null)
                    return Template;
                if (all.Contains(id) && !Reflect.IsNull(all[id]))
                    return id;
                Slot entry;
                if (!Entries.TryGetValue(id, out entry) || Reflect.IsNull(entry.Entity) || Reflect.IsNull(entry.Prefab))
                {
                    object template = all.Contains(Template) ? all[Template] : null;
                    var source = Reflect.IsNull(template) ? null : Reflect.Get(template, "hudPrefab") as GameObject;
                    if (Reflect.IsNull(source))
                        return Template;
                    if (Reflect.IsNull(holder))
                    {
                        holder = new GameObject("FrostforgedCrescent_Hud");
                        holder.SetActive(false);
                        Reflect.Set(holder, "hideFlags", 61);
                        UnityEngine.Object.DontDestroyOnLoad(holder);
                    }
                    entry = new Slot();
                    entry.Prefab = (GameObject)UnityEngine.Object.Instantiate(source, holder.transform);
                    entry.Prefab.name = "EffectHUD_" + id;
                    Component hud = entry.Prefab.GetComponent(Reflect.GameType("UI_EffectHUD_Basic"));
                    Type localized = Reflect.GameType("LocalizedString");
                    Reflect.Set(hud, "effectName", Activator.CreateInstance(localized, nameKey));
                    Reflect.Set(hud, "effectFlavorText", Activator.CreateInstance(localized, flavorKey));
                    PaintIcon(entry.Prefab, hud, iconFile);

                    entry.Entity = ScriptableObject.CreateInstance(Reflect.GameType("EffectHUDEntity"));
                    entry.Entity.name = id;
                    Reflect.Set(entry.Entity, "hideFlags", 61);
                    Reflect.Set(entry.Entity, "id", id);
                    Reflect.Set(entry.Entity, "hudPrefab", entry.Prefab);
                    Entries[id] = entry;
                }
                all[id] = entry.Entity;
                return id;
            }
            catch (Exception e)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("상태 표시 준비 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
                }
                return Template;
            }
        }

        // 복제한 상태 표시의 아이콘 그림을 바꿉니다(덧씌우는 흰 번쩍임과 채움 그림은 그대로).
        private static void PaintIcon(GameObject prefab, Component hud, string iconFile)
        {
            object icon = SpriteLoader.Load(System.IO.Path.Combine((Entry.packageRoot ?? ""), "Sprites", iconFile), null, "상태 아이콘");
            Type image = Reflect.UnityType("UnityEngine.UI.Image", "UnityEngine.UI");
            MethodInfo find = typeof(GameObject).GetMethod("GetComponentsInChildren", new[] { typeof(Type), typeof(bool) });
            if (icon == null || image == null || find == null)
                return;
            object overlay = Reflect.Get(hud, "whiteOverlayImage"), fill = Reflect.Get(hud, "fillImage");
            var images = ((IEnumerable)find.Invoke(prefab, new object[] { image, true })).Cast<object>().ToList();
            object target = images.FirstOrDefault(i => !ReferenceEquals(i, overlay) && !ReferenceEquals(i, fill) && !Reflect.IsNull(Reflect.Get(i, "sprite")));
            if (!loggedLayout)
            {
                loggedLayout = true;
                Log.Info("상태 표시 구성: " + string.Join(", ", images.Select(i => ((UnityEngine.Object)i).name + "="
                         + (Reflect.IsNull(Reflect.Get(i, "sprite")) ? "없음" : ((UnityEngine.Object)Reflect.Get(i, "sprite")).name)
                         + (ReferenceEquals(i, target) ? "(교체)" : "")).ToArray()));
            }
            if (target != null)
                Reflect.Set(target, "sprite", icon);
        }
    }
}

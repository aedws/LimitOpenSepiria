using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리단조 칼날 : 초승달 식' 무기를 만들어 등록합니다.
    ///
    /// '겨울 대장장이의 습작'의 무기 프리팹을 복제하고, 무기 효과(CrescentAddon)를 붙인 뒤
    /// 그 무기의 다음 단계 강화 무기로 등록합니다.
    /// 복제한 프리팹은 게임의 RegisterNetworkPrefab 으로 등록해 멀티플레이에서도 생성됩니다.
    /// </summary>
    internal static class WeaponFactory
    {
        public const int WeaponId = 9101401;
        public const string NameKey = "Weapon_FrostforgedCrescent_Name";
        public const string EffectKey = "WeaponAddon_FrostforgedCrescent_Effect";
        private const string NetworkKey = Entry.ModId + ":weapon-crescent";

        private static GameObject holder;
        private static GameObject prefab;
        private static UnityEngine.Object entity;

        /// <summary>HorayModAPI.OnLoadWeaponDatabase 에서 불립니다(월드에 들어갈 때마다).</summary>
        public static void Register(string packageRoot)
        {
            try
            {
                Type database = Reflect.GameType("WeaponDatabase");
                object template = FindTemplate(database);
                if (template == null)
                {
                    Log.Error("카타나를 찾지 못해 무기를 등록하지 못했습니다.");
                    return;
                }

                if (Reflect.IsNull(entity) || Reflect.IsNull(prefab))
                    Build(template, packageRoot);
                if (Reflect.IsNull(entity))
                    return;

                Reflect.Call(null, database, "Register", entity);
            }
            catch (Exception e)
            {
                Log.Error("무기 등록 중 오류: " + e);
            }
        }

        private const string ParentPrefabName = "Weapon_Katana_Ice"; // 겨울 대장장이의 습작
        private static readonly string[] ParentNames = { "겨울 대장장이의 습작", "Winter Smith" };

        // 진화 전 무기: 겨울 대장장이의 습작. 찾지 못하면 기본 카타나로 대신합니다.
        private static object FindTemplate(Type database)
        {
            var all = Reflect.Call(null, database, "GetAll") as IEnumerable;
            if (all == null)
                return null;

            object byName = null, basic = null, anyKatana = null;
            foreach (object weapon in all)
            {
                var main = Reflect.Get(weapon, "mainWeaponPrefab") as GameObject;
                if (Reflect.IsNull(main))
                    continue;
                Component simple = main.GetComponent(typeof(WeaponSimple));
                if (Reflect.IsNull(simple) || Convert.ToString(Reflect.Get(simple, "weaponType")) != "Katana")
                    continue;
                string name = DisplayName(weapon);
                if (ParentNames.Any(n => name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    return weapon;
                if (byName == null && main.name == ParentPrefabName)
                    byName = weapon; // 이름으로 못 찾을 때(다른 언어 등)는 프리팹 이름으로
                if (basic == null && (bool)Reflect.Get(weapon, "isDefaultWeapon"))
                    basic = weapon;
                if (anyKatana == null)
                    anyKatana = weapon;
            }
            if (byName != null)
                return byName;
            Log.Warn("'겨울 대장장이의 습작'을 찾지 못해 기본 카타나의 강화 무기로 등록합니다.");
            return basic ?? anyKatana;
        }

        private static string DisplayName(object weapon)
        {
            try
            {
                return Convert.ToString(Reflect.Get(weapon, "aName")) ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static void Build(object template, string packageRoot)
        {
            var templatePrefab = (GameObject)Reflect.Get(template, "mainWeaponPrefab");
            int templateId = (int)Reflect.Get(template, "id");
            Log.Info("진화 전 무기 #" + templateId + " '" + ((UnityEngine.Object)template).name + "' (" + DisplayName(template) + "), 프리팹 '" + templatePrefab.name
                     + "', 무기 효과 " + ((Reflect.Get(templatePrefab.GetComponent(typeof(WeaponSimple)), "addons") as IEnumerable ?? new object[0]).Cast<object>().Count()) + "개 계승");

            // 비활성 부모 아래에 복제해 둡니다. 프리팹의 Awake 는 돌지 않고, 게임이 복제해 쓸 때 활성으로 시작합니다.
            if (Reflect.IsNull(holder))
            {
                holder = new GameObject("FrostforgedCrescent_Prefabs");
                holder.SetActive(false);
                Reflect.Set(holder, "hideFlags", 61); // HideAndDontSave: 씬이 바뀌어도 지워지지 않게
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }

            prefab = (GameObject)UnityEngine.Object.Instantiate(templatePrefab, holder.transform);
            prefab.name = "Weapon-FrostforgedCrescent";

            Component simple = prefab.GetComponent(typeof(WeaponSimple));
            if (Reflect.IsNull(simple))
            {
                Log.Error("복제한 프리팹에 WeaponSimple 이 없습니다.");
                prefab = null;
                return;
            }

            // 진화 전 무기의 효과는 그대로 계승하고, 그 뒤에 이 무기의 효과를 붙입니다.
            var addon = (CrescentAddon)prefab.AddComponent(typeof(CrescentAddon));
            addon.parent = (WeaponSimple)simple;
            addon.enabled = true;
            Reflect.Set(addon, "effectText", NewLocalizedString(EffectKey));

            var existing = (Reflect.Get(simple, "addons") as IEnumerable ?? new object[0]).Cast<object>().ToList();
            Array addons = Array.CreateInstance(typeof(WeaponAddon), existing.Count + 1);
            for (int i = 0; i < existing.Count; i++)
                addons.SetValue(existing[i], i);
            addons.SetValue(addon, existing.Count);
            Reflect.Set(simple, "addons", addons);

            try
            {
                FrameRecolor.Apply(simple, packageRoot);
            }
            catch (Exception e)
            {
                Log.Warn("전용 프레임을 만들지 못해 원본 그림을 씁니다: " + e);
            }

            if (!HorayModAPI.RegisterNetworkPrefab(prefab, NetworkKey))
                Log.Warn("네트워크 프리팹 등록에 실패했습니다. 멀티플레이에서 무기가 보이지 않을 수 있습니다.");

            // 무기 데이터: 진화 전 무기의 강화 무기로 등록합니다.
            Type entityType = Reflect.GameType("WeaponEntity");
            UnityEngine.Object created = ScriptableObject.CreateInstance(entityType);
            created.name = "FrostforgedCrescent";
            Reflect.Set(created, "hideFlags", 61);
            Reflect.Set(created, "id", WeaponId);
            Reflect.Set(created, "enabled", true);
            Reflect.Set(created, "activeState", Reflect.Get(template, "activeState"));
            Reflect.Set(created, "aName", NewLocalizedString(NameKey));
            object templateIcon = Reflect.Get(template, "icon");
            object templateBigIcon = Reflect.Get(template, "bigIcon");
            Reflect.Set(created, "icon", SpriteLoader.Load(System.IO.Path.Combine(packageRoot, "Sprites", "icon.png"), templateIcon, "아이콘") ?? templateIcon);
            Reflect.Set(created, "bigIcon", templateBigIcon); // 진화 전 무기와 같이 둡니다(없으면 아이콘이 대신 쓰입니다)
            Reflect.Set(created, "mainWeaponPrefab", prefab);
            Reflect.Set(created, "subWeaponPrefab", Reflect.Get(template, "subWeaponPrefab"));
            Reflect.Set(created, "standardEnhancements", Activator.CreateInstance(typeof(List<>).MakeGenericType(Reflect.GameType("EnhancementMetadata"))));
            Reflect.Set(created, "isDefaultWeapon", false);
            Reflect.Set(created, "enhanceFromId", templateId);
            entity = created;

            Log.Info("무기 #" + WeaponId + " 준비됨 (#" + templateId + " 의 강화 무기)");
        }

        private static object NewLocalizedString(string key)
        {
            return Activator.CreateInstance(Reflect.GameType("LocalizedString"), key);
        }
    }
}

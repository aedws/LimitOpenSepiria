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
    /// 게임의 기본 카타나를 템플릿으로 삼아 무기 프리팹을 복제하고, 무기 효과(CrescentAddon)를 붙인 뒤
    /// '기본 카타나의 강화 무기'로 등록합니다(무녀의 예언 같은 강화 무기와 같은 자리).
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
                    Log.Error("기본 카타나를 찾지 못해 무기를 등록하지 못했습니다.");
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

        // 기본 무기 중 카타나 종류인 것.
        private static object FindTemplate(Type database)
        {
            var all = Reflect.Call(null, database, "GetAll") as IEnumerable;
            if (all == null)
                return null;

            object fallback = null;
            foreach (object weapon in all)
            {
                var main = Reflect.Get(weapon, "mainWeaponPrefab") as GameObject;
                if (Reflect.IsNull(main))
                    continue;
                Component simple = main.GetComponent(typeof(WeaponSimple));
                if (Reflect.IsNull(simple) || Convert.ToString(Reflect.Get(simple, "weaponType")) != "Katana")
                    continue;
                if ((bool)Reflect.Get(weapon, "isDefaultWeapon"))
                    return weapon;
                if (fallback == null)
                    fallback = weapon;
            }
            return fallback;
        }

        private static void Build(object template, string packageRoot)
        {
            var templatePrefab = (GameObject)Reflect.Get(template, "mainWeaponPrefab");
            int templateId = (int)Reflect.Get(template, "id");
            Log.Info("템플릿 무기 #" + templateId + " '" + ((UnityEngine.Object)template).name + "', 프리팹 '" + templatePrefab.name + "'");

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

            // 무기 효과를 맨 앞에 등록합니다. 템플릿(기본 카타나)의 효과는 그대로 둡니다.
            var addon = (CrescentAddon)prefab.AddComponent(typeof(CrescentAddon));
            addon.parent = (WeaponSimple)simple;
            addon.enabled = true;
            Reflect.Set(addon, "effectText", NewLocalizedString(EffectKey));

            var existing = (Reflect.Get(simple, "addons") as IEnumerable ?? new object[0]).Cast<object>().ToList();
            Array addons = Array.CreateInstance(typeof(WeaponAddon), existing.Count + 1);
            addons.SetValue(addon, 0);
            for (int i = 0; i < existing.Count; i++)
                addons.SetValue(existing[i], i + 1);
            Reflect.Set(simple, "addons", addons);

            ApplyWeaponTint(simple);

            if (!HorayModAPI.RegisterNetworkPrefab(prefab, NetworkKey))
                Log.Warn("네트워크 프리팹 등록에 실패했습니다. 멀티플레이에서 무기가 보이지 않을 수 있습니다.");

            // 무기 데이터: 템플릿의 강화 무기로 등록합니다.
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
            Reflect.Set(created, "bigIcon", templateBigIcon); // 기본 카타나와 같이 둡니다(없으면 아이콘이 대신 쓰입니다)
            Reflect.Set(created, "mainWeaponPrefab", prefab);
            Reflect.Set(created, "subWeaponPrefab", Reflect.Get(template, "subWeaponPrefab"));
            Reflect.Set(created, "standardEnhancements", Activator.CreateInstance(typeof(List<>).MakeGenericType(Reflect.GameType("EnhancementMetadata"))));
            Reflect.Set(created, "isDefaultWeapon", false);
            Reflect.Set(created, "enhanceFromId", templateId);
            entity = created;

            Log.Info("무기 #" + WeaponId + " 준비됨 (템플릿 #" + templateId + " 의 강화 무기)");
        }

        // 손에 든 칼. 카타나의 모습은 여러 장의 애니메이션 그림으로 이루어져 있어 한 장으로 바꿀 수 없습니다.
        // 대신 본체 그림에 서리빛 색조를 입힙니다.
        private static void ApplyWeaponTint(Component simple)
        {
            object body = Reflect.Get(simple, "mainWeaponBody");
            object renderer = Reflect.Get(body, "weaponSpriteRenderer");
            if (Reflect.IsNull(renderer))
            {
                Log.Info("무기 색조: 이 무기에는 본체 그림 칸이 없습니다.");
                return;
            }
            Reflect.Set(renderer, "color", new Color(0.62f, 0.88f, 1f, 1f));
            Log.Info("무기 색조 적용");
        }

        private static object NewLocalizedString(string key)
        {
            return Activator.CreateInstance(Reflect.GameType("LocalizedString"), key);
        }
    }
}

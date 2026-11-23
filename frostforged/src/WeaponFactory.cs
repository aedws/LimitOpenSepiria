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
        public const int WeaponId = 9101401; // 서리단조 칼날 : 초승달 식
        public const string NameKey = "Weapon_FrostforgedCrescent_Name";
        public const string EffectKey = "WeaponAddon_FrostforgedCrescent_Effect";

        // 만드는 카타나들. Prefix 는 그림 파일 이름의 앞머리입니다(<Prefix>.png, <Prefix>_sheathed.png, 아이콘, 칼집).
        private sealed class Spec
        {
            public int Kind, Id;
            public string Name, NameKey, EffectKey, NetworkKey, Drawn, Sheathed, Icon, Scabbard;
            public GameObject Prefab;
            public UnityEngine.Object Entity;
        }

        private static readonly Spec[] Specs =
        {
            // 2단계: 만변도 : 초승 — 기본 카타나를 바탕으로, 카타나 공용 능력만 가집니다.
            new Spec { Kind = CrescentAddon.Base, Id = 9101407, Name = "KatanaBase", NameKey = "Weapon_FFC_KatanaBase_Name", EffectKey = "WeaponAddon_FFC_KatanaBase_Effect",
                       NetworkKey = Entry.ModId + ":weapon-katanabase", Drawn = "katana_base.png", Sheathed = "katana_base_sheathed.png", Icon = "katana_base.png", Scabbard = "scabbard_base.png" },
            // 3단계: 서리단조 칼날 : 초승달 식 — 겨울 대장장이의 습작을 바탕으로 합니다(그 무기의 효과도 계승).
            new Spec { Kind = CrescentAddon.Frost, Id = WeaponId, Name = "FrostforgedCrescent", NameKey = NameKey, EffectKey = EffectKey,
                       NetworkKey = Entry.ModId + ":weapon-crescent", Drawn = "katana.png", Sheathed = "katana_sheathed.png", Icon = "icon.png", Scabbard = "scabbard.png" },
            // 3단계: 흑요도 : 참월 II형 — 기본 카타나를 바탕으로 합니다.
            new Spec { Kind = CrescentAddon.Obsidian, Id = 9101408, Name = "Obsidian", NameKey = "Weapon_FFC_Obsidian_Name", EffectKey = "WeaponAddon_FFC_Obsidian_Effect",
                       NetworkKey = Entry.ModId + ":weapon-obsidian", Drawn = "katana_obsidian.png", Sheathed = "katana_obsidian_sheathed.png", Icon = "katana_obsidian.png", Scabbard = "scabbard_obsidian.png" },
        };

        private static GameObject holder;

        /// <summary>무기 ID 로 카타나 종류를 찾습니다(이 모드의 카타나가 아니면 -1).</summary>
        public static int KindOfId(int weaponId)
        {
            foreach (Spec spec in Specs)
                if (spec.Id == weaponId)
                    return spec.Kind;
            return -1;
        }

        public static string ScabbardFile(int kind)
        {
            foreach (Spec spec in Specs)
                if (spec.Kind == kind)
                    return spec.Scabbard;
            return "scabbard.png";
        }

        /// <summary>HorayModAPI.OnLoadWeaponDatabase 에서 불립니다(월드에 들어갈 때마다).</summary>
        public static void Register(string packageRoot)
        {
            try
            {
                Type database = Reflect.GameType("WeaponDatabase");
                if (Specs.Any(x => Reflect.IsNull(x.Entity) || Reflect.IsNull(x.Prefab)))
                    BuildAll(database, packageRoot);
                foreach (Spec spec in Specs)
                    if (!Reflect.IsNull(spec.Entity))
                        Reflect.Call(null, database, "Register", spec.Entity);
            }
            catch (Exception e)
            {
                Log.Error("카타나 등록 중 오류: " + e);
            }
        }

        private const string WinterPrefabName = "Weapon_Katana_Ice"; // 겨울 대장장이의 습작
        private static readonly string[] WinterNames = { "겨울 대장장이의 습작", "Winter Smith" };

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

        private static void BuildAll(Type database, string packageRoot)
        {
            var all = Reflect.Call(null, database, "GetAll") as IEnumerable;
            if (all == null)
                return;
            // 겨울 대장장이의 습작과 기본 카타나를 찾습니다.
            object winter = null, winterByPrefab = null, basic = null, anyKatana = null;
            foreach (object weapon in all)
            {
                var main = Reflect.Get(weapon, "mainWeaponPrefab") as GameObject;
                if (Reflect.IsNull(main))
                    continue;
                Component simple = main.GetComponent(typeof(WeaponSimple));
                if (Reflect.IsNull(simple) || Convert.ToString(Reflect.Get(simple, "weaponType")) != "Katana")
                    continue;
                string name = DisplayName(weapon);
                if (winter == null && WinterNames.Any(n => name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    winter = weapon;
                if (winterByPrefab == null && main.name == WinterPrefabName)
                    winterByPrefab = weapon;
                if (basic == null && (bool)Reflect.Get(weapon, "isDefaultWeapon"))
                    basic = weapon;
                if (anyKatana == null)
                    anyKatana = weapon;
            }
            winter = winter ?? winterByPrefab;
            basic = basic ?? anyKatana;
            if (basic == null)
            {
                Log.Error("카타나를 찾지 못해 이 모드의 카타나를 등록하지 못했습니다.");
                return;
            }
            if (winter == null)
                Log.Warn("'겨울 대장장이의 습작'을 찾지 못해 서리단조 칼날을 기본 카타나를 바탕으로 만듭니다.");

            // 만변도 : 초승은 카타나의 2단계 새 갈래: 겨울 대장장이의 습작과 같은 무기에서 갈라져 나옵니다.
            int basicId = Convert.ToInt32(Reflect.Get(basic, "id"));
            int winterParent = winter == null ? -1 : Convert.ToInt32(Reflect.Get(winter, "enhanceFromId"));
            int baseParent = winterParent >= 0 ? winterParent : basicId;
            int baseId = Specs.First(x => x.Kind == CrescentAddon.Base).Id;
            Log.Info("카타나 구성: 기본 카타나 #" + basicId + " " + DisplayName(basic)
                     + ", 겨울 대장장이의 습작 " + (winter == null ? "없음" : "#" + Reflect.Get(winter, "id") + " (상위 #" + winterParent + ")")
                     + " → 만변도의 상위 #" + baseParent);

            if (Reflect.IsNull(holder))
            {
                holder = new GameObject("FrostforgedCrescent_Prefabs");
                holder.SetActive(false);
                Reflect.Set(holder, "hideFlags", 61); // HideAndDontSave: 씬이 바뀌어도 지워지지 않게
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }
            foreach (Spec spec in Specs)
            {
                try
                {
                    object template = spec.Kind == CrescentAddon.Frost ? (winter ?? basic) : basic;
                    Build(spec, template, spec.Kind == CrescentAddon.Base ? baseParent : baseId, packageRoot);
                }
                catch (Exception e)
                {
                    Log.Error("카타나 '" + spec.Name + "' 준비 중 오류: " + e);
                }
            }
        }

        private static void Build(Spec spec, object template, int parentId, string packageRoot)
        {
            var templatePrefab = (GameObject)Reflect.Get(template, "mainWeaponPrefab");
            int templateId = (int)Reflect.Get(template, "id");

            // 비활성 부모 아래에 복제해 둡니다. 프리팹의 Awake 는 돌지 않고, 게임이 복제해 쓸 때 활성으로 시작합니다.
            GameObject prefab = (GameObject)UnityEngine.Object.Instantiate(templatePrefab, holder.transform);
            prefab.name = "Weapon-" + spec.Name;
            Component simple = prefab.GetComponent(typeof(WeaponSimple));
            if (Reflect.IsNull(simple))
            {
                Log.Error("복제한 프리팹에 WeaponSimple 이 없습니다.");
                return;
            }

            // 바탕 무기의 효과는 그대로 계승하고, 그 뒤에 이 무기의 효과를 붙입니다.
            var addon = (CrescentAddon)prefab.AddComponent(typeof(CrescentAddon));
            addon.parent = (WeaponSimple)simple;
            addon.enabled = true;
            addon.kind = spec.Kind;
            Reflect.Set(addon, "effectText", NewLocalizedString(spec.EffectKey));

            var existing = (Reflect.Get(simple, "addons") as IEnumerable ?? new object[0]).Cast<object>().ToList();
            Array addons = Array.CreateInstance(typeof(WeaponAddon), existing.Count + 1);
            for (int i = 0; i < existing.Count; i++)
                addons.SetValue(existing[i], i);
            addons.SetValue(addon, existing.Count);
            Reflect.Set(simple, "addons", addons);

            try
            {
                FrameSet.Apply(simple, packageRoot, spec.Drawn, spec.Sheathed);
            }
            catch (Exception e)
            {
                Log.Warn("전용 그림을 넣지 못해 원본 그림을 씁니다: " + e);
            }

            // 흑요도: 베기 궤적을 검은 우주빛으로(공격 데이터를 이 무기 전용으로 복제해 표시해 둡니다).
            if (spec.Kind == CrescentAddon.Obsidian)
            {
                try
                {
                    LanceFx.Claim(simple, LanceFx.ObsidianPalette);
                }
                catch (Exception e)
                {
                    Log.Warn("흑요도 공격 이펙트 준비 중 오류: " + e);
                }
            }

            if (!HorayModAPI.RegisterNetworkPrefab(prefab, spec.NetworkKey))
                Log.Warn("네트워크 프리팹 등록에 실패했습니다. 멀티플레이에서 무기가 보이지 않을 수 있습니다.");

            Type entityType = Reflect.GameType("WeaponEntity");
            UnityEngine.Object created = ScriptableObject.CreateInstance(entityType);
            created.name = spec.Name;
            Reflect.Set(created, "hideFlags", 61);
            Reflect.Set(created, "id", spec.Id);
            Reflect.Set(created, "enabled", true);
            Reflect.Set(created, "activeState", Reflect.Get(template, "activeState"));
            Reflect.Set(created, "aName", NewLocalizedString(spec.NameKey));
            object templateIcon = Reflect.Get(template, "icon");
            object templateBigIcon = Reflect.Get(template, "bigIcon");
            Reflect.Set(created, "icon", SpriteLoader.Load(System.IO.Path.Combine(packageRoot, "Sprites", spec.Icon), templateIcon, "아이콘") ?? templateIcon);
            Reflect.Set(created, "bigIcon", templateBigIcon); // 바탕 무기와 같이 둡니다(없으면 아이콘이 대신 쓰입니다)
            Reflect.Set(created, "mainWeaponPrefab", prefab);
            Reflect.Set(created, "subWeaponPrefab", Reflect.Get(template, "subWeaponPrefab"));
            Reflect.Set(created, "standardEnhancements", Activator.CreateInstance(typeof(List<>).MakeGenericType(Reflect.GameType("EnhancementMetadata"))));
            Reflect.Set(created, "isDefaultWeapon", false);
            Reflect.Set(created, "enhanceFromId", parentId);
            spec.Prefab = prefab;
            spec.Entity = created;

            Log.Info("무기 #" + spec.Id + " '" + spec.Name + "' 준비됨 (바탕 #" + templateId + ", #" + parentId + " 의 강화 무기)");
        }

        private static object NewLocalizedString(string key)
        {
            return Activator.CreateInstance(Reflect.GameType("LocalizedString"), key);
        }
    }
}

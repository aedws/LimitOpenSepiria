using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// '서리기수 : 돌격창 블루비틀' 무기를 만들어 등록합니다.
    ///  - 몸체: '돌격창 기오로이드'의 무기 프리팹을 복제합니다(일반공격: 창격, 대시 공격 불가를 그대로 씁니다).
    ///  - 그림: '무녀의 예언'의 무기 그림을 실행 중에 읽어 푸르게 다시 칠합니다(게임 그림은 모드에 담지 않습니다).
    ///  - 강화 경로: '백상의 봉'의 강화 무기로 등록합니다.
    /// 세 무기는 ID 가 코드에 없어 이름으로 찾고, 못 찾으면 구성으로 찾습니다.
    /// </summary>
    internal static class LanceFactory
    {
        public const int WeaponId = 9101402;
        public const string NameKey = "Weapon_FFC_BlueBeetle_Name";
        public const string EffectKey = "WeaponAddon_FFC_BlueBeetle_Effect";
        private const string NetworkKey = Entry.ModId + ":weapon-bluebeetle";

        private static readonly string[] BodyNames = { "기오로이드", "Gioroid", "Gyoroid" };
        private static readonly string[] LookNames = { "무녀의 예언", "Voluspa", "Völuspá", "Prophecy of the Seeress" };
        private static readonly string[] ParentNames = { "백상의 봉" };

        private static GameObject holder;
        private static GameObject prefab;
        private static UnityEngine.Object entity;
        private static bool failed;

        public static void Register(string packageRoot)
        {
            try
            {
                Type database = Reflect.GameType("WeaponDatabase");
                if (!failed && (Reflect.IsNull(entity) || Reflect.IsNull(prefab)))
                    Build(database, packageRoot);
                if (!Reflect.IsNull(entity))
                    Reflect.Call(null, database, "Register", entity);
            }
            catch (Exception e)
            {
                failed = true;
                Log.Error("블루비틀 등록 중 오류: " + e);
            }
        }

        private static string DisplayName(object weapon)
        {
            try { return Convert.ToString(Reflect.Get(weapon, "aName")) ?? ""; }
            catch (Exception) { return ""; }
        }

        private static Component Simple(object weapon)
        {
            var main = Reflect.Get(weapon, "mainWeaponPrefab") as GameObject;
            return Reflect.IsNull(main) ? null : main.GetComponent(typeof(WeaponSimple));
        }

        private static List<object> Addons(Component simple)
        {
            return (Reflect.Get(simple, "addons") as IEnumerable ?? new object[0]).Cast<object>().Where(a => !Reflect.IsNull(a)).ToList();
        }

        private static object ByName(List<object> weapons, string[] names)
        {
            return weapons.FirstOrDefault(w =>
            {
                string name = DisplayName(w);
                return names.Any(n => name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        private static bool HasRapier(object weapon)
        {
            Component simple = Simple(weapon);
            return !Reflect.IsNull(simple) && Addons(simple).Any(a => a.GetType().Name == "WeaponAddon_Rapier");
        }

        // '무녀의 예언'을 이름으로 못 찾을 때: 무기 공격으로 볼루스파를 발동시키는 능력치가 붙은 무기
        private static bool HasSpearStat(object weapon)
        {
            Component simple = Simple(weapon);
            if (Reflect.IsNull(simple))
                return false;
            foreach (object addon in Addons(simple))
            {
                var stats = Reflect.Get(addon, "status") as IEnumerable;
                if (stats == null)
                    continue;
                foreach (object stat in stats)
                    if (Convert.ToString(Reflect.Get(stat, "statId")) == "ICESPEARWITHWEAPONATTACK")
                        return true;
            }
            return false;
        }

        private static void Build(Type database, string packageRoot)
        {
            var all = Reflect.Call(null, database, "GetAll") as IEnumerable;
            if (all == null)
                return;
            List<object> weapons = all.Cast<object>().Where(w => !Reflect.IsNull(Simple(w))).ToList();

            object body = ByName(weapons, BodyNames);
            if (body == null || !(Simple(body).GetType().Name == "WeaponSimple_SwordAndShield"))
                body = weapons.FirstOrDefault(HasRapier);
            if (body == null)
            {
                failed = true;
                Log.Error("'돌격창 기오로이드'(창격을 쓰는 무기)를 찾지 못해 블루비틀을 등록하지 못했습니다.");
                return;
            }
            object look = ByName(weapons, LookNames) ?? weapons.FirstOrDefault(HasSpearStat);
            object parent = ByName(weapons, ParentNames);
            if (look == null)
                Log.Warn("'무녀의 예언'을 찾지 못해 블루비틀이 기오로이드의 그림을 푸르게 칠해 씁니다.");
            if (parent == null)
                Log.Warn("'백상의 봉'을 찾지 못해 블루비틀을 기오로이드의 강화 무기로 등록합니다.");

            var bodyPrefab = (GameObject)Reflect.Get(body, "mainWeaponPrefab");
            Log.Info("블루비틀 구성: 몸체 #" + Reflect.Get(body, "id") + " " + DisplayName(body) + " (" + bodyPrefab.name + ")"
                     + ", 그림 " + (look == null ? "없음" : "#" + Reflect.Get(look, "id") + " " + DisplayName(look))
                     + ", 상위 " + (parent == null ? "없음" : "#" + Reflect.Get(parent, "id") + " " + DisplayName(parent)));

            if (Reflect.IsNull(holder))
            {
                holder = new GameObject("FrostforgedCrescent_LancePrefabs");
                holder.SetActive(false);
                Reflect.Set(holder, "hideFlags", 61);
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }
            prefab = (GameObject)UnityEngine.Object.Instantiate(bodyPrefab, holder.transform);
            prefab.name = "Weapon-FFC-BlueBeetle";
            Component simple = prefab.GetComponent(typeof(WeaponSimple));

            // 무기 효과: 기오로이드의 것 중 창격만 남기고, 블루비틀의 효과를 붙입니다.
            List<object> inherited = Addons(simple);
            List<object> kept = inherited.Where(a => a.GetType().Name == "WeaponAddon_Rapier").ToList();
            Log.Info("블루비틀 무기 효과: 기오로이드의 [" + string.Join(", ", inherited.Select(a => a.GetType().Name).ToArray()) + "] 중 창격 " + kept.Count + "개 계승");
            if (kept.Count == 0)
            {
                Type rapier = Reflect.GameType("WeaponAddon_Rapier");
                if (rapier != null)
                {
                    Component added = prefab.AddComponent(rapier);
                    Reflect.Set(added, "parent", simple);
                    object source = weapons.Where(HasRapier).Select(w => Addons(Simple(w)).First(a => a.GetType().Name == "WeaponAddon_Rapier")).FirstOrDefault();
                    if (source != null)
                        Reflect.Set(added, "effectText", Reflect.Get(source, "effectText"));
                    kept.Add(added);
                }
            }
            var addon = (LanceAddon)prefab.AddComponent(typeof(LanceAddon));
            addon.parent = (WeaponSimple)simple;
            addon.enabled = true;
            Reflect.Set(addon, "effectText", NewLocalizedString(EffectKey));
            Array addons = Array.CreateInstance(typeof(WeaponAddon), kept.Count + 1);
            for (int i = 0; i < kept.Count; i++)
                addons.SetValue(kept[i], i);
            addons.SetValue(addon, kept.Count);
            Reflect.Set(simple, "addons", addons);

            try
            {
                ApplyLook(simple, look == null ? null : Simple(look), packageRoot);
            }
            catch (Exception e)
            {
                Log.Warn("블루비틀 그림을 넣지 못해 기오로이드의 그림을 씁니다: " + e);
            }

            if (!HorayModAPI.RegisterNetworkPrefab(prefab, NetworkKey))
                Log.Warn("블루비틀 네트워크 프리팹 등록에 실패했습니다. 멀티플레이에서 무기가 보이지 않을 수 있습니다.");

            object iconSource = look ?? body;
            var icon = Reflect.Get(iconSource, "icon") as Sprite;
            var bigIcon = Reflect.Get(iconSource, "bigIcon") as Sprite;
            Type entityType = Reflect.GameType("WeaponEntity");
            UnityEngine.Object created = ScriptableObject.CreateInstance(entityType);
            created.name = "FFC_BlueBeetle";
            Reflect.Set(created, "hideFlags", 61);
            Reflect.Set(created, "id", WeaponId);
            Reflect.Set(created, "enabled", true);
            Reflect.Set(created, "activeState", Reflect.Get(body, "activeState"));
            Reflect.Set(created, "aName", NewLocalizedString(NameKey));
            Reflect.Set(created, "icon", SpriteTint.Bluer(icon, null, 1f) ?? icon);
            Reflect.Set(created, "bigIcon", Reflect.IsNull(bigIcon) ? null : (SpriteTint.Bluer(bigIcon, null, 1f) ?? bigIcon));
            Reflect.Set(created, "mainWeaponPrefab", prefab);
            Reflect.Set(created, "subWeaponPrefab", Reflect.Get(body, "subWeaponPrefab"));
            Reflect.Set(created, "standardEnhancements", Activator.CreateInstance(typeof(List<>).MakeGenericType(Reflect.GameType("EnhancementMetadata"))));
            Reflect.Set(created, "isDefaultWeapon", false);
            Reflect.Set(created, "enhanceFromId", (int)Reflect.Get(parent ?? body, "id"));
            entity = created;
            SpriteTint.Release();

            Log.Info("무기 #" + WeaponId + " 블루비틀 준비됨 (#" + Reflect.Get(parent ?? body, "id") + " 의 강화 무기)");
        }

        private static Sprite BodySprite(Component simple, out object renderer, out object stencil)
        {
            object weaponBody = Reflect.Get(simple, "mainWeaponBody");
            renderer = Reflect.IsNull(weaponBody) ? null : Reflect.Get(weaponBody, "weaponSpriteRenderer");
            stencil = Reflect.IsNull(weaponBody) ? null : Reflect.Get(weaponBody, "weaponStencilRenderer");
            return Reflect.IsNull(renderer) ? null : Reflect.Get(renderer, "sprite") as Sprite;
        }

        private static string Describe(Sprite sprite)
        {
            if (Reflect.IsNull(sprite))
                return "없음";
            Rect rect = sprite.rect;
            Vector2 pivot = sprite.pivot;
            return sprite.name + " " + rect.width + "x" + rect.height + "px, 기준점 (" + pivot.x.ToString("0.#", CultureInfo.InvariantCulture) + ", "
                   + pivot.y.ToString("0.#", CultureInfo.InvariantCulture) + "), " + sprite.pixelsPerUnit + "px = 1단위";
        }

        // 손에 든 창의 그림을 바꿉니다. 손에 쥐는 위치(기준점)는 기본적으로 기오로이드의 것을 따릅니다.
        private static void ApplyLook(Component simple, Component lookSimple, string packageRoot)
        {
            object renderer, stencil, unusedA, unusedB;
            Sprite lance = BodySprite(simple, out renderer, out stencil);
            Sprite source = Reflect.IsNull(lookSimple) ? null : BodySprite(lookSimple, out unusedA, out unusedB);
            Log.Info("블루비틀 그림: 기오로이드 [" + Describe(lance) + "], 무녀의 예언 [" + Describe(source) + "]");
            if (Reflect.IsNull(renderer) || Reflect.IsNull(lance))
            {
                Log.Warn("기오로이드의 무기 그림 위치가 예상과 달라 그림을 바꾸지 못했습니다.");
                return;
            }
            if (Reflect.IsNull(source))
                source = lance;

            Dictionary<string, string> options = ReadOptions(Path.Combine(packageRoot, "Sprites", "lance.txt"));
            Rect lanceRect = lance.rect;
            Vector2 lancePivot = lance.pivot;
            Vector2? pivot = new Vector2(lancePivot.x / lanceRect.width, lancePivot.y / lanceRect.height);
            if (Option(options, "pivot", "lance") == "source")
                pivot = null;
            float x, y, scale = 1f;
            if (TryNumber(options, "pivotx", out x) && TryNumber(options, "pivoty", out y))
                pivot = new Vector2(x, y);
            TryNumber(options, "scale", out scale);

            Sprite tinted = SpriteTint.Bluer(source, pivot, scale <= 0f ? 1f : scale);
            if (tinted == null)
                return;
            Reflect.Set(renderer, "sprite", tinted);
            if (!Reflect.IsNull(stencil))
                Reflect.Set(stencil, "sprite", tinted);
            foreach (string field in new[] { "rapierFormSprite", "swordFormSprite" })
                if (!Reflect.IsNull(Reflect.Get(simple, field)))
                    Reflect.Set(simple, field, tinted);
            Log.Info("블루비틀 그림 적용 (기준점 " + (pivot.HasValue ? pivot.Value.x.ToString("0.##", CultureInfo.InvariantCulture) + ", " + pivot.Value.y.ToString("0.##", CultureInfo.InvariantCulture) : "원본")
                     + ", 배율 " + scale.ToString("0.##", CultureInfo.InvariantCulture) + ")");
        }

        private static Dictionary<string, string> ReadOptions(string path)
        {
            var options = new Dictionary<string, string>();
            try
            {
                if (File.Exists(path))
                    foreach (string line in File.ReadAllLines(path))
                    {
                        int at = line.IndexOf('=');
                        if (at > 0 && !line.TrimStart().StartsWith("#"))
                            options[line.Substring(0, at).Trim().ToLowerInvariant()] = line.Substring(at + 1).Trim().ToLowerInvariant();
                    }
            }
            catch (Exception)
            {
            }
            return options;
        }

        private static string Option(Dictionary<string, string> options, string key, string fallback)
        {
            string value;
            return options.TryGetValue(key, out value) ? value : fallback;
        }

        private static bool TryNumber(Dictionary<string, string> options, string key, out float value)
        {
            value = 1f;
            string text;
            float parsed;
            if (!options.TryGetValue(key, out text) || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return false;
            value = parsed;
            return true;
        }

        private static object NewLocalizedString(string key)
        {
            return Activator.CreateInstance(Reflect.GameType("LocalizedString"), key);
        }
    }
}

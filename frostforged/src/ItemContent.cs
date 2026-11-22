using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 아티팩트 5종과 '생명' 콤보를 등록합니다.
    ///
    /// 아이템은 모두 게임의 '능력치만 주는 아티팩트'(Charm_StatusInstance)로 만듭니다. 게임에 없는 효과는
    /// 이 모드의 능력치(FFC_...)로 정의하고, 그 능력치를 읽어 실제 동작을 하는 쪽은 ItemEffects 입니다.
    /// 그래서 툴팁의 레벨별 수치·획득·해제·콤보 계산은 전부 게임 원래 경로를 탑니다.
    /// </summary>
    internal static class ItemContent
    {
        public const string VitalityCategory = "FFCVITALITY";
        public const string MaxHpDamageId = "FFC_MaxHpDamage";

        // 능력치 키(캐릭터에 쌓이는 이름). 상태 ID 는 FFC_ 가 붙은 형태입니다.
        public const string IceCrit = "FFCICECRIT";
        public const string WeaponToIce = "FFCWEAPONTOICE";
        public const string HealOnHit = "FFCHEALONHIT";
        public const string LastStand = "FFCLASTSTAND";
        public const string MaxHpDamage = "FFCMAXHPDAMAGE";

        private sealed class CustomStat
        {
            public string Id, Key, Keyword, Symbol;
            public bool ShowValue;
            public int Divide = 1;   // 표시할 때 나누는 수
        }

        private static readonly CustomStat[] CustomStats =
        {
            new CustomStat { Id = "FFC_ICE_CRIT", Key = IceCrit, Keyword = "FFC_IceCrit", Symbol = "%", ShowValue = true },
            new CustomStat { Id = "FFC_WEAPON_TO_ICE", Key = WeaponToIce, Keyword = "FFC_WeaponToIce", Symbol = "%", ShowValue = true },
            new CustomStat { Id = "FFC_HEAL_ON_HIT", Key = HealOnHit, Keyword = "FFC_HealOnHit", Symbol = "%", ShowValue = true, Divide = 100 }, // 값 10 = 0.1%
            new CustomStat { Id = "FFC_LAST_STAND", Key = LastStand, Keyword = "FFC_LastStand", Symbol = "", ShowValue = false },
            new CustomStat { Id = "FFC_MAXHP_DAMAGE", Key = MaxHpDamage, Keyword = "FFC_MaxHpDamage", Symbol = "%", ShowValue = true },
        };

        private sealed class Stat
        {
            public string Id;
            public int[] Values;
            public bool HideZero;
            public Stat(string id, bool hideZero, params int[] values) { Id = id; Values = values; HideZero = hideZero; }
        }

        private sealed class Item
        {
            public int Id;
            public string Name;       // 내부 이름(키·파일 이름)
            public bool Bond;
            public string[] Categories;
            public Stat[] Stats;
            public GameObject Prefab;
            public UnityEngine.Object Entity;
        }

        private static readonly Item[] Items =
        {
            new Item
            {
                Id = 9101411, Name = "FrostBlade", Bond = true, Categories = new[] { "FROST", "PRECISION" },
                Stats = new[]
                {
                    new Stat("FFC_ICE_CRIT", false, 10, 20, 30, 40, 50),
                    new Stat("CHARGING_CHARM_AMPLIFY", true, 0, 0, 1, 1, 2),
                    new Stat("FFC_WEAPON_TO_ICE", false, 90, 80, 70, 60, 50),
                }
            },
            new Item
            {
                Id = 9101412, Name = "Frostmourne2", Bond = true, Categories = new[] { "FROST", "GLACIER" },
                Stats = new[]
                {
                    new Stat("ICE_DAMAGE", false, 5, 10, 15, 20, 30),
                    new Stat("CHARGING_CHARM_AMPLIFY", true, 0, 0, 1, 1, 2),
                    new Stat("FREEZE_DAMAGE", false, 20, 25, 30, 40, 50),
                }
            },
            new Item
            {
                Id = 9101413, Name = "ScoreNorthWind", Bond = true, Categories = new[] { "FROST", "WINDSONG" },
                Stats = new[]
                {
                    new Stat("ATTACK_SPEED", false, 4, 8, 12, 16, 20),
                    new Stat("CHARGING_CHARM_AMPLIFY", true, 0, 0, 1, 1, 2),
                }
            },
            new Item
            {
                Id = 9101414, Name = "BloodstonePendant", Categories = new[] { VitalityCategory },
                Stats = new[]
                {
                    new Stat("FFC_HEAL_ON_HIT", false, 10, 30, 50), // 0.01% 단위: 0.1 / 0.3 / 0.5%
                    new Stat("FINAL_HP", false, 10, 20, 30),
                }
            },
            new Item
            {
                Id = 9101415, Name = "BloodstoneCross", Categories = new[] { VitalityCategory },
                Stats = new[]
                {
                    new Stat("HP_STEAL", false, 10, 25, 50),
                    new Stat("FFC_LAST_STAND", false, 1, 1, 1),
                }
            },
        };

        // 기존 아티팩트 '혈석 귀걸이'에 생명 콤보를 붙입니다. 아이템 ID 가 코드에 없어 이름으로 찾습니다.
        private static readonly string[] EarringNames = { "혈석 귀걸이", "Bloodstone Earring" };

        private static string root;
        private static UnityEngine.Object category;
        private static GameObject comboPrefab;
        private static readonly List<UnityEngine.Object> Keep = new List<UnityEngine.Object>();

        public static IEnumerable<int> ItemIds { get { return Items.Select(i => i.Id); } }

        public static void Hook(string packageRoot)
        {
            root = packageRoot ?? "";
            HorayModAPI.OnLoadKeywordDatabase += RegisterKeywords;
            HorayModAPI.OnLoadStatusDatabase += RegisterStatuses;
            HorayModAPI.OnLoadItemDatabase += RegisterItems;
        }

        public static void Unhook()
        {
            HorayModAPI.OnLoadKeywordDatabase -= RegisterKeywords;
            HorayModAPI.OnLoadStatusDatabase -= RegisterStatuses;
            HorayModAPI.OnLoadItemDatabase -= RegisterItems;
        }

        // ── 능력치 이름(툴팁 문구) ────────────────────────────────────────────
        private static readonly Dictionary<string, UnityEngine.Object> Keywords = new Dictionary<string, UnityEngine.Object>();
        private static readonly Dictionary<string, UnityEngine.Object> Statuses = new Dictionary<string, UnityEngine.Object>();

        private static void RegisterKeywords()
        {
            try
            {
                Type type = Reflect.GameType("KeywordEntity");
                foreach (CustomStat stat in CustomStats)
                {
                    UnityEngine.Object keyword;
                    if (!Keywords.TryGetValue(stat.Keyword, out keyword) || Reflect.IsNull(keyword))
                    {
                        keyword = ScriptableObject.CreateInstance(type);
                        keyword.name = stat.Keyword;
                        Reflect.Set(keyword, "hideFlags", 61);
                        Reflect.Set(keyword, "keyword", stat.Keyword);
                        Reflect.Set(keyword, "needParseValueOnVisualText", stat.ShowValue); // 문구 안의 {VALUE} 자리에 수치
                        Reflect.Set(keyword, "visualText", Text("Keyword_" + stat.Keyword));
                        Keywords[stat.Keyword] = keyword;
                    }
                    Reflect.Call(null, Reflect.GameType("KeywordDatabase"), "Register", keyword);
                }
            }
            catch (Exception e)
            {
                Log.Error("능력치 문구 등록 중 오류: " + e);
            }
        }

        private static void RegisterStatuses()
        {
            try
            {
                Type type = Reflect.GameType("StatusEntity");
                foreach (CustomStat stat in CustomStats)
                {
                    UnityEngine.Object status;
                    if (!Statuses.TryGetValue(stat.Id, out status) || Reflect.IsNull(status))
                    {
                        status = ScriptableObject.CreateInstance(type);
                        status.name = stat.Id;
                        Reflect.Set(status, "hideFlags", 61);
                        Reflect.Set(status, "id", stat.Id);
                        Reflect.Set(status, "className", "StatusInstance_Custom/" + stat.Key);
                        Reflect.Set(status, "statKeyword", stat.Keyword);
                        Reflect.Set(status, "includePositiveNegativeSign", false);
                        Reflect.Set(status, "displayValue", stat.ShowValue);
                        Reflect.Set(status, "symbol", stat.Symbol);
                        Reflect.Set(status, "divideForDisplay", stat.Divide);
                        Statuses[stat.Id] = status;
                    }
                    Reflect.Call(null, Reflect.GameType("StatusDatabase"), "Register", status);
                }
            }
            catch (Exception e)
            {
                Log.Error("능력치 등록 중 오류: " + e);
            }
        }

        // ── 아이템·콤보 ──────────────────────────────────────────────────────
        private static void RegisterItems()
        {
            try
            {
                Type database = Reflect.GameType("ItemDatabase");
                RegisterCategory(database);

                int registered = 0;
                foreach (Item item in Items)
                {
                    if (Reflect.IsNull(item.Entity) || Reflect.IsNull(item.Prefab))
                        Build(item);
                    if (Reflect.IsNull(item.Entity))
                        continue;
                    Reflect.Call(null, database, "Register", item.Entity);
                    registered++;
                }
                Log.Info("아티팩트 " + registered + "/" + Items.Length + "개 등록");
                AttachToEarring(database);
            }
            catch (Exception e)
            {
                Log.Error("아티팩트 등록 중 오류: " + e);
            }
        }

        private static void Build(Item item)
        {
            Type charmType = Reflect.GameType("Charm_StatusInstance");
            Type groupType = Reflect.GameType("Charm_StatusInstance+StatusGroup");
            Type entityType = Reflect.GameType("ItemEntity");
            if (charmType == null || groupType == null || entityType == null)
            {
                Log.Error("아티팩트에 필요한 게임 타입을 찾지 못했습니다.");
                return;
            }

            // 프리팹: 게임의 '능력치 아티팩트' 하나.
            GameObject prefab = HorayModAPI.CreateNetworkPrefab("Charm-FFC_" + item.Name);
            Component charm = prefab.AddComponent(charmType);
            Array groups = Array.CreateInstance(groupType, item.Stats.Length);
            for (int i = 0; i < item.Stats.Length; i++)
            {
                object group = Activator.CreateInstance(groupType);
                Reflect.Set(group, "statusID", item.Stats[i].Id);
                Reflect.Set(group, "valuesByLevel", item.Stats[i].Values);
                Reflect.Set(group, "hideIfStatValueIsZero", item.Stats[i].HideZero);
                groups.SetValue(group, i);
            }
            Reflect.Set(charm, "stats", groups);
            Reflect.Set(charm, "showStatsEffectStringFirst", true);
            Reflect.Set(charm, "maxLevel", item.Stats[0].Values.Length); // 값이 N 개면 최대 레벨 N (마지막 값은 최대 레벨까지 유지)
            Reflect.Set(charm, "effectsString", Array.CreateInstance(Reflect.GameType("LocalizedString"), 0));
            Reflect.Set(charm, "isUniqueEffect", true); // [고유]
            if (!HorayModAPI.RegisterNetworkPrefab(prefab, Entry.ModId + ":item-" + item.Name.ToLowerInvariant()))
                Log.Warn("'" + item.Name + "' 네트워크 프리팹 등록 실패");
            item.Prefab = prefab;

            object icon = SpriteLoader.Load(Path.Combine(root, "Sprites", "Items", item.Name + ".png"), null, "아이콘 " + item.Name);

            UnityEngine.Object entity = ScriptableObject.CreateInstance(entityType);
            entity.name = "FFC_" + item.Name;
            Reflect.Set(entity, "hideFlags", 61);
            Reflect.Set(entity, "id", item.Id);
            Reflect.Set(entity, "activeType", 0);            // Default
            Reflect.Set(entity, "type", 5);                  // Charm
            Reflect.Set(entity, "rarity", 3);                // Legend
            Reflect.Set(entity, "cost", 1100);
            Reflect.Set(entity, "isDual", item.Bond);        // [결속]
            Reflect.Set(entity, "aName", Text("Item_FFC_" + item.Name + "_Name"));
            Reflect.Set(entity, "aFlavorText", Text("Item_FFC_" + item.Name + "_FlavorText"));
            Reflect.Set(entity, "categories", item.Categories.ToList());
            Reflect.Set(entity, "icon", icon);
            Reflect.Set(entity, "iconInWorld", icon);
            Reflect.Set(entity, "resourcePrefab", prefab);
            item.Entity = entity;
        }

        // '생명' 콤보: (2) 최대 HP 비례 추가 피해 10%, (3) HP 흡수 +42.
        private static void RegisterCategory(Type database)
        {
            var categories = Reflect.GetStatic(database, "itemCategories") as IDictionary;
            if (categories == null)
            {
                Log.Error("콤보 목록을 찾지 못해 '생명' 콤보를 등록하지 못했습니다.");
                return;
            }

            if (Reflect.IsNull(category) || Reflect.IsNull(comboPrefab))
            {
                Type comboType = Reflect.GameType("ComboEffectBase");
                Type tierType = Reflect.GameType("ComboEffectBase+ComboStat");
                Type categoryType = Reflect.GameType("ItemCategoryEntity");

                comboPrefab = HorayModAPI.CreateNetworkPrefab("Combo-FFC_Vitality");
                Component combo = comboPrefab.AddComponent(comboType);
                Reflect.Set(combo, "defaultEffect", Text("ComboEffect_FFC_Vitality"));
                Array tiers = Array.CreateInstance(tierType, 2);
                tiers.SetValue(Tier(tierType, 2, "FFC_MAXHP_DAMAGE/10"), 0);
                tiers.SetValue(Tier(tierType, 3, "HP_STEAL/42"), 1);
                Reflect.Set(combo, "addStatByCombo", tiers);
                if (!HorayModAPI.RegisterNetworkPrefab(comboPrefab, Entry.ModId + ":combo-vitality"))
                    Log.Warn("'생명' 콤보 네트워크 프리팹 등록 실패");

                object icon = SpriteLoader.Load(Path.Combine(root, "Sprites", "Items", "Vitality.png"), null, "콤보 아이콘");
                UnityEngine.Object created = ScriptableObject.CreateInstance(categoryType);
                created.name = "FFC_Vitality";
                Reflect.Set(created, "hideFlags", 61);
                Reflect.Set(created, "id", VitalityCategory);
                Reflect.Set(created, "isEnabled", true);
                Reflect.Set(created, "categoryName", Text("ItemCategory_FFC_Vitality"));
                Reflect.Set(created, "categoryFruitName", Text("ItemCategory_FFC_Vitality_Fruit"));
                Reflect.Set(created, "categoryIcon", icon);
                Reflect.Set(created, "categoryFruitIcon", icon);
                Reflect.Set(created, "categoryIceFruitIcon", icon);
                Reflect.Set(created, "comboEffectPrefab", comboPrefab);
                // 콤보가 켜질 때 나는 소리는 기존 콤보의 것을 씁니다.
                foreach (object existing in categories.Values)
                {
                    if (Reflect.IsNull(existing))
                        continue;
                    Reflect.Set(created, "enableSound", Reflect.Get(existing, "enableSound"));
                    break;
                }
                category = created;
            }

            categories[VitalityCategory] = category;
        }

        private static object Tier(Type type, int count, string status)
        {
            object tier = Activator.CreateInstance(type);
            Reflect.Set(tier, "comboCount", count);
            Reflect.Set(tier, "status", new[] { status });
            return tier;
        }

        private static void AttachToEarring(Type database)
        {
            var ids = Reflect.Call(null, database, "GetAllItemID") as IEnumerable;
            if (ids == null)
                return;
            foreach (object id in ids)
            {
                object entity = Reflect.Call(null, database, "FindItemById", id);
                if (Reflect.IsNull(entity))
                    continue;
                string name;
                try { name = Convert.ToString(Reflect.Get(entity, "aName")) ?? ""; }
                catch (Exception) { continue; }
                if (!EarringNames.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var list = Reflect.Get(entity, "categories") as IList;
                if (list == null)
                    return;
                if (list.Contains(VitalityCategory))
                {
                    Log.Info("'" + name + "'(#" + id + ")에는 이미 생명 콤보가 있습니다.");
                    return;
                }
                if (list.Count >= 2)
                {
                    Log.Warn("'" + name + "'(#" + id + ")은 이미 콤보가 2개라 생명 콤보를 붙이지 않았습니다(툴팁이 2개까지만 표시합니다).");
                    return;
                }
                list.Add(VitalityCategory);
                Log.Info("'" + name + "'(#" + id + ")에 생명 콤보 부여");
                return;
            }
            Log.Warn("'혈석 귀걸이'를 찾지 못해 생명 콤보를 붙이지 못했습니다.");
        }

        private static object Text(string key)
        {
            return Activator.CreateInstance(Reflect.GameType("LocalizedString"), key);
        }
    }
}

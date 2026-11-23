using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FrostforgedCrescent
{
    /// <summary>
    /// 우주 속성: 이 모드가 추가하는 속성. 모두 서버(호스트)에서 처리합니다.
    ///
    /// 피해   우주 속성 피해(FFCCOSMIC) 수치만으로 계산합니다. 게임의 속성 목록에는 없는 속성이라, 내부적으로는 어느 속성에도
    ///        속하지 않는 피해로 두고 피해 이름(FFC_Cosmic_...)과 색으로 구분합니다.
    /// 무지   일식 콤보(1)가 켜져 있을 때 우주 피해(디버프 피해 제외)를 주면 걸립니다(적마다 2초에 한 번).
    ///        둔화 25%(10초) + 0.5초마다 15%(4초) + 2초 뒤 180% 가 한꺼번에 걸리고, 중첩 수만큼 강해집니다.
    ///        5중첩이 되면 허무로 바뀝니다.
    /// 허무   500% 피해와 기절.
    /// 태극   5초마다 충전되는 발동형 아티팩트(참월 흑·백, 섬멸 청, 명멸 적).
    /// </summary>
    internal static class Cosmic
    {
        public const string IdPrefix = "FFC_Cosmic_";
        public const string DebuffPrefix = "FFC_Cosmic_Debuff_";
        public const string TaegeukPrefix = "FFC_Cosmic_Taegeuk_";
        private const string IgnoranceId = DebuffPrefix + "Ignorance";
        private const string VoidId = DebuffPrefix + "Void";
        private const string ScoreId = IdPrefix + "Score";

        // 무지·허무의 기본값
        public const float ApplyCooldown = 2f;
        public const int VoidStacks = 5;
        public const float SlowPerStack = 0.25f, SlowTime = 10f, SlowCap = 0.8f;
        public const float BurstTime = 2f, BurstPercent = 180f;
        public const float DotTime = 4f, DotInterval = 0.5f, DotPercent = 15f, DotPercentUp = 25f;
        public const float VoidPercent = 500f, VoidPercentUp = 750f, VoidStun = 2f;
        // 태극
        public const float ChargeTime = 5f, SpearRange = 9f, FlatDamage = 30f;
        // 참월: 선이 그어지고 SlashDelay 뒤부터 SlashHits 번, SlashInterval 간격으로 피해(총량은 그대로, 타수만큼 나눔)
        public const float SlashDelay = 0.2f, SlashInterval = 0.2f, SlashHalfWidth = 4f, SlashReach = 21f;
        public const int SlashHits = 3;

        public static readonly Color Violet = new Color(0.66f, 0.52f, 1f, 1f);
        private static readonly Color White = new Color(1f, 1f, 1f, 1f);

        private sealed class Ignorance
        {
            public object Attacker;
            public int Stacks;
            public float SlowUntil, BurstAt, DotUntil, NextTick, LastApply = -999f, Slowed;
        }

        private sealed class Charges { public readonly float[] Time = new float[4]; public float Check, LastCall = -999f; public bool BasicPending; public readonly bool[] Hud = new bool[4], Ready = new bool[4]; }

        private sealed class Delayed { public float At; public Action Run; }

        private static readonly Dictionary<object, Ignorance> Victims = new Dictionary<object, Ignorance>();
        private static readonly Dictionary<object, Charges> Players = new Dictionary<object, Charges>();
        private static readonly List<Delayed> Queue = new List<Delayed>();
        private static readonly System.Random Dice = new System.Random();
        private static readonly bool[] Logged = new bool[8];
        private static float now;
        private static GameObject ticker;
        private static PropertyInfo serverActive;
        private static bool loggedFailure, firingChain;

        public static bool IsCosmic(string id) { return id != null && id.StartsWith(IdPrefix); }
        public static bool IsDebuff(string id) { return id != null && id.StartsWith(DebuffPrefix); }
        public static bool IsTaegeuk(string id) { return id != null && id.StartsWith(TaegeukPrefix); }

        public static void Apply(Harmony harmony)
        {
            Type server = Reflect.UnityType("Mirror.NetworkServer", "Mirror");
            serverActive = server != null ? server.GetProperty("active", Reflect.Any) : null;
            MethodInfo basic = typeof(WeaponSimple).GetMethods(Reflect.Any).FirstOrDefault(m => m.Name == "BasicAttackCreated" && m.GetParameters().Length == 2);
            if (basic != null)
                harmony.Patch(basic, postfix: new HarmonyMethod(typeof(Cosmic), nameof(BasicAttackPostfix)));
            else
                Log.Warn("일반 공격 지점을 찾지 못해 참월(흑·백)은 일반 공격으로 발동하지 않습니다.");
        }

        private static void Once(int index, string message)
        {
            if (Logged[index])
                return;
            Logged[index] = true;
            Log.Info(message);
        }

        private static void Fail(string where, Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("우주 속성 처리 중 오류(" + where + ", 이후 같은 오류는 기록하지 않습니다): " + e);
        }

        private static bool Dead(object unit)
        {
            return Reflect.IsNull(unit) || Equals(Reflect.Get(unit, "IsDead"), true);
        }

        private static Vector3 Position(object unit)
        {
            return ((Component)unit).transform.position;
        }

        private static int Stat(object avatar, string key)
        {
            return Reflect.IsNull(avatar) ? 0 : ItemEffects.Stat(avatar, key);
        }

        /// <summary>우주 속성 피해 수치의 percent% 에 해당하는 피해량</summary>
        private static float Power(object attacker, float percent)
        {
            return Stat(attacker, ItemContent.Cosmic) * percent / 100f;
        }

        // ── 매 프레임 처리 ────────────────────────────────────────────────────
        private static void EnsureTicker()
        {
            if (!Reflect.IsNull(ticker))
                return;
            ticker = new GameObject("FrostforgedCrescent_Cosmic");
            Reflect.Set(ticker, "hideFlags", 61);
            UnityEngine.Object.DontDestroyOnLoad(ticker);
            ticker.AddComponent(typeof(CosmicTicker));
        }

        /// <summary>CosmicTicker 가 매 프레임 부릅니다.</summary>
        public static void Tick(float delta)
        {
            try
            {
                if (serverActive == null || !(bool)serverActive.GetValue(null, null))
                {
                    if (Victims.Count > 0) Victims.Clear();
                    if (Queue.Count > 0) Queue.Clear();
                    return;
                }
                now += delta;
                if (Queue.Count > 0)
                {
                    List<Delayed> due = Queue.Where(d => d.At <= now).ToList();
                    foreach (Delayed item in due)
                    {
                        Queue.Remove(item);
                        try { item.Run(); }
                        catch (Exception e) { Fail("예약 실행", e); }
                    }
                }
                if (Victims.Count > 0)
                    TickIgnorance();
                if (Players.Count > 0)
                    TickCharges(delta);
            }
            catch (Exception e)
            {
                Fail("매 프레임", e);
            }
        }

        private static void Later(float delay, Action run)
        {
            Queue.Add(new Delayed { At = now + delay, Run = run });
        }

        // ── 무지 ─────────────────────────────────────────────────────────────
        /// <summary>attacker 가 victim 에게 우주 피해(디버프 제외)를 줌.</summary>
        public static void OnCosmicHit(object attacker, object victim)
        {
            try
            {
                if (Dead(victim) || Stat(attacker, ItemContent.EclipseOn) <= 0)
                    return;
                EnsureTicker();
                Ignorance state;
                if (!Victims.TryGetValue(victim, out state))
                    Victims[victim] = state = new Ignorance();
                if (now - state.LastApply < ApplyCooldown)
                    return;
                state.LastApply = now;
                state.Attacker = attacker;

                // 깊은 무지: 무지 1중첩의 피해를 준 뒤 곧바로 허무
                if (Dice.Next(100) < Stat(attacker, ItemContent.DeepIgnorance))
                {
                    Hurt(attacker, victim, Power(attacker, BurstPercent) * IgnoranceBonus(attacker), IgnoranceId);
                    Once(2, "우주 속성 확인: 깊은 무지 → 허무");
                    Void(attacker, victim, state);
                    return;
                }

                int applications = 1 + Math.Max(0, Stat(attacker, ItemContent.IgnoranceExtra));
                int perApplication = 1 + Math.Max(0, Stat(attacker, ItemContent.IgnoranceStack));
                state.Stacks += applications * perApplication;
                CosmicFx.Spawn("ignorance", Above(victim), 0f, 1f, 1f, White, 12f, false, -1f);
                Once(0, "우주 속성 확인: 무지 부여 (" + state.Stacks + "중첩)");
                if (state.Stacks >= VoidStacks)
                {
                    Void(attacker, victim, state);
                    return;
                }
                state.SlowUntil = now + SlowTime;
                state.BurstAt = now + BurstTime;
                state.DotUntil = now + DotTime;
                if (state.NextTick < now)
                    state.NextTick = now + TickInterval(attacker);
                SetSlow(victim, state, Math.Min(SlowCap, state.Stacks * SlowPerStack));
            }
            catch (Exception e)
            {
                Fail("무지 부여", e);
            }
        }

        private static float IgnoranceBonus(object attacker)
        {
            return 1f + Math.Max(0, Stat(attacker, ItemContent.IgnoranceDamage)) / 100f;
        }

        private static float TickInterval(object attacker)
        {
            return DotInterval / (1f + Math.Max(0, Stat(attacker, ItemContent.IgnoranceSpeed)) / 100f);
        }

        private static Vector3 Above(object unit)
        {
            Vector3 p = Position(unit);
            return new Vector3(p.x, p.y + 0.8f, p.z);
        }

        private static void SetSlow(object victim, Ignorance state, float slow)
        {
            if (Math.Abs(slow - state.Slowed) < 0.001f || Reflect.IsNull(victim))
                return;
            object current = Reflect.Get(victim, "moveSpeedMultiplier");
            if (current is float)
                Reflect.Set(victim, "NetworkmoveSpeedMultiplier", (float)current - (slow - state.Slowed));
            state.Slowed = slow;
        }

        private static void ClearIgnorance(object victim, Ignorance state)
        {
            SetSlow(victim, state, 0f);
            state.Stacks = 0;
            state.SlowUntil = state.BurstAt = state.DotUntil = 0f;
        }

        private static void TickIgnorance()
        {
            foreach (KeyValuePair<object, Ignorance> pair in Victims.ToList())
            {
                object victim = pair.Key;
                Ignorance state = pair.Value;
                if (Dead(victim))
                {
                    if (!Reflect.IsNull(victim))
                        SetSlow(victim, state, 0f);
                    Victims.Remove(victim);
                    continue;
                }
                if (state.Stacks <= 0)
                {
                    if (now - state.LastApply > ApplyCooldown + 1f)
                        Victims.Remove(victim); // 재사용 대기 기록만 남은 대상은 정리
                    continue;
                }
                object attacker = state.Attacker;
                if (Reflect.IsNull(attacker))
                {
                    ClearIgnorance(victim, state);
                    continue;
                }
                int stacks = Math.Min(VoidStacks, state.Stacks);
                if (state.DotUntil > 0f && now >= state.NextTick && now <= state.DotUntil + 0.01f)
                {
                    state.NextTick = now + TickInterval(attacker);
                    float percent = Stat(attacker, ItemContent.IgnoranceUp) > 0 ? DotPercentUp : DotPercent;
                    Hurt(attacker, victim, Power(attacker, percent) * stacks * IgnoranceBonus(attacker), IgnoranceId);
                }
                if (state.BurstAt > 0f && now >= state.BurstAt)
                {
                    state.BurstAt = 0f;
                    Hurt(attacker, victim, Power(attacker, BurstPercent) * stacks * IgnoranceBonus(attacker), IgnoranceId);
                }
                if (now >= state.SlowUntil)
                    ClearIgnorance(victim, state);
            }
        }

        // ── 허무 ─────────────────────────────────────────────────────────────
        private static void Void(object attacker, object victim, Ignorance state)
        {
            ClearIgnorance(victim, state);
            float percent = Stat(attacker, ItemContent.VoidUp) > 0 ? VoidPercentUp : VoidPercent;
            float amount = Power(attacker, percent) * (1f + Math.Max(0, Stat(attacker, ItemContent.VoidBonus)) / 100f);
            CosmicFx.Spawn("void", Above(victim), 0f, 1.5f, 1.5f, White, 14f, false, -1f);
            Hurt(attacker, victim, amount, VoidId);
            if (!Dead(victim))
            {
                try { Reflect.Call(victim, null, "StartStun", VoidStun); }
                catch (Exception) { }
            }
            Once(1, "우주 속성 확인: 허무 발생 (피해 " + amount.ToString("0") + ")");
        }

        private static void Hurt(object attacker, object victim, float amount, string id)
        {
            if (amount <= 0f || Dead(victim) || Reflect.IsNull(attacker))
                return;
            ItemEffects.DealDamage(attacker, victim, amount, id, IsDebuff(id) ? 2 : 1);
        }

        // ── 무기 적중에 반응하는 아티팩트 ─────────────────────────────────────
        /// <summary>attacker 의 무기 공격이 victim 에게 적중함.</summary>
        public static void OnWeaponHit(object attacker, object victim)
        {
            try
            {
                // 악보 : '일식' — 무기 공격 시 정해진 양의 우주 속성 피해
                int score = Stat(attacker, ItemContent.ScoreCosmic);
                if (score > 0 && !Dead(victim))
                    Hurt(attacker, victim, score, ScoreId);
                Charges charges;
                if (!Players.TryGetValue(attacker, out charges))
                    return;
                // 참월 — 일반 공격에 처음 맞은 적을 중심축으로, 충전된 참월이 발동
                if (charges.BasicPending)
                {
                    charges.BasicPending = false;
                    for (int i = 0; i < 2; i++)
                        if (charges.Time[i] >= ChargeTime && Stat(attacker, PowerStats[i]) > 0)
                            Fire(attacker, charges, i, victim, true);
                }
                // 종말기수 — 공격할 때마다 보유한 섬멸 '청'·명멸 '적'을 충전과 관계없이 불러옵니다(한 번 휘두를 때 한 번)
                if (LanceAddon.KindOf(attacker) == LanceAddon.Doom && now - charges.LastCall >= 0.15f)
                {
                    charges.LastCall = now;
                    for (int i = 2; i < 4; i++)
                        if (Stat(attacker, PowerStats[i]) > 0)
                            Fire(attacker, charges, i, victim, false);
                }
                // 일월광풍 — 무기 공격 시 충전된 태극 발동형 아티팩트를 바로 발동
                if (Stat(attacker, ItemContent.TaegeukOnHit) > 0)
                    FireCharged(attacker, victim);
            }
            catch (Exception e)
            {
                Fail("무기 적중", e);
            }
        }

        // ── 태극 발동형 ───────────────────────────────────────────────────────
        // 0 참월 '흑'(가로 베기), 1 참월 '백'(세로 베기), 2 섬멸 '청'(물빛 창), 3 명멸 '적'(불창)
        private static readonly string[] PowerStats = { ItemContent.SlashBlack, ItemContent.SlashWhite, ItemContent.SpearBlue, ItemContent.SpearRed };
        private static readonly string[] CountStats = { null, null, ItemContent.SpearBlueCount, ItemContent.SpearRedCount };

        /// <summary>태극 아티팩트를 가질 수 있는 캐릭터로 기억해 둡니다(충전 계산 대상).</summary>
        public static void Track(object avatar)
        {
            if (Reflect.IsNull(avatar) || Players.ContainsKey(avatar))
                return;
            if (avatar.GetType().Name != "PlayerAvatar" || !PowerStats.Any(key => Stat(avatar, key) > 0))
                return;
            Players[avatar] = new Charges();
            CosmicFx.LearnLayer(avatar);
            EnsureTicker();
        }

        private static void TickCharges(float delta)
        {
            foreach (KeyValuePair<object, Charges> pair in Players.ToList())
            {
                object avatar = pair.Key;
                Charges charges = pair.Value;
                if (Reflect.IsNull(avatar))
                {
                    Players.Remove(avatar);
                    continue;
                }
                if (Dead(avatar))
                    continue;
                for (int i = 0; i < 4; i++)
                    if (charges.Time[i] < ChargeTime)
                        charges.Time[i] += delta;
                // 창은 주변에 적이 있으면 스스로 발동합니다(0.2초마다 확인).
                charges.Check -= delta;
                if (charges.Check > 0f)
                    continue;
                charges.Check = 0.2f;
                ShowCharges(avatar, charges);
                for (int i = 2; i < 4; i++)
                {
                    if (charges.Time[i] < ChargeTime || Stat(avatar, PowerStats[i]) <= 0)
                        continue;
                    object target = Nearest(avatar, Position(avatar), SpearRange, null);
                    if (target != null)
                        Fire(avatar, charges, i, target, true);
                }
            }
        }

        // 충전 상태 표시: 보유한 태극 아티팩트마다 상태 아이콘을 띄우고, 남은 충전 시간을 아이콘의 가림막으로 보여 줍니다.
        private static readonly string[] HudNames = { "ChamwolBlack", "ChamwolWhite", "AnnihilationBlue", "FlickerRed" };

        private static void ShowCharges(object avatar, Charges charges)
        {
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    string name = "FFC_Taegeuk_" + i;
                    bool owned = Stat(avatar, PowerStats[i]) > 0;
                    if (!owned)
                    {
                        if (charges.Hud[i])
                        {
                            Reflect.Call(avatar, null, "DestroyEffectHUD", name);
                            charges.Hud[i] = false;
                        }
                        continue;
                    }
                    if (!charges.Hud[i])
                    {
                        string entity = StatusHud.Ensure("FFC_TAEGEUK_" + i, "Item_FFC_" + HudNames[i] + "_Name", "EffectHUD_FFC_Taegeuk_FlavorText", "taegeuk_" + i + ".png");
                        Reflect.Call(avatar, null, "CreateEffectHUD", entity, name);
                        Reflect.Call(avatar, null, "SetEffectHUDValue", name, "");
                        charges.Hud[i] = true;
                        charges.Ready[i] = false;
                    }
                    bool ready = charges.Time[i] >= ChargeTime;
                    Reflect.Call(avatar, null, "SetEffectHUDFillAmount", name, ready ? 0f : 1f - charges.Time[i] / ChargeTime);
                    if (ready && !charges.Ready[i])
                        Reflect.Call(avatar, null, "SetEffectHUDFlash", name); // 충전 완료 번쩍
                    charges.Ready[i] = ready;
                }
            }
            catch (Exception e)
            {
                Fail("충전 표시", e);
            }
        }

        // 일반 공격이 나감(서버): 이 공격이 적에게 처음 닿을 때 충전된 참월이 발동합니다(OnWeaponHit).
        private static void BasicAttackPostfix(object __instance)
        {
            try
            {
                if (Players.Count == 0 && serverActive != null && !(bool)serverActive.GetValue(null, null))
                    return;
                object controller = Reflect.Get(__instance, "Networkowner");
                object avatar = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "unitAvatar");
                if (Reflect.IsNull(avatar) || (Stat(avatar, ItemContent.SlashBlack) <= 0 && Stat(avatar, ItemContent.SlashWhite) <= 0))
                    return;
                Track(avatar);
                Charges charges;
                if (Players.TryGetValue(avatar, out charges))
                    charges.BasicPending = true; // 이 공격에 처음 맞는 적이 참월의 중심축이 됩니다
            }
            catch (Exception e)
            {
                Fail("참월", e);
            }
        }

        // 충전이 끝난 태극 아티팩트를 조건과 관계없이 지금 발동합니다(일월광풍).
        private static void FireCharged(object avatar, object victim)
        {
            Charges charges;
            if (!Players.TryGetValue(avatar, out charges))
                return;
            for (int i = 0; i < 4; i++)
                if (charges.Time[i] >= ChargeTime && Stat(avatar, PowerStats[i]) > 0)
                    Fire(avatar, charges, i, victim, true);
        }

        private static void Fire(object avatar, Charges charges, int kind, object target, bool natural)
        {
            if (natural)
                charges.Time[kind] = 0f;
            int repeats = 1 + Math.Max(0, Stat(avatar, ItemContent.TaegeukExtra));
            Vector3 center = Dead(target) ? Position(avatar) : Position(target); // 참월의 중심축: 맞은 적(없으면 캐릭터)
            for (int r = 0; r < repeats; r++)
            {
                int index = r;
                if (index == 0)
                    Activate(avatar, kind, target, center);
                else
                    Later(0.3f * index, () => { if (!Dead(avatar)) Activate(avatar, kind, null, center); });
            }
            Once(3, "우주 속성 확인: 태극 발동 (" + new[] { "참월 흑", "참월 백", "섬멸 청", "명멸 적" }[kind] + ", " + repeats + "회)");

            // 무구정광대다라니경(3레벨): 하나가 발동하면 나머지도 충전과 관계없이 차례로 발동합니다.
            if (natural && !firingChain && Stat(avatar, ItemContent.TaegeukChain) > 0)
            {
                firingChain = true;
                try
                {
                    int order = 1;
                    for (int i = 0; i < 4; i++)
                    {
                        if (i == kind || Stat(avatar, PowerStats[i]) <= 0)
                            continue;
                        int next = i;
                        Later(0.2f * order++, () => { if (!Dead(avatar)) Fire(avatar, charges, next, null, false); });
                    }
                }
                finally
                {
                    firingChain = false;
                }
            }
        }

        private static float TaegeukAmount(object avatar, int kind)
        {
            float amount = FlatDamage + Power(avatar, Stat(avatar, PowerStats[kind]));
            return amount * (1f + Math.Max(0, Stat(avatar, ItemContent.TaegeukDamage)) / 100f);
        }

        private static void Activate(object avatar, int kind, object target, Vector3 center)
        {
            if (kind < 2)
                Slash(avatar, kind == 1, center);
            else
            {
                int count = Math.Max(1, Stat(avatar, CountStats[kind]));
                for (int i = 0; i < count; i++)
                {
                    object chosen = i == 0 ? target : null;
                    if (i == 0)
                        Spear(avatar, kind, chosen);
                    else
                        Later(0.25f * i, () => { if (!Dead(avatar)) Spear(avatar, kind, null); });
                }
            }
        }

        // 참월(콤보 데스폴트를 본뜸): 중심축을 지나는 면도날 같은 선이 화면 끝에서 끝까지 즉시 그어지고(충격 고리와 함께),
        // 잠깐 뒤 그 선을 따라 공간이 갈라집니다: 타마다 흰 바늘이 엇갈려 번쩍이며 선 주변의 적 모두에게 피해가 들어가고,
        // 선은 가는 점선으로 끊기며 사라집니다. 흑은 가로(검은 심 + 흰 테), 백은 세로(흰 심 + 푸른 테).
        private static readonly float[] NeedleTilt = { -14f, 14f, 0f };

        private static void Slash(object avatar, bool vertical, Vector3 center)
        {
            float amount = TaegeukAmount(avatar, vertical ? 1 : 0) / SlashHits;
            string id = TaegeukPrefix + (vertical ? "SlashWhite" : "SlashBlack");
            float angle = vertical ? 90f : 0f;
            var middle = new Vector3(center.x, center.y + 0.5f, center.z);
            const float stretch = 7f; // 그림은 가로 6칸: 화면을 가로지르도록 늘입니다(굵기는 그대로 가늘게)
            for (int hit = 0; hit < SlashHits; hit++)
            {
                float tilt = NeedleTilt[hit % NeedleTilt.Length];
                Later(SlashDelay + SlashInterval * hit, () =>
                {
                    if (Dead(avatar))
                        return;
                    CosmicFx.Spawn("needle", middle, angle + tilt, stretch, 1.5f, White, 25f, false, -1f);
                    foreach (object creature in Hostiles(avatar).ToList())
                    {
                        Vector3 p = Position(creature);
                        float along = vertical ? p.y - center.y : p.x - center.x;
                        float across = vertical ? p.x - center.x : p.y - center.y;
                        if (Math.Abs(along) <= SlashReach && Math.Abs(across) <= SlashHalfWidth)
                            Hurt(avatar, creature, amount, id);
                    }
                });
            }
            // 선: 처음 5프레임(또렷한 선)이 SlashDelay 동안 보이고, 나머지가 파열이 끝날 때까지 이어집니다.
            float total = SlashDelay + SlashInterval * SlashHits;
            CosmicFx.Spawn(vertical ? "cut_white" : "cut_black", middle, angle, stretch, 1f, White, 12f / total, false, -1f);
            CosmicFx.Spawn("ring", middle, 0f, 1.5f, 1.5f, White, 25f, false, -1f);
        }

        // 섬멸·명멸: 무녀의 예언의 볼루스파처럼, 창이 캐릭터 뒤에서 나타나 조준(커서) 방향으로 화면을 꿰뚫고 지나갑니다.
        // 조준 방향 쪽에 적이 있으면 가장 가까운 적을 향하고, 지나가는 길 위의 적 모두에게 피해를 줍니다.
        private const float SpearReach = 10f, SpearSpeed = 46f, SpearHalfWidth = 1.2f;
        private static readonly Color BlueTrail = new Color(0.72f, 0.88f, 1f, 1f), RedTrail = new Color(1f, 0.72f, 0.4f, 1f);

        private static void Spear(object avatar, int kind, object target)
        {
            Vector3 origin = Position(avatar);
            float dx = 1f, dy = 0f;
            Component controller = ((Component)avatar).GetComponent(Reflect.GameType("WeaponControllerSimple"));
            object aimed = Reflect.IsNull(controller) ? null : Reflect.Get(controller, "aimedPositionClientside");
            if (aimed is Vector3)
            {
                dx = ((Vector3)aimed).x - origin.x;
                dy = ((Vector3)aimed).y - origin.y;
            }
            // 조준 방향 쪽(앞쪽)의 가장 가까운 적을 겨눕니다.
            object focus = null;
            float best = 15f * 15f;
            foreach (object creature in Hostiles(avatar))
            {
                Vector3 p = Position(creature);
                float ex = p.x - origin.x, ey = p.y - origin.y, d = ex * ex + ey * ey;
                if (d <= best && ex * dx + ey * dy >= 0f)
                {
                    best = d;
                    focus = creature;
                }
            }
            if (focus != null)
            {
                Vector3 p = Position(focus);
                dx = p.x - origin.x;
                dy = p.y - origin.y;
            }
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.01f)
            {
                dx = 1f;
                dy = 0f;
                length = 1f;
            }
            float nx = dx / length, ny = dy / length;
            float angle = (float)(Math.Atan2(ny, nx) * 180.0 / Math.PI);
            float flight = 2f * SpearReach / SpearSpeed;

            var start = new Vector3(origin.x - nx * SpearReach, origin.y + 0.5f - ny * SpearReach, origin.z);
            FxPlayer fx = CosmicFx.Spawn(kind == 2 ? "spear_blue" : "spear_red", start, angle, 1f, 1f, White, 20f, true, flight);
            if (fx != null)
            {
                fx.velocityX = nx * SpearSpeed;
                fx.velocityY = ny * SpearSpeed;
            }
            // 지나간 자리의 잔상: 길 한가운데에, 창이 반쯤 지났을 때
            Color trail = kind == 2 ? BlueTrail : RedTrail;
            var middle = new Vector3(origin.x, origin.y + 0.5f, origin.z);
            Later(flight * 0.5f, () => CosmicFx.Spawn("spear_trail", middle, angle, 2f, 1f, trail, 12f, false, -1f));

            float amount = TaegeukAmount(avatar, kind);
            string id = TaegeukPrefix + (kind == 2 ? "SpearBlue" : "SpearRed");
            foreach (object creature in Hostiles(avatar).ToList())
            {
                Vector3 p = Position(creature);
                float ex = p.x - origin.x, ey = p.y - origin.y;
                float along = ex * nx + ey * ny, across = Math.Abs(ex * ny - ey * nx);
                if (along < -SpearReach || along > SpearReach || across > SpearHalfWidth)
                    continue;
                object victim = creature;
                Later((along + SpearReach) / SpearSpeed, () => Hurt(avatar, victim, amount, id));
            }
        }

        // ── 적 찾기 ───────────────────────────────────────────────────────────
        private static IEnumerable<object> Hostiles(object avatar)
        {
            object manager = Reflect.GetStatic(Reflect.GameType("CombatManager"), "Instance");
            var all = Reflect.IsNull(manager) ? null : Reflect.Get(manager, "AllCreatures") as IEnumerable;
            if (all == null)
                yield break;
            object faction = Reflect.Get(avatar, "faction");
            foreach (object creature in all.Cast<object>().ToList())
            {
                if (Dead(creature) || ReferenceEquals(creature, avatar))
                    continue;
                if (Equals(Reflect.Get(creature, "faction"), faction))
                    continue; // 같은 편(동료 포함)
                object canBeTarget = Reflect.Get(creature, "canBeTarget");
                if (canBeTarget != null)
                {
                    try { if ((bool)Reflect.Call(canBeTarget, null, "IsFalse")) continue; }
                    catch (Exception) { }
                }
                yield return creature;
            }
        }

        private static object Nearest(object avatar, Vector3 from, float range, object except)
        {
            object best = null;
            float bestDistance = range * range;
            foreach (object creature in Hostiles(avatar))
            {
                if (ReferenceEquals(creature, except))
                    continue;
                Vector3 p = Position(creature);
                float dx = p.x - from.x, dy = p.y - from.y, d = dx * dx + dy * dy;
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = creature;
                }
            }
            return best;
        }
    }

    /// <summary>우주 속성의 시간 처리(무지의 지속 피해, 태극의 충전, 예약된 발동)를 매 프레임 돌립니다.</summary>
    public class CosmicTicker : MonoBehaviour
    {
        private void Update()
        {
            Cosmic.Tick(Time.deltaTime);
        }
    }
}

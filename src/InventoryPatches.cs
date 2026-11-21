using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 인벤토리 확장(+6 / +12).
    ///
    /// 게임은 인벤토리 칸 수(GridInventory.CurrentInventoryStorage, 기본 24)를 항상 AddStorage 로
    /// '더하고 빼는' 식으로만 바꿉니다(레벨업, 재능 특전, 농장 능력 등). 이 모드도 같은 함수로
    /// 서버(호스트)에서 보너스를 더하고, 인벤토리별로 더해 둔 양을 기억해 설정이 바뀌면 그 차이만큼만 조정합니다.
    /// 줄일 때 넘치는 아이템은 게임의 AddStorage 가 빈칸으로 옮기거나 바닥에 떨어뜨립니다.
    /// </summary>
    internal static class InventoryPatches
    {
        private sealed class Bonus { public int Value; }
        private static readonly ConditionalWeakTable<object, Bonus> Bonuses = new ConditionalWeakTable<object, Bonus>();

        private static FieldInfo avatarInventory;
        private static MethodInfo addStorage;
        private static PropertyInfo isServer;
        private static FieldInfo multiplayerList;
        private static PropertyInfo spawnerAvatar;
        private static bool ready;
        private static bool disabled;
        private static bool loggedFailure;

        public static void Apply()
        {
            disabled = false;
            Type avatar = GameReflection.FindType("PlayerAvatar");
            Type inventory = GameReflection.FindType("GridInventory");
            Type spawner = GameReflection.FindType("PlayerSpawner");

            avatarInventory = avatar != null ? avatar.GetField("Inventory", BindingFlags.Public | BindingFlags.Instance) : null;
            if (inventory != null)
            {
                addStorage = inventory.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "AddStorage"
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(short));
                isServer = inventory.GetProperty("isServer", BindingFlags.Public | BindingFlags.Instance);
            }
            if (spawner != null)
            {
                multiplayerList = spawner.GetField("MultiplayerList", GameReflection.Any);
                spawnerAvatar = spawner.GetProperty("PlayerAvatar", GameReflection.Any);
            }

            ready = avatarInventory != null && addStorage != null && isServer != null;
            if (!ready)
            {
                Log.Warn("GridInventory.AddStorage 를 찾지 못해 인벤토리 확장을 쓸 수 없습니다.");
                return;
            }

            Settings.InventoryBonusChanged = ApplyToAllPlayers;
            Log.Info("인벤토리 확장 준비됨 (설정 +" + Settings.InventoryBonus + ")");
        }

        /// <summary>모드가 내려갈 때 더해 둔 칸을 걷어냅니다.</summary>
        public static void Remove()
        {
            disabled = true;
            Settings.InventoryBonusChanged = null;
            ApplyToAllPlayers();
            ready = false;
        }

        /// <summary>플레이어가 스폰될 때(서버) 불립니다.</summary>
        public static void OnPlayerSpawned(object avatar)
        {
            Ensure(avatar);
        }

        public static void ApplyToAllPlayers()
        {
            if (!ready || multiplayerList == null || spawnerAvatar == null)
                return;
            try
            {
                var spawners = multiplayerList.GetValue(null) as IEnumerable;
                if (spawners == null)
                    return;
                foreach (object spawner in spawners.Cast<object>().ToArray())
                {
                    if (!GameReflection.IsNull(spawner))
                        Ensure(spawnerAvatar.GetValue(spawner, null));
                }
            }
            catch (Exception e)
            {
                LogFailureOnce(e);
            }
        }

        private static void Ensure(object avatar)
        {
            if (!ready || GameReflection.IsNull(avatar))
                return;
            try
            {
                object inventory = avatarInventory.GetValue(avatar);
                // 칸 수는 서버가 정하고 동기화합니다. 참가자 쪽에서는 건드리지 않습니다.
                if (GameReflection.IsNull(inventory) || !(bool)isServer.GetValue(inventory, null))
                    return;

                Bonus bonus = Bonuses.GetOrCreateValue(inventory);
                int wanted = disabled ? 0 : Settings.InventoryBonus;
                int delta = wanted - bonus.Value;
                if (delta == 0)
                    return;

                addStorage.Invoke(inventory, new object[] { (short)delta });
                bonus.Value = wanted;
            }
            catch (Exception e)
            {
                LogFailureOnce(e);
            }
        }

        private static void LogFailureOnce(Exception e)
        {
            if (loggedFailure)
                return;
            loggedFailure = true;
            Log.Error("인벤토리 확장 적용 중 오류(이후 같은 오류는 기록하지 않습니다): " + e);
        }
    }
}

using System;
using HarmonyLib;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 모드 진입점. metadata.json 의 entryClass 가 이 클래스를 가리킵니다.
    /// </summary>
    public sealed class Entry : WorkshopModBase
    {
        public const string ModId = "aedws.limit-open-sepiria";

        private static Harmony harmony;

        // 프로세스당 한 번 호출됩니다(월드 입장 시).
        protected override void OnModLoaded()
        {
            base.OnModLoaded();

            // 옵션 등록이 실패해도(게임 업데이트로 옵션 API가 바뀐 경우 등) 패치는 기본값으로 동작합니다.
            try
            {
                Settings.Bind(this);
            }
            catch (Exception e)
            {
                Log.Warn("옵션 화면 등록에 실패해 기본값(모두 켜짐)으로 동작합니다: " + e);
            }

            if (harmony != null)
                return;

            harmony = new Harmony(ModId);
            try
            {
                FountainPatches.Apply(harmony);
            }
            catch (Exception e)
            {
                Log.Error("소원 분수 패치 적용 중 오류: " + e);
            }

            try
            {
                ProgressionPatches.Apply(harmony);
                HorayModAPI.OnPlayerAvatarSpawnedServerside += HandlePlayerSpawned;
            }
            catch (Exception e)
            {
                Log.Error("재능·과일 꼬치 패치 적용 중 오류: " + e);
            }

            Log.Info("로드됨 v" + metadata.modVersion);
        }

        private static void HandlePlayerSpawned(PlayerAvatar player)
        {
            ProgressionPatches.OnPlayerSpawned(player);
        }

        protected override void OnModUnloaded()
        {
            try
            {
                HorayModAPI.OnPlayerAvatarSpawnedServerside -= HandlePlayerSpawned;
                ProgressionPatches.Remove();
                if (harmony != null)
                    harmony.UnpatchAll(ModId);
            }
            catch (Exception e)
            {
                Log.Warn("패치 해제 중 오류: " + e);
            }

            harmony = null;
            base.OnModUnloaded();
        }
    }
}

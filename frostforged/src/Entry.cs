using System;
using HarmonyLib;

namespace FrostforgedCrescent
{
    /// <summary>모드 진입점. metadata.json 의 entryClass 가 이 클래스를 가리킵니다.</summary>
    public sealed class Entry : WorkshopModBase
    {
        public const string ModId = "aedws.frostforged-crescent";

        private static Harmony harmony;
        internal static string packageRoot;

        protected override void OnModLoaded()
        {
            base.OnModLoaded();
            packageRoot = Context != null ? Context.PackageRoot : null;

            HorayModAPI.OnLoadWeaponDatabase += RegisterWeapon;
            ItemContent.Hook(packageRoot);

            if (harmony == null)
            {
                harmony = new Harmony(ModId);
                try
                {
                    ScabbardPatch.Apply(harmony);
                    MotionLock.Apply(harmony);
                    CharmLink.Apply(harmony);
                    FinalCombo.Apply(harmony);
                    ItemEffects.Apply(harmony);
                    DarkCloudBoost.Apply(harmony);
                    LanceMotion.Apply(harmony);
                    WeakSpear.Apply(harmony);
                    LanceFx.Apply(harmony);
                }
                catch (Exception e)
                {
                    Log.Error("패치 적용 중 오류: " + e);
                }
            }

            Log.Info("로드됨 v" + metadata.modVersion);
        }

        protected override void OnModUnloaded()
        {
            HorayModAPI.OnLoadWeaponDatabase -= RegisterWeapon;
            ItemContent.Unhook();
            try
            {
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

        private static void RegisterWeapon()
        {
            WeaponFactory.Register(packageRoot ?? "");
            LanceFactory.Register(packageRoot ?? "");
        }
    }
}

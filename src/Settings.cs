using System;
using System.Runtime.CompilerServices;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 게임 옵션 화면의 모드 페이지에 토글을 등록하고 현재 값을 보관합니다.
    /// 값은 패치가 실행될 때마다 읽으므로, 토글을 바꾸면 재시작 없이 다음에 분수를 열 때 바로 반영됩니다.
    /// </summary>
    internal static class Settings
    {
        public const string KeyUnlimitedCapacity = "unlimited_capacity";
        public const string KeyAllItems = "all_items";
        public const string KeyIncludeOtherTypes = "include_other_types";
        public const string KeyIncludeHidden = "include_hidden";

        // 옵션 API를 쓸 수 없을 때의 기본값이기도 합니다.
        public static volatile bool UnlimitedCapacity = true;
        public static volatile bool AllItems = true;
        public static volatile bool IncludeOtherTypes = false;
        public static volatile bool IncludeHidden = false;

        // 옵션 API 타입을 참조하는 코드를 이 메서드에 가둬, 타입이 맞지 않으면 호출부의 try/catch 로 떨어지게 합니다.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Bind(WorkshopModBase mod)
        {
            WorkshopModContext context = mod.Context;
            ModOptions options = context != null ? context.Options : null;
            if (options == null)
            {
                Log.Warn("모드 옵션 저장소를 얻지 못해 기본값(모두 켜짐)으로 동작합니다.");
                return;
            }

            ModToggleOption capacity = options.AddToggle(
                KeyUnlimitedCapacity, true,
                "소원 분수 용량 제한 해제 / Unlimited fountain capacity",
                value => { UnlimitedCapacity = value; Save(options); });

            ModToggleOption allItems = options.AddToggle(
                KeyAllItems, true,
                "소원 분수에서 모든 아이템 선택 / All items selectable in fountain",
                value => { AllItems = value; Save(options); });

            ModToggleOption otherTypes = options.AddToggle(
                KeyIncludeOtherTypes, false,
                "└ 아티팩트 외 종류(포션·석판 등)도 포함 / Include non-artifact items",
                value => { IncludeOtherTypes = value; Save(options); });

            ModToggleOption includeHidden = options.AddToggle(
                KeyIncludeHidden, false,
                "└ 숨김·비활성·테스트용 아이템도 포함 / Include hidden, disabled & test items",
                value => { IncludeHidden = value; Save(options); });

            UnlimitedCapacity = capacity != null ? capacity.Value : options.GetBool(KeyUnlimitedCapacity, true);
            AllItems = allItems != null ? allItems.Value : options.GetBool(KeyAllItems, true);
            IncludeOtherTypes = otherTypes != null ? otherTypes.Value : options.GetBool(KeyIncludeOtherTypes, false);
            IncludeHidden = includeHidden != null ? includeHidden.Value : options.GetBool(KeyIncludeHidden, false);
        }

        private static void Save(ModOptions options)
        {
            try
            {
                options.Save();
            }
            catch (Exception e)
            {
                Log.Warn("옵션 저장 실패: " + e.Message);
            }
        }
    }
}

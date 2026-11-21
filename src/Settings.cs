using System;
using System.Runtime.CompilerServices;

namespace LimitOpenSepiria
{
    /// <summary>
    /// 모드 설정값. 게임 옵션 화면의 모드 페이지에 등록되고, 소원 분수 화면의 버튼으로도 바꿀 수 있습니다.
    /// 값은 패치가 실행될 때마다 읽으므로 재시작 없이 반영됩니다.
    /// </summary>
    internal static class Settings
    {
        public const int TalentPointLimit = 140;
        public const int FruitSkewerLimit = 21;

        private const string KeyUnlimitedCapacity = "unlimited_capacity";
        private const string KeyAllItems = "all_items";
        private const string KeyIncludeOtherTypes = "include_other_types";
        private const string KeyIncludeHidden = "include_hidden";
        private const string KeyTalentPoints = "talent_points";
        private const string KeyFruitSkewer = "fruit_skewer";
        private const string KeyUnlimitedTalentLevel = "unlimited_talent_level";

        // 옵션 API를 쓸 수 없을 때의 기본값이기도 합니다.
        public static volatile bool UnlimitedCapacity = true;
        public static volatile bool AllItems = true;
        public static volatile bool IncludeOtherTypes = false;
        public static volatile bool IncludeHidden = false;

        /// <summary>재능 포인트 최대치. 게임이 준 최대치보다 작으면(0 포함) 게임 값을 그대로 씁니다.</summary>
        public static volatile int TalentPoints = TalentPointLimit;

        /// <summary>과일 꼬치 최대 개수. 게임이 준 개수보다 작으면(0 포함) 게임 값을 그대로 씁니다.</summary>
        public static volatile int FruitSkewer = FruitSkewerLimit;

        /// <summary>재능 하나에 찍을 수 있는 레벨 상한(게임 기본 20)을 해제합니다.</summary>
        public static volatile bool UnlimitedTalentLevel = false;

        /// <summary>재능 포인트 설정이 바뀌면 호출됩니다.</summary>
        public static Action TalentPointsChanged;

        /// <summary>재능 레벨 상한 설정이 바뀌면 호출됩니다.</summary>
        public static Action TalentLevelCapChanged;

        private static Action<int> setTalentPoints;
        private static Action<int> setFruitSkewer;
        private static Action<bool> setUnlimitedTalentLevel;

        private static Action<bool> setUnlimitedCapacity;
        private static Action<bool> setAllItems;

        // 옵션 API 타입을 참조하는 코드를 이 메서드에 가둬, 타입이 맞지 않으면 호출부의 try/catch 로 떨어지게 합니다.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Bind(WorkshopModBase mod)
        {
            WorkshopModContext context = mod.Context;
            ModOptions options = context != null ? context.Options : null;
            if (options == null)
            {
                Log.Warn("모드 옵션 저장소를 얻지 못해 기본값으로 동작합니다.");
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

            ModSliderOption talent = options.AddSlider(
                KeyTalentPoints, 0f, TalentPointLimit, TalentPointLimit, 1f,
                "재능 포인트 최대치 (0 = 게임 기본) / Max talent points (0 = game default)",
                value =>
                {
                    TalentPoints = ToInt(value, TalentPointLimit);
                    Save(options);
                    Action changed = TalentPointsChanged;
                    if (changed != null)
                        changed();
                });

            ModSliderOption fruit = options.AddSlider(
                KeyFruitSkewer, 0f, FruitSkewerLimit, FruitSkewerLimit, 1f,
                "과일 꼬치 최대 개수 (0 = 게임 기본) / Max fruit skewer picks (0 = game default)",
                value => { FruitSkewer = ToInt(value, FruitSkewerLimit); Save(options); });

            ModToggleOption talentLevel = options.AddToggle(
                KeyUnlimitedTalentLevel, false,
                "재능별 레벨 상한(20) 해제 / Remove per-talent level cap (20)",
                value =>
                {
                    UnlimitedTalentLevel = value;
                    Save(options);
                    Action changed = TalentLevelCapChanged;
                    if (changed != null)
                        changed();
                });

            UnlimitedTalentLevel = talentLevel.Value;
            setUnlimitedTalentLevel = value => talentLevel.Value = value;
            setTalentPoints = value => talent.Value = value;
            setFruitSkewer = value => fruit.Value = value;

            UnlimitedCapacity = capacity.Value;
            AllItems = allItems.Value;
            IncludeOtherTypes = otherTypes.Value;
            IncludeHidden = includeHidden.Value;
            TalentPoints = ToInt(talent.Value, TalentPointLimit);
            FruitSkewer = ToInt(fruit.Value, FruitSkewerLimit);

            // 값을 옵션 항목에 쓰면 위의 onChanged 가 불려 필드와 저장이 함께 갱신됩니다.
            setUnlimitedCapacity = value => capacity.Value = value;
            setAllItems = value => allItems.Value = value;
        }

        public static void SetUnlimitedCapacity(bool value)
        {
            if (setUnlimitedCapacity != null) setUnlimitedCapacity(value);
            else UnlimitedCapacity = value;
        }

        public static void SetAllItems(bool value)
        {
            if (setAllItems != null) setAllItems(value);
            else AllItems = value;
        }

        public static void SetTalentPoints(int value)
        {
            value = ToInt(value, TalentPointLimit);
            if (setTalentPoints != null)
            {
                setTalentPoints(value);
                return;
            }
            TalentPoints = value;
            Action changed = TalentPointsChanged;
            if (changed != null)
                changed();
        }

        public static void SetFruitSkewer(int value)
        {
            value = ToInt(value, FruitSkewerLimit);
            if (setFruitSkewer != null) setFruitSkewer(value);
            else FruitSkewer = value;
        }

        public static void SetUnlimitedTalentLevel(bool value)
        {
            if (setUnlimitedTalentLevel != null)
            {
                setUnlimitedTalentLevel(value);
                return;
            }
            UnlimitedTalentLevel = value;
            Action changed = TalentLevelCapChanged;
            if (changed != null)
                changed();
        }

        private static int ToInt(float value, int limit)
        {
            int rounded = (int)Math.Round(value);
            return rounded < 0 ? 0 : (rounded > limit ? limit : rounded);
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

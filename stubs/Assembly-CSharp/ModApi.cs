// 컴파일 전용 스텁입니다. 공식 모딩 가이드(team-horay/sephiria_modding, API 레퍼런스 1.1.0)에
// 공개된 시그니처 중 이 모드가 쓰는 것만 옮겼습니다. 게임 코드는 들어 있지 않으며,
// 실행 시에는 게임의 실제 Assembly-CSharp.dll이 쓰입니다.
using System;

public class HorayModBase
{
    public struct Metadata
    {
        public string modName;
        public string modVersion;
        public string modAuthor;
    }

    public Metadata metadata;

    public void LoadMod(Metadata metadata) { }
    protected virtual void OnModLoaded() { }
    public void UnloadMod() { }
    protected virtual void OnModUnloaded() { }
}

public abstract class WorkshopModBase : HorayModBase
{
    public WorkshopModContext Context { get { return null; } }
}

public sealed class WorkshopModContext
{
    public string PackageRoot { get { return null; } }
    public ulong FileId { get { return 0; } }
    public ModOptions Options { get { return null; } }
}

public sealed class ModOptions
{
    public string ModId { get { return null; } }
    public bool IsAvailable { get { return false; } }
    public bool GetBool(string key, bool fallback = false) { return fallback; }
    public void SetBool(string key, bool value) { }
    public bool Save() { return false; }
    public ModToggleOption AddToggle(string key, bool defaultValue = false, ModOptionText label = default, Action<bool> onChanged = null) { return null; }
    public ModSelectionOption AddSelectionBox(string key, string[] choices, int defaultValue = 0, ModOptionText label = default, Action<int> onChanged = null) { return null; }
    public ModSliderOption AddSlider(string key, float min = 0f, float max = 1f, float defaultValue = 0f, float step = 0f, ModOptionText label = default, Action<float> onChanged = null) { return null; }
    public void UnregisterUI() { }
}

public readonly struct ModOptionText
{
    public static implicit operator ModOptionText(string text) { return default; }
}

public abstract class ModOptionDefinition
{
    public ModOptions Options { get { return null; } }
    public string Key { get { return null; } }
}

public abstract class ModOption<T> : ModOptionDefinition
{
    public event Action<T> ValueChanged { add { } remove { } }
    public T DefaultValue { get { return default; } }
    public T Value { get { return default; } set { } }
}

public sealed class ModToggleOption : ModOption<bool>
{
}

public sealed class ModSelectionOption : ModOption<int>
{
}

public sealed class ModSliderOption : ModOption<float>
{
}

public class PlayerAvatar
{
}

public static class HorayModAPI
{
    public static event Action<PlayerAvatar> OnPlayerAvatarSpawnedServerside { add { } remove { } }
    public static bool TryGetUIWidgets(out UIWidgetFactory widgets) { widgets = null; return false; }
}

public class UI_HorayButton : UnityEngine.Component
{
}

public sealed class UIWidgetFactory
{
    public UnityEngine.RectTransform CreateHorizontalLayout(UnityEngine.Transform parent, float spacing = 4f) { return null; }
    public UI_HorayButton CreateButton(UnityEngine.Transform parent, string label, UnityEngine.Events.UnityAction onClick) { return null; }
    public static void SetPreferredSize(UnityEngine.Component widget, float width, float height, float flexibleHeight = 0f) { }
}

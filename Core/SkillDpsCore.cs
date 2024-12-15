using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;

namespace Skill_DPS.Core;

public class SkillDpsCore : BaseSettingsPlugin<Settings>
{
    private readonly List<SkillData> _activeSkills = [];
    private TimeCache<bool> _cachedValue;

    public SkillDpsCore()
    {
        Name = "Skill DPS";
    }

    public override void OnLoad()
    {
        Input.RegisterKey(Keys.LControlKey);
        Settings.UpdateInterval.OnValueChanged += (sender, interval) => _cachedValue.NewTime(interval);
        _cachedValue = new TimeCache<bool>(ProcessSkillDPS, Settings.UpdateInterval);
    }

    private bool ProcessSkillDPS()
    {
        var skillBar = RemoteMemoryObject.GameController.IngameState.IngameUi.SkillBar;
        var hoverUI = GameController.Game.IngameState.UIHoverTooltip.Tooltip;

        if (skillBar?.Skills == null || skillBar.Skills.Count == 0)
        {
            LogError("SkillBar or Skills collection is invalid");
            return false;
        }

        _activeSkills.Clear();
        foreach (var skillElement in skillBar.Skills)
        {
            if (skillElement?.Skill == null) continue;

            var elementRect = skillElement.GetClientRect();
            var displayRect = new RectangleF(elementRect.X, elementRect.Y - 2, elementRect.Width, -Settings.FontSize);

            if (hoverUI != null && hoverUI.GetClientRect().Intersects(displayRect) && hoverUI.IsVisibleLocal)
                continue;

            var damageValue = CalculateSkillDamage(skillElement.Skill);
            if (damageValue == 0) continue;

            _activeSkills.Add(new SkillData
            {
                SkillElement = skillElement,
                Value = damageValue,
                DisplayBox = displayRect,
                DisplayPosition = new Vector2(displayRect.Center.X, displayRect.Center.Y - Settings.FontSize / 2f)
            });
        }

        return true;
    }

    private decimal CalculateSkillDamage(ActorSkill skill)
    {
        var stats = skill.Stats;

        if (stats.TryGetValue(GameStat.HundredTimesDamagePerSecond, out var dps))
            return dps / 100m;

        if (stats.TryGetValue(GameStat.HundredTimesAttacksPerSecond, out var aps))
            return aps / 100m;

        if (stats.TryGetValue(GameStat.HundredTimesAverageDamagePerSkillUse, out var avgDmg))
            return avgDmg / 100m;

        if (stats.TryGetValue(GameStat.IntermediaryFireSkillDotDamageToDealPerMinute, out var dotDmg))
            return dotDmg / 60m;

        if (stats.TryGetValue(GameStat.BaseSkillShowAverageDamageInsteadOfDps, out var baseDmg))
            return baseDmg / 100m;

        return 0;
    }

    public override void Render()
    {
        if (!_cachedValue.Value) return;

        foreach (var skill in _activeSkills.Where(skill =>
                     skill.DisplayBox.Location != Vector2.Zero && skill.SkillElement.IsVisible))
        {
            Graphics.DrawText(FormatDamageValue(skill.Value), skill.DisplayPosition, Settings.FontColor,
                FontAlign.Center);
            Graphics.DrawBox(skill.DisplayBox, Settings.BackgroundColor);
            Graphics.DrawFrame(skill.DisplayBox, Settings.BorderColor, 1);
        }
    }

    private static string FormatDamageValue(decimal value)
    {
        return value switch
        {
            > 999999999 => value.ToString("0,,,.###B", CultureInfo.InvariantCulture),
            > 999999 => value.ToString("0,,.##M", CultureInfo.InvariantCulture),
            > 999 => value.ToString("0,.##K", CultureInfo.InvariantCulture),
            _ => value.ToString("0.#", CultureInfo.InvariantCulture)
        };
    }
}

public class SkillData
{
    public Element SkillElement { get; set; }
    public RectangleF DisplayBox { get; set; }
    public decimal Value { get; set; }
    public Vector2 DisplayPosition { get; set; }
}
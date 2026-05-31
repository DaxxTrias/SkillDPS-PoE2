using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;

namespace Skill_DPS.Core;

public class SkillDpsCore : BaseSettingsPlugin<Settings>
{
    private readonly List<SkillData> _activeSkills = [];
    private TimeCache<bool>? _skillUpdateCache;
    private bool _loggedMissingSkillBar;

    public SkillDpsCore()
    {
        Name = "Skill DPS";
    }

    public override void OnLoad()
    {
        _skillUpdateCache = new TimeCache<bool>(RefreshSkillDps, Settings.UpdateInterval);
        Settings.UpdateInterval.OnValueChanged += (_, _) => _skillUpdateCache?.NewTime(Settings.UpdateInterval);
    }

    public override void AreaChange(AreaInstance area)
    {
        _loggedMissingSkillBar = false;
        _activeSkills.Clear();
    }

    private bool RefreshSkillDps()
    {
        _activeSkills.Clear();

        if (!Settings.Enable)
            return true;

        var ingameUi = GameController.Game.IngameState.IngameUi;
        var skillBar = ingameUi.SkillBar;
        if (skillBar is not { Address: not 0, IsValid: true })
        {
            LogMissingSkillBarOnce();
            return true;
        }

        var skillElements = GetSkillElements(skillBar);
        if (skillElements.Count == 0)
        {
            LogMissingSkillBarOnce();
            return true;
        }

        var hoverTooltip = GameController.Game.IngameState.UIHover.Tooltip;
        var actor = GameController.Player?.GetComponent<Actor>();

        for (var slotIndex = 0; slotIndex < skillElements.Count; slotIndex++)
        {
            var skillElement = skillElements[slotIndex];
            if (skillElement is not { Address: not 0, IsValid: true })
                continue;

            var actorSkill = ResolveActorSkill(skillElement, actor, slotIndex);
            if (actorSkill == null)
                continue;

            var elementRect = skillElement.GetClientRectCache;
            if (elementRect.Width <= 1 || elementRect.Height <= 1)
                continue;

            var labelHeight = Settings.FontSize;
            var displayRect = new RectangleF(
                elementRect.X,
                elementRect.Y - labelHeight - 2f,
                elementRect.Width,
                labelHeight);

            if (hoverTooltip is { IsVisibleLocal: true } &&
                hoverTooltip.GetClientRectCache.Intersects(displayRect))
                continue;

            var damageValue = CalculateSkillDamage(actorSkill);
            if (damageValue <= 0)
                continue;

            _activeSkills.Add(new SkillData
            {
                SkillElement = skillElement,
                Value = damageValue,
                DisplayBox = displayRect,
                DisplayPosition = new Vector2(displayRect.Center.X, displayRect.Top + labelHeight * 0.5f)
            });
        }

        return true;
    }

    private static List<SkillElement> GetSkillElements(SkillBarElement skillBar)
    {
        var skills = skillBar.Skills;
        if (skills is { Count: > 0 })
            return skills;

        var fromChildren = new List<SkillElement>(skillBar.Children.Count);
        foreach (var child in skillBar.Children)
        {
            if (child is not { Address: not 0, IsValid: true })
                continue;

            var skillElement = child as SkillElement ?? child.GetChildAtIndex(0)?.AsObject<SkillElement>();
            if (skillElement is { Address: not 0, IsValid: true })
                fromChildren.Add(skillElement);
        }

        return fromChildren;
    }

    private static ActorSkill? ResolveActorSkill(SkillElement skillElement, Actor? actor, int slotIndex)
    {
        if (skillElement.Skill is { Address: not 0 } skillFromElement)
            return skillFromElement;

        if (actor == null)
            return null;

        var indexedSlot = skillElement.IndexInParent ?? slotIndex;
        return actor.ActorSkills.FirstOrDefault(skill =>
            skill.IsOnSkillBar && skill.SkillSlotIndex == indexedSlot);
    }

    private static decimal CalculateSkillDamage(ActorSkill skill)
    {
        var dps = skill.Dps;
        if (dps > 0)
            return (decimal)dps;

        var stats = skill.Stats;
        if (stats == null || stats.Count == 0)
            return 0;

        if (stats.TryGetValue(GameStat.HundredTimesDamagePerSecond, out var hundredTimesDps))
            return hundredTimesDps / 100m;

        if (stats.TryGetValue(GameStat.HundredTimesAttacksPerSecond, out var attacksPerSecond))
            return attacksPerSecond / 100m;

        if (stats.TryGetValue(GameStat.HundredTimesAverageDamagePerSkillUse, out var averageDamage))
            return averageDamage / 100m;

        if (stats.TryGetValue(GameStat.IntermediaryFireSkillDotDamageToDealPerMinute, out var dotPerMinute))
            return dotPerMinute / 60m;

        if (stats.TryGetValue(GameStat.BaseSkillShowAverageDamageInsteadOfDps, out var averageInsteadOfDps))
            return averageInsteadOfDps / 100m;

        return 0;
    }

    public override void Render()
    {
        if (!Settings.Enable)
            return;

        _ = _skillUpdateCache?.Value;

        foreach (var skill in _activeSkills)
        {
            if (!skill.SkillElement.IsVisibleLocal)
                continue;

            Graphics.DrawText(
                FormatDamageValue(skill.Value),
                skill.DisplayPosition,
                Settings.FontColor,
                FontAlign.Center);
            Graphics.DrawBox(skill.DisplayBox, Settings.BackgroundColor);
            Graphics.DrawFrame(skill.DisplayBox, Settings.BorderColor, 1);
        }
    }

    private void LogMissingSkillBarOnce()
    {
        if (_loggedMissingSkillBar)
            return;

        _loggedMissingSkillBar = true;
        LogError("Skill bar was not available yet (hidden UI, loading, or town). Labels appear once combat skills are visible.");
    }

    private static string FormatDamageValue(decimal value)
    {
        return value switch
        {
            > 999_999_999 => value.ToString("0,,,.###B", CultureInfo.InvariantCulture),
            > 999_999 => value.ToString("0,,.##M", CultureInfo.InvariantCulture),
            > 999 => value.ToString("0,.##K", CultureInfo.InvariantCulture),
            _ => value.ToString("0.#", CultureInfo.InvariantCulture)
        };
    }
}

public class SkillData
{
    public SkillElement SkillElement { get; set; } = null!;
    public RectangleF DisplayBox { get; set; }
    public decimal Value { get; set; }
    public Vector2 DisplayPosition { get; set; }
}

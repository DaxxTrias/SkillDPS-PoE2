using System.Drawing;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace Skill_DPS.Core;

public class Settings : ISettings
{
    public RangeNode<int> UpdateInterval { get; set; } = new(1000, 100, 3000);
    public RangeNode<int> FontSize { get; set; } = new(15, 1, 50);
    public ColorNode FontColor { get; set; } = Color.FromArgb(255, 216, 216, 216);
    public ColorNode BackgroundColor { get; set; } = Color.FromArgb(255, 0, 0, 0);
    public ColorNode BorderColor { get; set; } = Color.FromArgb(255, 146, 107, 43);
    public ToggleNode Enable { get; set; } = new(true);
}
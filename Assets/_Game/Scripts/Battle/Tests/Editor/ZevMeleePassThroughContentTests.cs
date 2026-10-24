using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class ZevMeleePassThroughContentTests
{
    private const string SkillsRoot = "Assets/_Game/Content/Skills/Enemy/";
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Regex DefenseBlocks = new Regex(
        @"(?m)^    - rid: (?<rid>\d+)\r?\n" +
        @"      type: \{class: Action_DefenseWindow, ns: , asm: Assembly-CSharp\}\r?\n" +
        @"      data:\r?\n(?<data>(?:        [^\r\n]*(?:\r?\n|$))*)");

    [TestCase("ZEV/Skill_BlinkSlash.asset", 1003, 0.4f)]
    [TestCase("ZEV/Skill_ComboSlash.asset", 1002, 0.42f)]
    public void SelectedMeleeSkills_OptInWithoutChangingTheirDefenseWindow(
        string relativePath, int defenseId, float expectedWindow)
    {
        MatchCollection blocks = DefenseBlocks.Matches(ReadSkill(relativePath));
        Assert.That(blocks.Count, Is.EqualTo(1), relativePath);
        Match block = blocks[0];
        Assert.That(int.Parse(block.Groups["rid"].Value, CultureInfo.InvariantCulture), Is.EqualTo(defenseId));
        string data = block.Groups["data"].Value;
        Assert.That(ReadNumber(data, "UseMeleePassThrough"), Is.EqualTo(1f));
        Assert.That(ReadNumber(data, "MeleeDashDuration"), Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(ReadNumber(data, "MeleePassDistance"), Is.EqualTo(0.95f).Within(0.0001f));
        Assert.That(ReadNumber(data, "MeleeExitDuration"), Is.EqualTo(0.09f).Within(0.0001f));
        Assert.That(ReadNumber(data, "AttackAnimationLeadTime"), Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(ReadNumber(data, "TimeWindow"), Is.EqualTo(expectedWindow).Within(0.0001f));
        Assert.That(ReadNumber(data, "Requirement"), Is.EqualTo(2f), "Keep the existing Z/X defense requirement.");
    }

    [Test]
    public void OtherEnemySkills_DoNotAcquirePassThroughImplicitly()
    {
        string[] paths = Directory.GetFiles(SkillsRoot, "*.asset", SearchOption.AllDirectories);
        int checkedBlocks = 0;
        foreach (string path in paths)
        {
            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath == SkillsRoot + "ZEV/Skill_BlinkSlash.asset"
                || normalizedPath == SkillsRoot + "ZEV/Skill_ComboSlash.asset") continue;
            foreach (Match block in DefenseBlocks.Matches(File.ReadAllText(path, StrictUtf8)))
            {
                string data = block.Groups["data"].Value;
                Match optIn = FindNumber(data, "UseMeleePassThrough");
                Assert.That(!optIn.Success || ParseNumber(optIn) == 0f, Is.True, path);
                checkedBlocks++;
            }
        }
        Assert.That(checkedBlocks, Is.GreaterThan(0), "The opt-out check must inspect existing defense blocks.");
    }

    [Test]
    public void NewDefenseBlocks_DefaultToExistingMotion()
    {
        var block = new Action_DefenseWindow();
        Assert.That(block.UseMeleePassThrough, Is.False);
        Assert.That(block.MeleeDashDuration, Is.EqualTo(0.12f));
        Assert.That(block.MeleePassDistance, Is.EqualTo(0.95f));
        Assert.That(block.MeleeExitDuration, Is.EqualTo(0.09f));
    }

    private static string ReadSkill(string relativePath) => File.ReadAllText(SkillsRoot + relativePath, StrictUtf8);

    private static Match FindNumber(string data, string field) => Regex.Match(data,
        @"(?m)^        " + Regex.Escape(field) + @": (?<value>[-+\d.eE]+)\r?$");

    private static float ReadNumber(string data, string field)
    {
        Match match = FindNumber(data, field);
        Assert.That(match.Success, Is.True, "Missing serialized field: " + field);
        return ParseNumber(match);
    }

    private static float ParseNumber(Match match) =>
        float.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
}

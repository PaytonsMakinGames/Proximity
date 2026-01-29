using UnityEngine;

public static class XpCurveRS
{
    // Keep OSRS ratios, but scale the whole table so that:
    // XP required to reach Level 2 == Level2XpTarget.
    public const int MaxLevel = 99;

    // Your requested anchor:
    public const int Level2XpTarget = 1800;

    // Raw OSRS-style table (unscaled)
    static readonly int[] rawTable = BuildRawXpTable();

    // Scaled table (what the game actually uses)
    static readonly int[] xpTable = BuildScaledXpTable();

    static int[] BuildRawXpTable()
    {
        // rawTable[level] = XP required to REACH that level
        // indices 1..MaxLevel, ignore index 0.
        var table = new int[MaxLevel + 1];

        int points = 0;
        table[1] = 0;

        for (int level = 2; level <= MaxLevel; level++)
        {
            points += Mathf.FloorToInt(level - 1 + 300f * Mathf.Pow(2f, (level - 1) / 7f));
            table[level] = points / 4;
        }

        return table;
    }

    static int[] BuildScaledXpTable()
    {
        var table = new int[MaxLevel + 1];

        // Safety
        int rawL2 = Mathf.Max(1, rawTable[2]);
        float scale = Level2XpTarget / (float)rawL2;

        table[1] = 0;

        for (int level = 2; level <= MaxLevel; level++)
        {
            table[level] = Mathf.RoundToInt(rawTable[level] * scale);
        }

        // Ensure strictly nondecreasing (rounding can cause duplicates)
        for (int level = 2; level <= MaxLevel; level++)
        {
            if (table[level] < table[level - 1])
                table[level] = table[level - 1];
        }

        return table;
    }

    public static int XpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        return xpTable[level];
    }

    public static int LevelForXp(int xp)
    {
        xp = Mathf.Max(0, xp);

        int lo = 1;
        int hi = MaxLevel;

        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (xp >= xpTable[mid]) lo = mid;
            else hi = mid - 1;
        }

        return lo;
    }

    public static float Progress01(int xp)
    {
        int level = LevelForXp(xp);
        if (level >= MaxLevel) return 1f;

        int cur = XpForLevel(level);
        int next = XpForLevel(level + 1);
        int span = Mathf.Max(1, next - cur);

        return Mathf.Clamp01((xp - cur) / (float)span);
    }

    public static int XpToNextLevel(int xp)
    {
        int level = LevelForXp(xp);
        if (level >= MaxLevel) return 0;
        return Mathf.Max(0, XpForLevel(level + 1) - xp);
    }
}
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Hint List", fileName = "HintList")]
public class HintList : ScriptableObject
{
    public enum HintCategory
    {
        Irony,
        Philosophy,
        Mechanics,
        Meta
    }

    [System.Serializable]
    private struct HintGroup
    {
        public HintCategory category;
        public Sprite icon;
        [TextArea]
        public string[] hints;
    }

    public struct HintResult
    {
        public string Text;
        public Sprite Icon;

        public HintResult(string text, Sprite icon)
        {
            Text = text;
            Icon = icon;
        }
    }

    [SerializeField] private HintGroup[] groups;

    public HintResult GetRandom()
    {
        if (groups == null || groups.Length == 0)
            return default;

        int groupIndex = Random.Range(0, groups.Length);
        var group = groups[groupIndex];

        var hints = group.hints;
        if (hints == null || hints.Length == 0)
            return default;

        int hintIndex = Random.Range(0, hints.Length);
        return new HintResult(hints[hintIndex], group.icon);
    }
    public HintResult GetRandom(HintCategory category)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].category != category)
                continue;

            var hints = groups[i].hints;
            if (hints == null || hints.Length == 0)
                return default;

            var index = Random.Range(0, hints.Length);
            return new HintResult(hints[index], groups[i].icon);
        }

        return default;
    }

    public HintResult GetRandomNonRepeating(HintCategory category, ref int lastIndex)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].category != category)
                continue;

            var hints = groups[i].hints;
            if (hints == null || hints.Length == 0)
                return default;

            if (hints.Length == 1)
            {
                lastIndex = 0;
                return new HintResult(hints[0], groups[i].icon);
            }

            int index;
            do
            {
                index = Random.Range(0, hints.Length);
            }
            while (index == lastIndex);

            lastIndex = index;
            return new HintResult(hints[index], groups[i].icon);
        }

        return default;
    }
}
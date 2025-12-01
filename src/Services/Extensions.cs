using System.Text;

namespace ModelEvaluator;

public static class Extensions
{
    private static readonly Random _rnd = new();
    
    public static T NextItem<T>(this Random r, IReadOnlyList<T> list) => list[r.Next(list.Count)];
    
    public static string Replace(this string s, string oldValue, string newValue, int count, StringComparison comp)
    {
        var sb = new StringBuilder(s);
        int replaced = 0, pos = 0;
        while (replaced < count && (pos = sb.ToString().IndexOf(oldValue, pos, comp)) >= 0)
        {
            sb.Remove(pos, oldValue.Length).Insert(pos, newValue);
            pos += newValue.Length;
            replaced++;
        }
        return sb.ToString();
    }
}

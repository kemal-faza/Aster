namespace Aster.Core.Logic;

public static class TrackHash
{
    public static int Of(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}

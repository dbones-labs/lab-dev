namespace Dev.Controllers.Github.Internal;

public static class SystemDateTime
{
    private static Func<DateTime> _get = () => DateTime.UtcNow;

    public static DateTime UtcNow => _get();

    public static void Set(Func<DateTime> getDateTime)
    {
        _get = getDateTime;
    }

    public static void Reset()
    {
        _get = () => DateTime.UtcNow;
    }
}
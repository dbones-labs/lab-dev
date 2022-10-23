namespace Dev.Infrastructure;

public static class Rng
{
    private static Random _random = new Random();

    public static int Between(int min, int max)
    {
        return _random.Next(min, max);
    }
    
}
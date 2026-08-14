namespace ModScope.LocalKnowledge;

public static class SevenDaysToDiePathInference
{
    public static string? InferBaseDataConfigPath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return null;
        }

        try
        {
            return Path.Combine(
                Path.GetFullPath(gamePath.Trim()),
                "Data",
                "Config");
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}

namespace gabMileage.Mileage.Api;

internal static class DelegatedPermissions
{
    public const string PomodoroRead = "mileage.read";
    public const string PomodoroWrite = "mileage.write";

    public static string?[] All => typeof(DelegatedPermissions)
        .GetFields()
        .Where(f => f.Name != nameof(All))
        .Select(f => f.GetValue(null) as string)
        .ToArray();
}

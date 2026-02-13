namespace Game.Core.Services;

public static class DemoGatePolicy
{
    public static bool AreDemosEnabled(bool? playableOverride, bool securityTestModeEnabled, bool isDebugBuild)
    {
        if (playableOverride.HasValue)
            return playableOverride.Value;

        if (isDebugBuild)
            return true;

        return securityTestModeEnabled;
    }
}

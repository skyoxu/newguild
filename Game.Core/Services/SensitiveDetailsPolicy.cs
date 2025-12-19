using System;

namespace Game.Core.Services;

public static class SensitiveDetailsPolicy
{
    public static bool IncludeSensitiveDetails(bool isDebugBuild, Func<string, string?>? getEnv = null)
    {
        if (!isDebugBuild)
            return false;

        getEnv ??= System.Environment.GetEnvironmentVariable;

        var isSecureMode = getEnv("GD_SECURE_MODE") == "1";
        var isCi = !string.IsNullOrWhiteSpace(getEnv("CI"));

        return !isSecureMode && !isCi;
    }
}


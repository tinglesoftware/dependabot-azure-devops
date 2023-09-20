namespace Tingle.Dependabot;

internal static class AuthConstants
{
    // These values are fixed strings due to configuration sections
    internal const string SchemeNameManagement = "Management";
    internal const string SchemeNameServiceHooks = "ServiceHooks";
    internal const string SchemeNameUpdater = "Updater";

    internal const string PolicyNameManagement = "Management";
    internal const string PolicyNameServiceHooks = "ServiceHooks";
    internal const string PolicyNameUpdater = "Updater";
}

internal static class ErrorCodes
{
    internal const string FeaturesDisabled = "features_disabled";
}

internal static class FeatureNames
{
    internal const string DebugAllJobs = "DebugAllJobs";
    internal const string DebugJobs = "DebugJobs";
}

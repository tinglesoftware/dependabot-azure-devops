namespace Tingle.Dependabot;

internal static class AuthConstants
{
    // These values are fixed strings due to configuration sections
    internal const string SchemeNameServiceHooks = "ServiceHooks";
    internal const string SchemeNameUpdater = "Updater";

    internal const string PolicyNameServiceHooks = "ServiceHooks";
    internal const string PolicyNameUpdater = "Updater";
}

internal static class ErrorCodes
{
    internal const string ProjectNotFound = "project_not_found";
    internal const string RepositoryNotFound = "repository_not_found";
    internal const string RepositoryUpdateNotFound = "repository_update_not_found";
    internal const string UpdateJobNotFound = "update_job_not_found";
}

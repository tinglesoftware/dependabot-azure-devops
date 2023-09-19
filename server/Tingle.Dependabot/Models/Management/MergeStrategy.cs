namespace Tingle.Dependabot.Models.Management;

public enum MergeStrategy
{
    NoFastForward = 0,
    Rebase = 1,
    RebaseMerge = 2,
    Squash = 3,
}

using Tingle.Dependabot.Models;

namespace Tingle.Dependabot.Workflow;

public readonly record struct SchedulableUpdate(int Index, DependabotUpdateSchedule Supplied)
{
    public void Deconstruct(out int index, out DependabotUpdateSchedule supplied)
    {
        index = Index;
        supplied = Supplied;
    }
}

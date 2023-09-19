using Tingle.Dependabot.Models.Dependabot;

namespace Tingle.Dependabot.Workflow;

public readonly record struct SchedulableUpdate(int Index, DependabotUpdateSchedule Supplied)
{
    public void Deconstruct(out int index, out DependabotUpdateSchedule supplied)
    {
        index = Index;
        supplied = Supplied;
    }
}

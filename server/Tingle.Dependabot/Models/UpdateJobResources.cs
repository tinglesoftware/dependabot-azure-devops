using Azure.ResourceManager.AppContainers.Models;
using System.ComponentModel.DataAnnotations;

namespace Tingle.Dependabot.Models;

public class UpdateJobResources
{
    public UpdateJobResources() { } // required for deserialization

    public UpdateJobResources(double cpu, double memory)
    {
        // multiplication by 100 to avoid the approximation
        if (memory * 100 % (0.1 * 100) != 0)
        {
            throw new ArgumentException("The memory requirement should be in increments of 0.1.", nameof(memory));
        }

        Cpu = cpu;
        Memory = memory;
    }

    /// <summary>CPU units provisioned.</summary>
    /// <example>0.25</example>
    [Required]
    public double Cpu { get; set; }

    /// <summary>Memory provisioned in GB.</summary>
    /// <example>1.2</example>
    [Required]
    public double Memory { get; set; }

    public static UpdateJobResources FromEcosystem(string ecosystem)
    {
        return ecosystem switch
        {
            //"nuget" => new(cpu: 0.25, memory: 0.2),
            //"gitsubmodule" => new(cpu: 0.1, memory: 0.2),
            //"terraform" => new(cpu: 0.25, memory: 1),
            //"npm" => new(cpu: 0.25, memory: 1),
            _ => new UpdateJobResources(cpu: 0.25, memory: 0.5), // the minimum
        };
    }

    public static implicit operator AppContainerResources(UpdateJobResources resources)
    {
        return new() { Cpu = resources.Cpu, Memory = $"{resources.Memory}Gi", };
    }
}

using Azure.ResourceManager.AppContainers.Models;
using System.ComponentModel.DataAnnotations;

namespace Tingle.Dependabot.Models.Management;

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
            "npm" => new(cpu: 1, memory: 2),
            "yarn" => new(cpu: 1, memory: 2),
            "pnpm" => new(cpu: 1, memory: 2),
            "terraform" => new(cpu: 0.5, memory: 1),
            _ => new(cpu: 0.25, memory: 0.5), // the minimum
        };
    }

    public static implicit operator AppContainerResources(UpdateJobResources resources)
    {
        return new() { Cpu = resources.Cpu, Memory = $"{resources.Memory}Gi", };
    }
}

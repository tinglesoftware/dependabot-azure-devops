using Azure.ResourceManager.AppContainers.Models;
using System.ComponentModel.DataAnnotations;

namespace Tingle.Dependabot.Models.Management;

public class UpdateJobResources
{
    // the minimum is 0.25vCPU and 0.5GB but we need more because a lot is happening in the container
    private static readonly UpdateJobResources Default = new(cpu: 0.5, memory: 1);

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
            "npm" => Default * 2,
            "yarn" => Default * 2,
            "pnpm" => Default * 2,
            _ => Default,
        };
    }

    public static UpdateJobResources operator *(UpdateJobResources resources, double factor) => new(resources.Cpu * factor, resources.Memory * factor);
    public static UpdateJobResources operator /(UpdateJobResources resources, double factor) => new(resources.Cpu / factor, resources.Memory / factor);

    public static implicit operator AppContainerResources(UpdateJobResources resources)
    {
        return new() { Cpu = resources.Cpu, Memory = $"{resources.Memory}Gi", };
    }
}

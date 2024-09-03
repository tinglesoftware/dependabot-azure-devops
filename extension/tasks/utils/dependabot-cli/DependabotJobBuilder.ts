import { ISharedVariables } from "../getSharedVariables";
import { IDependabotRegistry, IDependabotUpdate } from "../IDependabotConfig";
import { IDependabotUpdateJob } from "./interfaces/IDependabotUpdateJob";

// Wrapper class for building dependabot update job objects
export class DependabotJobBuilder {

    // Create a dependabot update job that updates all dependencies for a package ecyosystem
    public static updateAllDependenciesJob(
        variables: ISharedVariables,
        update: IDependabotUpdate,
        registries: Record<string, IDependabotRegistry>,
        existingPullRequests: any[]
    ): IDependabotUpdateJob {
        return {
            config: update,
            job: {
                // TODO: Parse all options from `config` and `variables`
                id: `update-${update.packageEcosystem}-all`, // TODO: Refine this
                'package-manager': update.packageEcosystem,
                'update-subdependencies': true, // TODO: add config option
                'updating-a-pull-request': false,
                'dependency-groups': mapDependencyGroups(update.groups),
                'allowed-updates': [
                    { 'update-type': 'all' } // TODO: update.allow
                ],
                'ignore-conditions': [], // TODO: update.ignore
                'security-updates-only': false, // TODO: update.'security-updates-only'
                'security-advisories': [], // TODO: update.securityAdvisories
                source: {
                    provider: 'azure',
                    'api-endpoint': variables.apiEndpointUrl,
                    hostname: variables.hostname,
                    repo: `${variables.organization}/${variables.project}/_git/${variables.repository}`,
                    branch: update.targetBranch,
                    commit: undefined, // use latest commit of target branch
                    directory: update.directories?.length == 0 ? update.directory : undefined,
                    directories: update.directories?.length > 0 ? update.directories : undefined
                },
                'existing-pull-requests': existingPullRequests.filter(pr => !pr['dependency-group-name']),
                'existing-group-pull-requests': existingPullRequests.filter(pr => pr['dependency-group-name']),
                'commit-message-options': undefined, // TODO: update.commitMessageOptions
                'experiments': undefined, // TODO: update.experiments
                'max-updater-run-time': undefined, // TODO: update.maxUpdaterRunTime
                'reject-external-code': undefined, // TODO: update.insecureExternalCodeExecution
                'repo-private': undefined, // TODO: update.repoPrivate
                'repo-contents-path': undefined, // TODO: update.repoContentsPath
                'requirements-update-strategy': undefined, // TODO: update.requirementsUpdateStrategy
                'lockfile-only': undefined, // TODO: update.lockfileOnly
                'vendor-dependencies': undefined, // TODO: update.vendorDependencies
                'debug': variables.debug
            },
            credentials: mapRegistryCredentials(variables, registries)
        };
    }

    // Create a dependabot update job that updates a single pull request
    static updatePullRequestJob(
        variables: ISharedVariables,
        update: IDependabotUpdate,
        registries: Record<string, IDependabotRegistry>,
        existingPullRequests: any[],
        pullRequestToUpdate: any
    ): IDependabotUpdateJob {
        const dependencyGroupName = pullRequestToUpdate['dependency-group-name'];
        const dependencies = (dependencyGroupName ? pullRequestToUpdate['dependencies'] : pullRequestToUpdate)?.map(d => d['dependency-name']);
        const result = this.updateAllDependenciesJob(variables, update, registries, existingPullRequests);
        result.job['id'] = `update-${update.packageEcosystem}-${Date.now()}`; // TODO: Refine this
        result.job['updating-a-pull-request'] = true;
        result.job['dependency-group-to-refresh'] = dependencyGroupName;
        result.job['dependencies'] = dependencies;
        return result;
    }

}

// Map registry credentials
function mapRegistryCredentials(variables: ISharedVariables, registries: Record<string, IDependabotRegistry>): any[] {
    let registryCredentials = new Array();
    if (variables.systemAccessToken) {
        registryCredentials.push({
            type: 'git_source',
            host: variables.hostname,
            username: variables.systemAccessUser?.trim()?.length > 0 ? variables.systemAccessUser : 'x-access-token',
            password: variables.systemAccessToken
        });
    }
    if (registries) {
        for (const key in registries) {
            const registry = registries[key];
            registryCredentials.push({
                type: registry.type,
                host: registry.host,
                url: registry.url,
                registry: registry.registry,
                region: undefined, // TODO: registry.region,
                username: registry.username,
                password: registry.password,
                token: registry.token,
                'replaces-base': registry['replaces-base'] || false
            });
        }
    }

    return registryCredentials;
}

// Map dependency groups
function mapDependencyGroups(groups: string): any[] {
    const dependencyGroups = JSON.parse(groups);
    return Object.keys(dependencyGroups).map(name => {
        return {
            'name': name,
            'rules': dependencyGroups[name]
        };
    });
}

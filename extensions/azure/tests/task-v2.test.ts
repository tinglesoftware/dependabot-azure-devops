import { TaskResult } from 'azure-pipelines-task-lib';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { type DependabotConfig, type DependabotOperationResult } from 'paklo/dependabot';
import { GitHubGraphClient } from 'paklo/github';
import { AzureDevOpsWebApiClient } from '../src/azure-devops/client';
import {
  DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
  DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
  DEVOPS_PR_PROPERTY_MICROSOFT_GIT_SOURCE_REF_NAME,
  type IPullRequestProperties,
} from '../src/azure-devops/models';
import { DependabotCli, type DependabotCliOptions } from '../src/dependabot/cli';
import { abandonPullRequestsWhereSourceRefIsDeleted, performDependabotUpdatesAsync } from '../src/task-v2';
import { type ISharedVariables } from '../src/utils/shared-variables';

vi.mock('../src/azure-devops/client');
vi.mock('../src/dependabot/cli');
vi.mock('../src/dependabot/job-builder');
vi.mock('paklo/github');

describe('abandonPullRequestsWhereSourceRefIsDeleted', () => {
  let taskInputs: ISharedVariables;
  let devOpsPrAuthorClient: AzureDevOpsWebApiClient;
  let existingBranchNames: string[];
  let existingPullRequests: IPullRequestProperties[];

  beforeEach(() => {
    vi.clearAllMocks();
    taskInputs = {
      url: {},
      dryRun: false,
    } as ISharedVariables;
    devOpsPrAuthorClient = new AzureDevOpsWebApiClient('https://dev.azure.com/test-org', 'fake-token', true);
    devOpsPrAuthorClient.abandonPullRequest = vi.fn().mockResolvedValue(true);
    existingBranchNames = [];
    existingPullRequests = [
      {
        id: 1,
        properties: [
          {
            name: DEVOPS_PR_PROPERTY_MICROSOFT_GIT_SOURCE_REF_NAME,
            value: 'dependabot/nuget/dependency1-1.0.0',
          },
        ],
      },
    ];
  });

  it('should abandon pull requests where the source branch has been deleted', async () => {
    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    expect(devOpsPrAuthorClient.abandonPullRequest).toHaveBeenCalledWith({
      pullRequestId: 1,
      comment:
        'It might be a good idea to add an ' +
        '[`ignore` condition](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/dependabot-options-reference#ignore--) ' +
        'with the desired `update-types` to your config file.',
    });
  });

  it('should not abandon pull requests when `dryRun` is true', async () => {
    taskInputs.dryRun = true;

    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    expect(devOpsPrAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
  });

  it('should not abandon pull requests where the source branch still exists', async () => {
    existingBranchNames = ['dependabot/nuget/dependency1-1.0.0'];

    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    expect(devOpsPrAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
  });

  it('should ignore "refs/heads/" prefix when comparing branch names', async () => {
    existingBranchNames = ['dependabot/nuget/dependency1-1.0.0'];
    existingPullRequests = [
      {
        id: 1,
        properties: [
          {
            name: DEVOPS_PR_PROPERTY_MICROSOFT_GIT_SOURCE_REF_NAME,
            value: 'refs/heads/dependabot/nuget/dependency1-1.0.0',
          },
        ],
      },
    ];

    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    expect(devOpsPrAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
  });

  it('should remove the pull request from the existing pull requests list after abandoning it', async () => {
    const pullRequestToBeAbandoned = existingPullRequests[0];

    await abandonPullRequestsWhereSourceRefIsDeleted(
      taskInputs,
      devOpsPrAuthorClient,
      existingBranchNames,
      existingPullRequests,
    );

    expect(existingPullRequests.length).not.toContain(pullRequestToBeAbandoned);
  });

  it('should not abandon any pull requests if existingBranchNames is undefined or null', async () => {
    await abandonPullRequestsWhereSourceRefIsDeleted(taskInputs, devOpsPrAuthorClient, undefined, existingPullRequests);
    await abandonPullRequestsWhereSourceRefIsDeleted(taskInputs, devOpsPrAuthorClient, undefined, existingPullRequests);

    expect(devOpsPrAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
  });

  it('should not abandon any pull requests if existingPullRequests is undefined or null', async () => {
    await abandonPullRequestsWhereSourceRefIsDeleted(taskInputs, devOpsPrAuthorClient, existingBranchNames, undefined);
    await abandonPullRequestsWhereSourceRefIsDeleted(taskInputs, devOpsPrAuthorClient, existingBranchNames, undefined);

    expect(devOpsPrAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
  });
});

describe('performDependabotUpdatesAsync', () => {
  let taskInputs: ISharedVariables;
  let dependabotConfig: DependabotConfig;
  let dependabotCli: DependabotCli;
  let dependabotCliUpdateOptions: DependabotCliOptions;
  let existingPullRequests: IPullRequestProperties[];

  beforeEach(() => {
    vi.clearAllMocks();
    taskInputs = { url: {} } as ISharedVariables;
    dependabotConfig = {
      updates: [
        {
          'package-ecosystem': 'npm',
          'directory': '/',
        },
      ],
      registries: {},
    } as DependabotConfig;
    dependabotCli = new DependabotCli(DependabotCli.CLI_PACKAGE_LATEST, null!, true);
    dependabotCli.update = vi
      .fn()
      .mockResolvedValue([
        { success: true, output: { type: 'mark_as_processed', data: {} } },
      ] as DependabotOperationResult[]);
    dependabotCliUpdateOptions = {};
    existingPullRequests = [];
  });

  it('should perform "update all" job successfully', async () => {
    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [],
    });
    expect(dependabotCli.update).toHaveBeenCalled();
    // TODO: figure out how to test for nested object, e.g. expect(builder.forUpdate).toHaveBeenCalled();
    // expect(DependabotJobBuilder.updateAllDependenciesJob).toHaveBeenCalled();
  });

  it('should skip "update all" job if open pull requests limit is reached', async () => {
    dependabotConfig.updates[0]!['open-pull-requests-limit'] = 1;
    existingPullRequests.push({
      id: 1,
      properties: [
        {
          name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
          value: 'npm_and_yarn',
        },
        {
          name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
          value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
        },
      ],
    });

    const tsDependabotOutputProcessor = await import('../src/dependabot/output-processor');
    vi.spyOn(tsDependabotOutputProcessor, 'parsePullRequestProperties').mockReturnValue('npm_and_yarn' as never);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [],
    });
    // TODO: figure out how to test for nested object, e.g. expect(builder.forUpdate).toHaveBeenCalled();
    // expect(DependabotJobBuilder.updateAllDependenciesJob).not.toHaveBeenCalled();
  });

  it('should perform "update security-only" job if open pull request limit is zero', async () => {
    dependabotConfig.updates[0]!['open-pull-requests-limit'] = 0;
    const ghsaClient = new GitHubGraphClient('fake-token');
    ghsaClient.getSecurityVulnerabilitiesAsync = vi.fn().mockResolvedValue([]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [],
    });
    // TODO: figure out how to test for nested object, e.g. expect(builder.forDependenciesList).toHaveBeenCalled();
    // expect(DependabotJobBuilder.listAllDependenciesJob).toHaveBeenCalled();
  });

  it('should perform "update pull request" job successfully if there are existing pull requests', async () => {
    dependabotConfig.updates[0]!['open-pull-requests-limit'] = 1;
    existingPullRequests.push({
      id: 1,
      properties: [
        {
          name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
          value: 'npm_and_yarn',
        },
        {
          name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
          value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
        },
      ],
    });

    const tsDependabotJobBuilder = await import('paklo/dependabot');
    vi.spyOn(tsDependabotJobBuilder, 'mapPackageEcosystemToPackageManager').mockReturnValue('npm_and_yarn');

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [],
    });
    // TODO: figure out how to test for nested object, e.g. expect(builder.forUpdate).toHaveBeenCalled();
    // expect(DependabotJobBuilder.updatePullRequestJob).toHaveBeenCalled();
  });

  it('should return Succeeded when all updates are successful', async () => {
    dependabotCli.update = vi.fn().mockResolvedValue([{ success: true, output: {} }] as DependabotOperationResult[]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [],
    });
  });

  it('should return SucceededWithIssues result when updates are mixture of success and failure', async () => {
    dependabotCli.update = vi.fn().mockResolvedValue([
      { success: true, output: {} },
      { success: false, output: {} },
    ] as DependabotOperationResult[]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.SucceededWithIssues,
      message: 'Partial success; some update tasks completed with issues. Check the logs for more information',
      prs: [],
    });
  });

  it('should return Failed result when all updates are failure', async () => {
    dependabotCli.update = vi.fn().mockResolvedValue([{ success: false, output: {} }] as DependabotOperationResult[]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Failed,
      message: 'Update tasks failed. Check the logs for more information',
      prs: [],
    });
  });

  it('should return Skipped result when no updates are performed', async () => {
    dependabotCli.update = vi.fn().mockResolvedValue([]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({ result: TaskResult.Skipped, message: '', prs: [] });
  });

  it('should collect PRs', async () => {
    dependabotCli.update = vi.fn().mockResolvedValue([
      { success: true, output: {}, pr: 501 },
      { success: true, output: {}, pr: 501 },
      { success: true, output: {}, pr: 521 },
    ] as DependabotOperationResult[]);

    const updateResult = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(updateResult).toEqual({
      result: TaskResult.Succeeded,
      message: 'All update tasks completed successfully',
      prs: [501, 521],
    });
  });
});

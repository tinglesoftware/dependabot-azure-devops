import { performDependabotUpdatesAsync } from './index';
import { IPullRequestProperties } from './utils/azure-devops/interfaces/IPullRequest';
import { DependabotCli } from './utils/dependabot-cli/DependabotCli';
import { DependabotJobBuilder } from './utils/dependabot-cli/DependabotJobBuilder';
import { DependabotOutputProcessor } from './utils/dependabot-cli/DependabotOutputProcessor';
import { IDependabotUpdateOperationResult } from './utils/dependabot-cli/interfaces/IDependabotUpdateOperationResult';
import { IDependabotConfig } from './utils/dependabot/interfaces/IDependabotConfig';
import { ISharedVariables } from './utils/getSharedVariables';
import { GitHubGraphClient } from './utils/github/GitHubGraphClient';

jest.mock('./utils/dependabot-cli/DependabotCli');
jest.mock('./utils/dependabot-cli/DependabotJobBuilder');
jest.mock('./utils/github/GitHubGraphClient');

const tsDependabotJobBuilder = require('./utils/dependabot-cli/DependabotJobBuilder');
const tsDependabotOutputProcessor = require('./utils/dependabot-cli/DependabotOutputProcessor');

describe('performDependabotUpdatesAsync', () => {
  let taskInputs: ISharedVariables;
  let dependabotConfig: IDependabotConfig;
  let dependabotCli: DependabotCli;
  let dependabotCliUpdateOptions: any;
  let existingPullRequests: IPullRequestProperties[];

  beforeEach(() => {
    jest.clearAllMocks();
    taskInputs = {} as ISharedVariables;
    dependabotConfig = {
      updates: [
        {
          'package-ecosystem': 'npm',
          'directory': '/',
        },
      ],
      registries: {},
    } as IDependabotConfig;
    dependabotCli = new DependabotCli(DependabotCli.CLI_PACKAGE_LATEST, null, true);
    dependabotCli.update = jest
      .fn()
      .mockResolvedValue([
        { success: true, output: { type: 'mark_as_processed', data: {} } },
      ] as IDependabotUpdateOperationResult[]);
    dependabotCliUpdateOptions = {};
    existingPullRequests = [];
  });

  it('should perform "update all" job successfully', async () => {
    const failedUpdates = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(failedUpdates).toBe(0);
    expect(dependabotCli.update).toHaveBeenCalled();
    expect(DependabotJobBuilder.updateAllDependenciesJob).toHaveBeenCalled();
  });

  it('should skip "update all" job if open pull requests limit is reached', async () => {
    dependabotConfig.updates[0]['open-pull-requests-limit'] = 1;
    existingPullRequests.push({
      id: 1,
      properties: [
        {
          name: DependabotOutputProcessor.PR_PROPERTY_NAME_PACKAGE_MANAGER,
          value: 'npm_and_yarn',
        },
        {
          name: DependabotOutputProcessor.PR_PROPERTY_NAME_DEPENDENCIES,
          value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
        },
      ],
    });

    jest.spyOn(tsDependabotOutputProcessor, 'parsePullRequestProperties').mockReturnValue('npm_and_yarn');

    const failedUpdates = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(failedUpdates).toBe(0);
    expect(DependabotJobBuilder.updateAllDependenciesJob).not.toHaveBeenCalled();
  });

  it('should perform "update security-only" job if open pull request limit is zero', async () => {
    dependabotConfig.updates[0]['open-pull-requests-limit'] = 0;
    const ghsaClient = new GitHubGraphClient('fake-token');
    ghsaClient.getSecurityVulnerabilitiesAsync = jest.fn().mockResolvedValue([]);

    const failedUpdates = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(failedUpdates).toBe(0);
    expect(DependabotJobBuilder.listAllDependenciesJob).toHaveBeenCalled();
  });

  it('should perform "update pull request" job successfully if there are existing pull requests', async () => {
    dependabotConfig.updates[0]['open-pull-requests-limit'] = 1;
    existingPullRequests.push({
      id: 1,
      properties: [
        {
          name: DependabotOutputProcessor.PR_PROPERTY_NAME_PACKAGE_MANAGER,
          value: 'npm_and_yarn',
        },
        {
          name: DependabotOutputProcessor.PR_PROPERTY_NAME_DEPENDENCIES,
          value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
        },
      ],
    });

    jest.spyOn(tsDependabotJobBuilder, 'mapPackageEcosystemToPackageManager').mockReturnValue('npm_and_yarn');

    const failedUpdates = await performDependabotUpdatesAsync(
      taskInputs,
      dependabotConfig,
      dependabotConfig.updates,
      dependabotCli,
      dependabotCliUpdateOptions,
      existingPullRequests,
    );

    expect(failedUpdates).toBe(0);
    expect(DependabotJobBuilder.updatePullRequestJob).toHaveBeenCalled();
  });
});

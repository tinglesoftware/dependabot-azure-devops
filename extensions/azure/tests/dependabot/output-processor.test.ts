import { error, warning } from 'azure-pipelines-task-lib/task';
import { type DependabotOperation } from 'paklo/dependabot';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AzureDevOpsWebApiClient } from '../../src/azure-devops/client';
import {
  DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
  DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
  type IPullRequestProperties,
} from '../../src/azure-devops/models';
import { DependabotOutputProcessor } from '../../src/dependabot/output-processor';
import { type ISharedVariables } from '../../src/utils/shared-variables';

vi.mock('../../src/azure-devops/client');
vi.mock('../../src/utils/shared-variables');
vi.mock('azure-pipelines-task-lib/task');

describe('DependabotOutputProcessor', () => {
  let processor: DependabotOutputProcessor;
  let taskInputs: ISharedVariables;
  let prAuthorClient: AzureDevOpsWebApiClient;
  let prApproverClient: AzureDevOpsWebApiClient;
  let existingBranchNames: string[];
  let existingPullRequests: IPullRequestProperties[];

  beforeEach(() => {
    taskInputs = {
      url: {},
    } as ISharedVariables;
    prAuthorClient = new AzureDevOpsWebApiClient('http://localhost:8081', 'token1', true);
    prApproverClient = new AzureDevOpsWebApiClient('http://localhost:8081', 'token1', true);
    existingBranchNames = [];
    existingPullRequests = [];
    processor = new DependabotOutputProcessor(
      taskInputs,
      prAuthorClient,
      prApproverClient,
      existingBranchNames,
      existingPullRequests,
      true,
    );
  });

  describe('process', () => {
    let update: DependabotOperation;

    beforeEach(() => {
      vi.clearAllMocks();
      update = {
        job: {
          'id': 'job1',
          'package-manager': 'npm_and_yarn',
          'source': {
            hostname: 'localhost:8081',
            provider: 'azure',
            repo: 'testproject/_git/test-repo',
          },
          'experiments': {},
          'credentials-metadata': [],
        },
        credentials: [],
        config: {
          'package-ecosystem': 'npm',
        },
      };
    });

    it('should process "update_dependency_list"', async () => {
      const result = await processor.process(update, {
        type: 'update_dependency_list',
        data: {
          dependencies: [],
          dependency_files: [],
        },
      });

      expect(result).toEqual({ success: true });
    });

    it('should skip processing "create_pull_request" if "dryRun" is true', async () => {
      taskInputs.dryRun = true;

      const result = await processor.process(update, {
        type: 'create_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependencies': [],
        },
      });

      expect(result).toEqual({ success: true });
      expect(prAuthorClient.createPullRequest).not.toHaveBeenCalled();
    });

    it('should skip processing "create_pull_request" if open pull request limit is reached', async () => {
      const packageManager = 'nuget';
      update.config['open-pull-requests-limit'] = 1;
      update.job['package-manager'] = packageManager;
      existingPullRequests.push({
        id: 1,
        properties: [
          { name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER, value: packageManager },
          { name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES, value: '[]' },
        ],
      } as IPullRequestProperties);
      console.info(existingPullRequests);
      const result = await processor.process(update, {
        type: 'create_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependencies': [],
        },
      });

      expect(result).toEqual({ success: true });
      expect(prAuthorClient.createPullRequest).not.toHaveBeenCalled();
    });

    it('should process "create_pull_request"', async () => {
      taskInputs.autoApprove = true;

      prAuthorClient.createPullRequest = vi.fn().mockResolvedValue(11);
      prAuthorClient.getDefaultBranch = vi.fn().mockResolvedValue('main');
      prApproverClient.approvePullRequest = vi.fn().mockResolvedValue(true);

      const result = await processor.process(update, {
        type: 'create_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependencies': [],
        },
      });

      expect(result).toEqual({ success: true, pr: 11 });
      expect(prAuthorClient.createPullRequest).toHaveBeenCalled();
      expect(prApproverClient.approvePullRequest).toHaveBeenCalled();
    });

    it('should skip processing "update_pull_request" if "dryRun" is false', async () => {
      taskInputs.dryRun = true;

      const result = await processor.process(update, {
        type: 'update_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependency-names': [],
        },
      });

      expect(result).toEqual({ success: true });
      expect(prAuthorClient.updatePullRequest).not.toHaveBeenCalled();
    });

    it('should fail processing "update_pull_request" if pull request does not exist', async () => {
      const result = await processor.process(update, {
        type: 'update_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependency-names': ['dependency1'],
        },
      });

      expect(result).toEqual({ success: false });
      expect(prAuthorClient.updatePullRequest).not.toHaveBeenCalled();
    });

    it('should process "update_pull_request"', async () => {
      taskInputs.autoApprove = true;
      update.job['package-manager'] = 'npm_and_yarn';

      existingPullRequests.push({
        id: 11,
        properties: [
          { name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER, value: 'npm_and_yarn' },
          {
            name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
            value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
          },
        ],
      });

      prAuthorClient.updatePullRequest = vi.fn().mockResolvedValue(true);
      prApproverClient.approvePullRequest = vi.fn().mockResolvedValue(true);

      const result = await processor.process(update, {
        type: 'update_pull_request',
        data: {
          'base-commit-sha': '1234abcd',
          'commit-message': 'Test commit message',
          'pr-body': 'Test body',
          'pr-title': 'Test PR',
          'updated-dependency-files': [],
          'dependency-names': ['dependency1'],
        },
      });

      expect(result).toEqual({ success: true, pr: 11 });
      expect(prAuthorClient.updatePullRequest).toHaveBeenCalled();
      expect(prApproverClient.approvePullRequest).toHaveBeenCalled();
    });

    it('should skip processing "close_pull_request" if "dryRun" is true', async () => {
      taskInputs.dryRun = true;

      const result = await processor.process(update, {
        type: 'close_pull_request',
        data: { 'dependency-names': [] },
      });

      expect(result).toEqual({ success: true });
      expect(prAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
    });

    it('should fail processing "close_pull_request" if pull request does not exist', async () => {
      taskInputs.dryRun = false;

      const result = await processor.process(update, {
        type: 'close_pull_request',
        data: { 'dependency-names': ['dependency1'] },
      });

      expect(result).toEqual({ success: false });
      expect(prAuthorClient.abandonPullRequest).not.toHaveBeenCalled();
    });

    it('should process "close_pull_request"', async () => {
      taskInputs.dryRun = false;
      update.job['package-manager'] = 'npm_and_yarn';
      existingPullRequests.push({
        id: 11,
        properties: [
          { name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER, value: 'npm_and_yarn' },
          {
            name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
            value: JSON.stringify([{ 'dependency-name': 'dependency1' }]),
          },
        ],
      });

      prAuthorClient.abandonPullRequest = vi.fn().mockResolvedValue(true);

      const result = await processor.process(update, {
        type: 'close_pull_request',
        data: { 'dependency-names': ['dependency1'] },
      });

      expect(result).toEqual({ success: true, pr: 11 });
      expect(prAuthorClient.abandonPullRequest).toHaveBeenCalled();
    });

    it('should process "mark_as_processed"', async () => {
      const result = await processor.process(update, { type: 'mark_as_processed', data: {} });
      expect(result).toEqual({ success: true });
    });

    it('should process "record_ecosystem_versions"', async () => {
      const result = await processor.process(update, { type: 'record_ecosystem_versions', data: {} });
      expect(result).toEqual({ success: true });
    });

    it('should process "record_ecosystem_meta"', async () => {
      const result = await processor.process(update, {
        type: 'record_ecosystem_meta',
        data: [{ ecosystem: { name: 'npm_any_yarn' } }],
      });
      expect(result).toEqual({ success: true });
    });

    it('should process "record_update_job_error"', async () => {
      const result = await processor.process(update, {
        type: 'record_update_job_error',
        data: { 'error-type': 'random' },
      });
      expect(result).toEqual({ success: false });
      expect(error).toHaveBeenCalled();
    });

    it('should process "record_update_job_unknown_error"', async () => {
      const result = await processor.process(update, {
        type: 'record_update_job_unknown_error',
        data: { 'error-type': 'random' },
      });
      expect(result).toEqual({ success: false });
      expect(error).toHaveBeenCalled();
    });

    it('should process "increment_metric"', async () => {
      const result = await processor.process(update, {
        type: 'increment_metric',
        data: { metric: 'random' },
      });
      expect(result).toEqual({ success: true });
    });

    it('should handle unknown output type', async () => {
      // @ts-expect-error - trying non existed type
      const result = await processor.process(update, { type: 'non_existant_output_type', expect: { data: {} } });
      expect(result).toEqual({ success: true });
      expect(warning).toHaveBeenCalled();
    });
  });
});

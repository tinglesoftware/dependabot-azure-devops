import { GitPullRequestMergeStrategy, VersionControlChangeType } from 'azure-devops-node-api/interfaces/GitInterfaces';
import { debug, error, warning } from 'azure-pipelines-task-lib/task';
import {
  DependabotExistingGroupPRSchema,
  DependabotExistingPRSchema,
  getBranchNameForUpdate,
  type DependabotClosePullRequest,
  type DependabotCreatePullRequest,
  type DependabotData,
  type DependabotDependency,
  type DependabotExistingGroupPR,
  type DependabotExistingPR,
  type DependabotOperation,
  type DependabotUpdatePullRequest,
} from 'paklo/dependabot';
import * as path from 'path';
import { type AzureDevOpsWebApiClient } from '../azure-devops/client';
import { section } from '../azure-devops/formatting';
import {
  DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
  DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
  type IFileChange,
  type IPullRequestProperties,
} from '../azure-devops/models';
import { type ISharedVariables } from '../utils/shared-variables';

export type DependabotOutputProcessorResult = {
  success: boolean;
  pr?: number;
};

const DependenciesPrPropertySchema = DependabotExistingPRSchema.array().or(DependabotExistingGroupPRSchema);

/**
 * Processes dependabot update outputs using the DevOps API
 */
export class DependabotOutputProcessor {
  private readonly prAuthorClient: AzureDevOpsWebApiClient;
  private readonly prApproverClient: AzureDevOpsWebApiClient | undefined;
  private readonly existingBranchNames: string[] | undefined;
  private readonly existingPullRequests: IPullRequestProperties[];
  private readonly createdPullRequestIds: number[];
  private readonly taskInputs: ISharedVariables;
  private readonly debug: boolean;

  public static PR_DEFAULT_AUTHOR_EMAIL = 'noreply@github.com';
  public static PR_DEFAULT_AUTHOR_NAME = 'dependabot[bot]';

  constructor(
    taskInputs: ISharedVariables,
    prAuthorClient: AzureDevOpsWebApiClient,
    prApproverClient: AzureDevOpsWebApiClient | undefined,
    existingBranchNames: string[] | undefined,
    existingPullRequests: IPullRequestProperties[],
    debug: boolean = false,
  ) {
    this.taskInputs = taskInputs;
    this.prAuthorClient = prAuthorClient;
    this.prApproverClient = prApproverClient;
    this.existingBranchNames = existingBranchNames;
    this.existingPullRequests = existingPullRequests;
    this.createdPullRequestIds = [];
    this.debug = debug;
  }

  /**
   * Process the appropriate DevOps API actions for the supplied dependabot update output
   * @param output A scenario output
   */
  public async process(
    operation: DependabotOperation,
    output: DependabotData,
  ): Promise<DependabotOutputProcessorResult> {
    const { project, repository } = this.taskInputs.url;
    const packageManager = operation.job?.['package-manager'];

    const type = output.type;
    section(`Processing '${type}'`);
    if (this.debug) {
      debug(JSON.stringify(output.data));
    }
    switch (type) {
      // Documentation on the 'data' model for each output type can be found here:
      // See: https://github.com/dependabot/cli/blob/main/internal/model/update.go

      case 'create_pull_request': {
        const title = output.data['pr-title'];
        if (this.taskInputs.dryRun) {
          warning(`Skipping pull request creation of '${title}' as 'dryRun' is set to 'true'`);
          return { success: true };
        }

        // Skip if active pull request limit reached.
        const openPullRequestsLimit = operation.config['open-pull-requests-limit']!;

        // Parse the Dependabot metadata for the existing pull requests that are related to this update
        // Dependabot will use this to determine if we need to create new pull requests or update/close existing ones
        const existingPullRequestsForPackageManager = parsePullRequestProperties(
          this.existingPullRequests,
          packageManager,
        );
        const existingPullRequestsCount = Object.entries(existingPullRequestsForPackageManager).length;
        const openPullRequestsCount = this.createdPullRequestIds.length + existingPullRequestsCount;
        const hasReachedOpenPullRequestLimit =
          openPullRequestsLimit > 0 && openPullRequestsCount >= openPullRequestsLimit;

        if (hasReachedOpenPullRequestLimit) {
          warning(
            `Skipping pull request creation of '${title}' as the open pull requests limit (${openPullRequestsLimit}) has been reached`,
          );
          return { success: true };
        }

        const changedFiles = getPullRequestChangedFilesForOutputData(output.data);
        const dependencies = getPullRequestDependenciesPropertyValueForOutputData(output.data);
        const targetBranch =
          operation.config['target-branch'] || (await this.prAuthorClient.getDefaultBranch(project, repository));
        const sourceBranch = getBranchNameForUpdate(
          operation.config['package-ecosystem'],
          targetBranch,
          operation.config.directory ||
            operation.config.directories?.find((dir) => changedFiles[0]?.path?.startsWith(dir)),
          !Array.isArray(dependencies) ? dependencies['dependency-group-name'] : undefined,
          !Array.isArray(dependencies) ? dependencies.dependencies : dependencies,
          operation.config['pull-request-branch-name']?.separator,
        );

        // Check if the source branch already exists or conflicts with an existing branch
        const existingBranch = this.existingBranchNames?.find((branch) => sourceBranch == branch) || [];
        if (existingBranch.length) {
          error(
            `Unable to create pull request '${title}' as source branch '${sourceBranch}' already exists; Delete the existing branch and try again.`,
          );
          return { success: false };
        }
        const conflictingBranches = this.existingBranchNames?.filter((branch) => sourceBranch.startsWith(branch)) || [];
        if (conflictingBranches.length) {
          error(
            `Unable to create pull request '${title}' as source branch '${sourceBranch}' would conflict with existing branch(es) '${conflictingBranches.join(', ')}'; Delete the conflicting branch(es) and try again.`,
          );
          return { success: false };
        }

        // Create a new pull request
        const newPullRequestId = await this.prAuthorClient.createPullRequest({
          project: project,
          repository: repository,
          source: {
            commit: output.data['base-commit-sha'] || operation.job.source.commit!,
            branch: sourceBranch,
          },
          target: {
            branch: targetBranch!,
          },
          author: {
            email: this.taskInputs.authorEmail || DependabotOutputProcessor.PR_DEFAULT_AUTHOR_EMAIL,
            name: this.taskInputs.authorName || DependabotOutputProcessor.PR_DEFAULT_AUTHOR_NAME,
          },
          title: title,
          description: getPullRequestDescription(
            packageManager,
            output.data['pr-body'],
            output.data.dependencies,
          ),
          commitMessage: output.data['commit-message'],
          autoComplete: this.taskInputs.setAutoComplete
            ? {
                ignorePolicyConfigIds: this.taskInputs.autoCompleteIgnoreConfigIds,
                mergeStrategy: (() => {
                  switch (this.taskInputs.mergeStrategy) {
                    case 'noFastForward':
                      return GitPullRequestMergeStrategy.NoFastForward;
                    case 'squash':
                      return GitPullRequestMergeStrategy.Squash;
                    case 'rebase':
                      return GitPullRequestMergeStrategy.Rebase;
                    case 'rebaseMerge':
                      return GitPullRequestMergeStrategy.RebaseMerge;
                    default:
                      return GitPullRequestMergeStrategy.Squash;
                  }
                })(),
              }
            : undefined,
          assignees: operation.config.assignees,
          labels: operation.config.labels?.map((label) => label?.trim()) || [],
          workItems: operation.config.milestone ? [operation.config.milestone] : [],
          changes: changedFiles,
          properties: buildPullRequestProperties(packageManager, dependencies),
        });

        // Auto-approve the pull request, if required
        if (this.taskInputs.autoApprove && this.prApproverClient && newPullRequestId) {
          await this.prApproverClient.approvePullRequest({
            project: project,
            repository: repository,
            pullRequestId: newPullRequestId,
          });
        }

        // Store the new pull request ID, so we can keep track of the total number of open pull requests
        if (newPullRequestId && newPullRequestId > 0) {
          this.createdPullRequestIds.push(newPullRequestId);
          return { success: true, pr: newPullRequestId };
        } else {
          return { success: false };
        }
      }

      case 'update_pull_request': {
        if (this.taskInputs.dryRun) {
          warning(`Skipping pull request update as 'dryRun' is set to 'true'`);
          return { success: true };
        }

        // Find the pull request to update
        const pullRequestToUpdate = this.getPullRequestForDependencyNames(
          packageManager,
          output.data['dependency-names'],
        );
        if (!pullRequestToUpdate) {
          error(
            `Could not find pull request to update for package manager '${packageManager}' with dependencies '${output.data['dependency-names'].join(', ')}'`,
          );
          return { success: false };
        }

        // Update the pull request
        const pullRequestWasUpdated = await this.prAuthorClient.updatePullRequest({
          project: project,
          repository: repository,
          pullRequestId: pullRequestToUpdate.id,
          commit: output.data['base-commit-sha'] || operation.job.source.commit!,
          author: {
            email: this.taskInputs.authorEmail || DependabotOutputProcessor.PR_DEFAULT_AUTHOR_EMAIL,
            name: this.taskInputs.authorName || DependabotOutputProcessor.PR_DEFAULT_AUTHOR_NAME,
          },
          changes: getPullRequestChangedFilesForOutputData(output.data),
          skipIfDraft: true,
          skipIfCommitsFromAuthorsOtherThan:
            this.taskInputs.authorEmail || DependabotOutputProcessor.PR_DEFAULT_AUTHOR_EMAIL,
          skipIfNotBehindTargetBranch: true,
        });

        // Re-approve the pull request, if required
        if (this.taskInputs.autoApprove && this.prApproverClient && pullRequestWasUpdated) {
          await this.prApproverClient.approvePullRequest({
            project: project,
            repository: repository,
            pullRequestId: pullRequestToUpdate.id,
          });
        }

        return { success: pullRequestWasUpdated, pr: pullRequestToUpdate.id };
      }

      case 'close_pull_request': {
        if (this.taskInputs.dryRun) {
          warning(`Skipping pull request closure as 'dryRun' is set to 'true'`);
          return { success: true };
        }

        // Find the pull request to close
        const pullRequestToClose = this.getPullRequestForDependencyNames(
          packageManager,
          output.data['dependency-names'],
        );
        if (!pullRequestToClose) {
          error(
            `Could not find pull request to close for package manager '${packageManager}' with dependencies '${output.data['dependency-names'].join(', ')}'`,
          );
          return { success: false };
        }

        // TODO: GitHub Dependabot will close with reason "Superseded by ${new_pull_request_id}" when another PR supersedes it.
        //       How do we detect this? Do we need to?

        // Close the pull request
        const success = await this.prAuthorClient.abandonPullRequest({
          project: project,
          repository: repository,
          pullRequestId: pullRequestToClose.id,
          comment: getPullRequestCloseReasonForOutputData(output.data),
          deleteSourceBranch: true,
        });
        return { success, pr: pullRequestToClose.id };
      }

      case 'update_dependency_list':
        // No action required
        return { success: true };

      case 'mark_as_processed':
        // No action required
        return { success: true };

      case 'record_ecosystem_versions':
        // No action required
        return { success: true };

      case 'record_ecosystem_meta':
        // No action required
        return { success: true };

      case 'record_update_job_error':
        error(
          `Update job error: ${output.data['error-type']} ${JSON.stringify(output.data['error-details'])}`,
        );
        return { success: false };

      case 'record_update_job_unknown_error':
        error(
          `Update job unknown error: ${output.data['error-type']}, ${JSON.stringify(output.data['error-details'])}`,
        );
        return { success: false };

      case 'increment_metric':
        // No action required
        return { success: true };

      default:
        warning(`Unknown dependabot output type '${type}', ignoring...`);
        return { success: true };
    }
  }

  private getPullRequestForDependencyNames(
    packageManager: string,
    dependencyNames: string[],
  ): IPullRequestProperties | undefined {
    return this.existingPullRequests.find((pr) => {
      return (
        pr.properties?.find(
          (p) => p.name === DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER && p.value === packageManager,
        ) &&
        pr.properties?.find(
          (p) =>
            p.name === DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES &&
            areEqual(getDependencyNames(DependenciesPrPropertySchema.parse(JSON.parse(p.value))), dependencyNames),
        )
      );
    });
  }
}

export function buildPullRequestProperties(
  packageManager: string,
  dependencies: DependabotExistingPR[] | DependabotExistingGroupPR,
) {
  return [
    {
      name: DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER,
      value: packageManager,
    },
    {
      name: DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES,
      value: JSON.stringify(dependencies),
    },
  ];
}

export function parsePullRequestProperties(
  pullRequests: IPullRequestProperties[],
  packageManager: string | null,
): Record<string, DependabotExistingPR[] | DependabotExistingGroupPR> {
  return Object.fromEntries(
    pullRequests
      .filter((pr) => {
        return pr.properties?.find(
          (p) =>
            p.name === DEVOPS_PR_PROPERTY_DEPENDABOT_PACKAGE_MANAGER &&
            (packageManager === null || p.value === packageManager),
        );
      })
      .map((pr) => {
        return [
          pr.id,
          DependenciesPrPropertySchema.parse(
            JSON.parse(pr.properties!.find((p) => p.name === DEVOPS_PR_PROPERTY_DEPENDABOT_DEPENDENCIES)!.value),
          ),
        ];
      }),
  );
}

function getPullRequestChangedFilesForOutputData(
  data: DependabotCreatePullRequest | DependabotUpdatePullRequest,
): IFileChange[] {
  return data['updated-dependency-files']
    .filter((file) => file.type === 'file')
    .map((file) => {
      let changeType = VersionControlChangeType.None;
      if (file.deleted === true) {
        changeType = VersionControlChangeType.Delete;
      } else if (file.operation === 'update') {
        changeType = VersionControlChangeType.Edit;
      } else {
        changeType = VersionControlChangeType.Add;
      }
      return {
        changeType: changeType,
        path: path.join(file.directory, file.name),
        content: file.content,
        encoding: file.content_encoding ?? 'utf8',
      } satisfies IFileChange;
    });
}

function getPullRequestCloseReasonForOutputData(data: DependabotClosePullRequest): string | undefined {
  // The first dependency is the "lead" dependency in a multi-dependency update
  const leadDependencyName = data['dependency-names'][0];
  let reason: string | undefined;
  switch (data.reason) {
    case 'dependencies_changed':
      reason = `Looks like the dependencies have changed`;
      break;
    case 'dependency_group_empty':
      reason = `Looks like the dependencies in this group are now empty`;
      break;
    case 'dependency_removed':
      reason = `Looks like ${leadDependencyName} is no longer a dependency`;
      break;
    case 'up_to_date':
      reason = `Looks like ${leadDependencyName} is up-to-date now`;
      break;
    case 'update_no_longer_possible':
      reason = `Looks like ${leadDependencyName} can no longer be updated`;
      break;
  }
  if (reason && reason.length > 0) {
    reason += ', so this is no longer needed.';
  }
  return reason;
}

function getPullRequestDependenciesPropertyValueForOutputData(
  data: DependabotCreatePullRequest,
): DependabotExistingPR[] | DependabotExistingGroupPR {
  const dependencies = data.dependencies?.map((dep) => {
    return {
      'dependency-name': dep.name,
      'dependency-version': dep.version,
      'directory': dep.directory,
    };
  });
  const dependencyGroupName = data['dependency-group']?.name;
  if (!dependencyGroupName) return dependencies;
  return {
    'dependency-group-name': dependencyGroupName,
    'dependencies': dependencies,
  } as DependabotExistingGroupPR;
}

function getDependencyNames(dependencies: DependabotExistingPR[] | DependabotExistingGroupPR): string[] {
  const deps = Array.isArray(dependencies) ? dependencies : dependencies.dependencies;
  return deps.map((dep) => dep['dependency-name']?.toString());
}

function areEqual(a: string[], b: string[]): boolean {
  if (a.length !== b.length) return false;
  return a.every((name) => b.includes(name));
}

function getPullRequestDescription(
  packageManager: string,
  body: string | null | undefined,
  dependencies: DependabotDependency[],
): string {
  let header = '';
  const footer = '';

  // Fix up GitHub mentions encoding issues by removing instances of the zero-width space '\u200B' as it does not render correctly in Azure DevOps.
  // https://github.com/dependabot/dependabot-core/issues/9572
  // https://github.com/dependabot/dependabot-core/blob/313fcff149b3126cb78b38d15f018907d729f8cc/common/lib/dependabot/pull_request_creator/message_builder/link_and_mention_sanitizer.rb#L245-L252
  const description = (body || '').replace(new RegExp(decodeURIComponent('%EF%BF%BD%EF%BF%BD%EF%BF%BD'), 'g'), '');

  // If there is exactly one dependency, add a compatibility score badge to the description header.
  // Compatibility scores are intended for single dependency security updates, not group updates.
  // https://docs.github.com/en/github/managing-security-vulnerabilities/about-dependabot-security-updates#about-compatibility-scores
  if (dependencies.length === 1) {
    const compatibilityScoreBadges = dependencies.map((dep) => {
      return `![Dependabot compatibility score](https://dependabot-badges.githubapp.com/badges/compatibility_score?dependency-name=${dep.name}&package-manager=${packageManager}&previous-version=${dep['previous-version']}&new-version=${dep.version})`;
    });
    header += compatibilityScoreBadges.join(' ') + '\n\n';
  }

  // Build the full pull request description.
  // The header/footer must not be truncated. If the description is too long, we truncate the body.
  const maxDescriptionLength = 4000;
  const maxDescriptionLengthAfterHeaderAndFooter = maxDescriptionLength - header.length - footer.length;
  return `${header}${description.substring(0, maxDescriptionLengthAfterHeaderAndFooter)}${footer}`;
}

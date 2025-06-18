import axios from 'axios';
import * as tl from 'azure-pipelines-task-lib/task';
import { getVariable } from 'azure-pipelines-task-lib/task';
import { existsSync } from 'fs';
import { readFile } from 'fs/promises';
import { parseDependabotConfig, POSSIBLE_CONFIG_FILE_PATHS, type DependabotConfig } from 'paklo/dependabot';
import * as path from 'path';
import { type ISharedVariables } from '../utils/shared-variables';

/**
 * Parse the dependabot config YAML file to specify update configuration.
 * The file should be located at any of POSSIBLE_CONFIG_FILE_PATHS.
 *
 * To view YAML file format, visit
 * https://docs.github.com/en/github/administering-a-repository/configuration-options-for-dependency-updates#allow
 *
 * @param taskInputs the input variables of the task
 * @returns {DependabotConfig} config - the dependabot configuration
 */
export async function getDependabotConfig(taskInputs: ISharedVariables): Promise<DependabotConfig> {
  let configPath: undefined | string;
  let configContents: undefined | string;

  /*
   * The configuration file can be available locally if the repository is cloned.
   * Otherwise, we should get it via the API which supports 2 scenarios:
   * 1. Running the pipeline without cloning, which is useful for huge repositories (multiple submodules or large commit log)
   * 2. Running a single pipeline to update multiple repositories https://github.com/mburumaxwell/dependabot-azure-devops/issues/328
   */
  if (taskInputs.repositoryOverridden) {
    tl.debug(`Attempting to fetch configuration file via REST API ...`);
    for (const fp of POSSIBLE_CONFIG_FILE_PATHS) {
      // make HTTP request
      const url = `${taskInputs.url.url}${taskInputs.url.project}/_apis/git/repositories/${taskInputs.url.repository}/items?path=/${fp}`;
      tl.debug(`GET ${url}`);

      try {
        const response = await axios.get(url, {
          auth: {
            username: 'x-access-token',
            password: taskInputs.systemAccessToken,
          },
          headers: {
            Accept: '*/*', // Gotcha!!! without this SH*T fails terribly
          },
        });
        if (response.status === 200) {
          tl.debug(`Found configuration file at '${url}'`);
          configContents = response.data;
          configPath = fp;
          break;
        }
      } catch (error) {
        if (axios.isAxiosError(error)) {
          const responseStatusCode = error?.response?.status;

          if (responseStatusCode === 404) {
            tl.debug(`No configuration file at '${url}'`);
            continue;
          } else if (responseStatusCode === 401) {
            throw new Error(`No access token has been provided to access '${url}'`);
          } else if (responseStatusCode === 403) {
            throw new Error(`The access token provided does not have permissions to access '${url}'`);
          }
        } else {
          throw error;
        }
      }
    }
  } else {
    const rootDir = getVariable('Build.SourcesDirectory')!;
    for (const fp of POSSIBLE_CONFIG_FILE_PATHS) {
      const filePath = path.join(rootDir, fp);
      if (existsSync(filePath)) {
        tl.debug(`Found configuration file cloned at ${filePath}`);
        configContents = await readFile(filePath, 'utf-8');
        configPath = filePath;
        break;
      } else {
        tl.debug(`No configuration file cloned at ${filePath}`);
      }
    }
  }

  // Ensure we have file contents. Otherwise throw a well readable error.
  if (!configContents || !configPath || typeof configContents !== 'string') {
    throw new Error(`Configuration file not found at possible locations: ${POSSIBLE_CONFIG_FILE_PATHS.join(', ')}`);
  } else {
    tl.debug('Configuration file contents read.');
  }

  return await parseDependabotConfig({ configContents, configPath, variableFinder: getVariable });
}

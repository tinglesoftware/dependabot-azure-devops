import { Command } from 'commander';
import * as yaml from 'js-yaml';
import { existsSync } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { stdin, stdout } from 'node:process';
import readline from 'node:readline/promises';
import { z } from 'zod/v4';

import { extractUrlParts } from '@/azure';
import {
  DEFAULT_EXPERIMENTS,
  DependabotJobBuilder,
  parseDependabotConfig,
  POSSIBLE_CONFIG_FILE_PATHS,
  type DependabotOperation,
} from '@/dependabot';
import { logger } from '../logger';
import { handlerOptions, type HandlerOptions } from './base';

const schema = z.object({
  organisationUrl: z.string(),
  project: z.string(),
  repository: z.string(),
  file: z.string().optional(),
  gitToken: z.string().optional(),
  githubToken: z.string().optional(),
  forListDependencies: z.boolean(),
  pullRequestId: z.coerce.string().optional(),
  outDir: z.string(),
});
type Options = z.infer<typeof schema>;

async function handler({ options, error }: HandlerOptions<Options>) {
  let { organisationUrl, file: configPath } = options;
  const { githubToken, gitToken, project, repository, forListDependencies, pullRequestId, outDir } = options;

  // extract url parts
  if (!organisationUrl.endsWith('/')) organisationUrl = `${organisationUrl}/`; // without trailing slash the extraction fails
  const url = extractUrlParts({ organisationUrl, project, repository });

  // TODO: change this to pull from REST API!
  // if we have no file, attempt to check against known paths
  if (!configPath) {
    logger.trace('file not specified searching under known possible paths');
    for (const fp of POSSIBLE_CONFIG_FILE_PATHS) {
      if (existsSync(fp)) {
        configPath = fp;
        break;
      }
    }

    // if we still have no path, throw an exception
    if (!configPath) {
      error(`Configuration file not found at possible locations:\n${POSSIBLE_CONFIG_FILE_PATHS.join(',\n')}`);
      return;
    }
  }

  // if the file does not exist, there is nothing to do
  if (!existsSync(configPath)) {
    error(`Configuration file '${configPath}' does not exist`);
    return;
  }

  // load the file contents and parse
  logger.info(`Using config file at ${configPath}`);
  const configContents = await readFile(configPath, 'utf-8');
  const variables = new Map<string, string>();
  const rl = readline.createInterface({ input: stdin, output: stdout });
  async function variableFinder(name: string) {
    if (variables.has(name)) return variables.get(name);
    logger.trace(`Asking value for variable named: ${name}`);
    const value = await rl.question(`Please provide the value for '${name}': `);
    variables.set(name, value);
    return value;
  }
  const config = await parseDependabotConfig({ configContents, configPath, variableFinder });
  rl.close();
  logger.info(
    `Configuration file valid: ${config.updates.length} update(s) and ${config.registries?.length ?? 'no'} registries.`,
  );

  // create output directory if it does not exist
  if (!existsSync(outDir)) await mkdir(outDir, { recursive: true });

  const updates = config.updates;
  for (const update of updates) {
    const updateId = updates.indexOf(update).toString();

    const builder = new DependabotJobBuilder({
      source: { provider: 'azure', ...url },
      config,
      update,
      systemAccessToken: gitToken,
      githubToken,
      experiments: DEFAULT_EXPERIMENTS,
      debug: false,
    });

    let operation: DependabotOperation | undefined = undefined;
    if (forListDependencies) {
      operation = builder.forDependenciesList({
        id: `discover-${updateId}-${update['package-ecosystem']}-dependency-list`,
      });
    } else {
      // TODO: complete this once we have abstracted out a way to get existing PRs and security vulnerabilities into something reusable
      // if (pullRequestId) {
      //   operation = builder.forUpdate({
      //     id: `update-pr-${pullRequestId}`
      //     existingPullRequests: existingPullRequestDependenciesForPackageManager,
      //     pullRequestToUpdate: existingPullRequestsForPackageManager[pullRequestId]!,
      //     securityVulnerabilities,
      //   });
      // } else {
      //   operation = builder.forUpdate({
      //     id: `update-${updateId}-${update['package-ecosystem']}-${securityUpdatesOnly ? 'security-only' : 'all'}`,
      //     dependencyNamesToUpdate,
      //     existingPullRequests: existingPullRequestDependenciesForPackageManager,
      //     securityVulnerabilities,
      //   });
      // }

      error('This has not been implemented yet. Sorry');
      return;
    }

    const contents = yaml.dump({
      job: operation.job,
      credentials: operation.credentials,
    });
    logger.trace(`JobConfig:\r\n${contents}`);
    const outputPath = join(outDir, `${operation.job.id}.yaml`);
    await writeFile(outputPath, contents);
  }
}

export const command = new Command('generate')
  .description('Generate dependabot job file(s) from a configuration file.')
  .argument(
    '<organisation-url>',
    'URL of the organisation e.g. https://dev.azure.com/my-org or https://my-org.visualstudio.com or http://my-org.com:8443/tfs',
  )
  .argument('<project>', 'Name or ID of the project')
  .argument('<repository>', 'Name or ID of the repository')
  .option('--git-token <GIT-TOKEN>', 'Token to use for authenticating access to the git repository.')
  .option(
    '-f, --file <FILE>',
    'Configuration file to validate.\nIf not specified, the command will search for one in the current directory.',
  )
  .option(
    '--github-token <GITHUB-TOKEN>',
    'GitHub token to use for authentication. If not specified, you may get rate limited.',
  )
  .option('--for-list-dependencies', 'Whether to only generate the job for listing dependencies.', false)
  .option(
    '--pull-request-id <PULL-REQUEST-ID>',
    'Identifier of pull request to update. If not specified, a job that updates everything is generated.',
  )
  .option('--out-dir', 'Output directory. If not specified, defaults to "dependabot-jobs".', 'dependabot-jobs')
  .action(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    async (...args: any[]) =>
      await handler(
        await handlerOptions({
          schema,
          input: {
            organisationUrl: args[0],
            project: args[1],
            repository: args[2],
            ...args[3],
          },
          command: args.at(-1),
        }),
      ),
  );

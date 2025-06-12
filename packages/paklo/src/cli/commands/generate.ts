import { Command } from 'commander';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { stdin, stdout } from 'node:process';
import readline from 'node:readline/promises';
import { z } from 'zod/v4';

import { parseDependabotConfig, POSSIBLE_CONFIG_FILE_PATHS } from '@/dependabot';
import { logger } from '../logger';
import { handlerOptions, type HandlerOptions } from './base';

const schema = z.object({
  file: z.string().optional(),
  githubToken: z.string().optional(),
});
type Options = z.infer<typeof schema>;

async function handler({ options, error }: HandlerOptions<Options>) {
  let { file: configPath } = options;

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
    const value = await rl.question(`Please provide the value for '${name}'`);
    variables.set(name, value);
    return value;
  }
  rl.close();
  const config = await parseDependabotConfig({ configContents, configPath, variableFinder });
  logger.info(
    `Configuration file valid: ${config.updates.length} update(s) and ${config.registries?.length ?? 'no'} registries.`,
  );
  error(`This command is not yet fully implemented`);
}

export const command = new Command('generate')
  .description('Generate dependabot job file(s) from a configuration file.')
  .option(
    '-f, --file <FILE>',
    'Configuration file to validate.\nIf not specified, the command will search for one in the current directory.',
  )
  .option(
    '--github-token <GITHUB-TOKEN>',
    'GitHub token to use for authentication. If not specified, you may get rate limited.',
  )
  .action(async (input, command) => await handler(await handlerOptions({ schema, input, command })));

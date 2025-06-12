import { Command } from 'commander';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { z } from 'zod/v4';

import { POSSIBLE_CONFIG_FILE_PATHS, parseDependabotConfig } from '@/dependabot';
import { logger } from '../logger';
import { handlerOptions, type HandlerOptions } from './base';

const schema = z.object({
  file: z.string().optional(),
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

  // load the file contents and validate
  logger.info(`Validating file at ${configPath}`);
  const configContents = await readFile(configPath, 'utf-8');
  const variables = new Set<string>();
  function variableFinder(name: string) {
    variables.add(name);
    return undefined;
  }
  const config = await parseDependabotConfig({ configContents, configPath, variableFinder });
  logger.info(
    `Configuration file valid: ${config.updates.length} update(s) and ${config.registries?.length ?? 'no'} registries.`,
  );
  if (variables.size) {
    logger.info(`Found replaceable variables/tokens:\n- ${variables.values().toArray().join('\n- ')}`);
  } else {
    logger.info('No replaceable variables/tokens found.');
  }
}

export const command = new Command('validate')
  .description('Validate a dependabot configuration file.')
  .option(
    '-f, --file <FILE>',
    'Configuration file to validate.\nIf not specified, the command will search for one in the current directory.',
  )
  .action(async (input, command) => await handler(await handlerOptions({ schema, input, command })));

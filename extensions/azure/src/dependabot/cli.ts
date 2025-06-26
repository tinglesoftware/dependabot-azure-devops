import { command, debug, error, tool, which, getVariable } from 'azure-pipelines-task-lib/task';
import { type ToolRunner } from 'azure-pipelines-task-lib/toolrunner';
import { closeSync, createReadStream, createWriteStream, existsSync, openSync } from 'fs';
import { rename, rm, stat, writeFile } from 'fs/promises';
import * as yaml from 'js-yaml';
import * as os from 'os';
import {
  type DependabotData,
  type DependabotInput,
  type DependabotOperation,
  type DependabotOperationResult,
  DependabotDataSchema,
} from 'paklo/dependabot';
import * as path from 'path';
import { Writable } from 'stream';
import { endgroup, group, section } from '../azure-devops/formatting';
import { type DependabotOutputProcessor } from './output-processor';
import * as readline from 'readline';

export type DependabotCliOptions = {
  sourceProvider?: string;
  sourceLocalPath?: string;
  azureDevOpsAccessToken?: string;
  gitHubAccessToken?: string;
  collectorImage?: string;
  collectorConfigPath?: string;
  proxyCertPath?: string;
  proxyImage?: string;
  updaterImage?: string;
  timeoutDurationMinutes?: number;
  flamegraph?: boolean;
  apiUrl?: string;
  apiListeningPort?: string;
};

/**
 * Wrapper class for running updates using dependabot-cli
 */
export class DependabotCli {
  private readonly jobsPath: string;
  private readonly toolPackage: string;
  private readonly outputProcessor: DependabotOutputProcessor;
  private readonly debug: boolean;
  private readonly outputLogStream: Writable;
  private toolPath?: string;

  public static readonly CLI_PACKAGE_LATEST = 'github.com/dependabot/cli/cmd/dependabot@latest';

  constructor(toolPackage: string, outputProcessor: DependabotOutputProcessor, debug: boolean = false) {
    this.jobsPath = getVariable('Build.SourcesDirectory')!;
    this.toolPackage = toolPackage;
    this.outputProcessor = outputProcessor;
    this.outputLogStream = new Writable({
      write: (chunk, encoding, callback) => logComponentOutput(debug, chunk, encoding, callback)
    });
    this.debug = debug;
  }

  /**
   * Run dependabot update job
   * @param operation
   * @param options
   * @returns
   */
  public async update(
    operation: DependabotOperation,
    options?: DependabotCliOptions,
  ): Promise<DependabotOperationResult[] | undefined> {
    try {
      group(`Job '${operation.job.id}'`);

      // Find the dependabot tool path, or install it if missing
      const dependabotPath = await this.getDependabotToolPath();

      // Set job files
      const jobId = operation.job.id!;
      const jobInputPath =  `${this.jobsPath}-${jobId.toString()}-job.yaml`;
      const jobOutputPath = `${this.jobsPath}-${jobId.toString()}-result.jsonl`;
      this.ensureFileExists(jobInputPath);

      // Compile dependabot cmd arguments
      // See: https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/root.go
      //      https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/update.go
      const dependabotArguments = ['update', '--file', jobInputPath];
      if (options?.sourceProvider) {
        dependabotArguments.push('--provider', options.sourceProvider);
      }
      if (options?.sourceLocalPath && existsSync(options.sourceLocalPath)) {
        dependabotArguments.push('--local', options.sourceLocalPath);
      }
      if (options?.collectorImage) {
        dependabotArguments.push('--collector-image', options.collectorImage);
      }
      if (options?.collectorConfigPath && existsSync(options.collectorConfigPath)) {
        dependabotArguments.push('--collector-config', options.collectorConfigPath);
      }
      if (options?.proxyCertPath && existsSync(options.proxyCertPath)) {
        dependabotArguments.push('--proxy-cert', options.proxyCertPath);
      }
      if (options?.proxyImage) {
        dependabotArguments.push('--proxy-image', options.proxyImage);
      }
      if (options?.updaterImage) {
        // If the updater image is provided but does not contain the "{ecosystem}" placeholder, tell the user they've misconfigured it
        if (!options.updaterImage.includes('{ecosystem}')) {
          throw new Error(
            `Dependabot Updater image '${options.updaterImage}' is invalid. ` +
              `Please ensure the image contains a "{ecosystem}" placeholder to denote the package ecosystem; e.g. "ghcr.io/dependabot/dependabot-updater-{ecosystem}:latest"`,
          );
        }
        dependabotArguments.push(
          '--updater-image',
          options.updaterImage.replace(/\{ecosystem\}/i, operation.config['package-ecosystem']),
        );
      }
      if (options?.timeoutDurationMinutes) {
        dependabotArguments.push('--timeout', `${options.timeoutDurationMinutes}m`);
      }
      if (options?.flamegraph) {
        dependabotArguments.push('--flamegraph');
      }
      if (options?.apiUrl) {
        dependabotArguments.push('--api-url', options.apiUrl);
      }
      // do not add debug here because the CLI hangs when --debug is passed (i.e. it becomes interactive)

      // Generate the job input file
      await writeJobConfigFile(jobInputPath, operation);

      // Run dependabot update
      section(`Processing job from '${jobInputPath}'`);
      // By default, tool(...) uses the environment variables in the current process if none are provided.
      // We need to additional variables for the dependabot CLI hence we extend them which overrides any defaults.
      // See: https://github.com/microsoft/azure-pipelines-task-lib/blob/740206eb72342b22d2ba545204827636eb7b7126/node/toolrunner.ts#L26-L27
      //      https://github.com/microsoft/azure-pipelines-task-lib/blob/740206eb72342b22d2ba545204827636eb7b7126/node/toolrunner.ts#L570
      //      https://github.com/mburumaxwell/dependabot-azure-devops/pull/1753#issuecomment-2944939628
      const env: Record<string, string | undefined> = {
        ...process.env,

        // additional ENV
        DEPENDABOT_JOB_ID: jobId.replace(/-/g, '_'), // replace hyphens with underscores
        LOCAL_GITHUB_ACCESS_TOKEN: options?.gitHubAccessToken, // avoid rate-limiting when pulling images from GitHub container registries
        LOCAL_AZURE_ACCESS_TOKEN: options?.azureDevOpsAccessToken, // technically not needed since we already supply this in our 'git_source' registry, but included for consistency
        FAKE_API_PORT: options?.apiListeningPort, // used to pin PORT of the Dependabot CLI api back-channel
      };
      const dependabotTool = tool(dependabotPath).arg(dependabotArguments);
      const jobOutputStream = createWriteStream(jobOutputPath, { flush: true } );
      dependabotTool.on('stdout', (buffer: Buffer) => jobOutputStream.write(buffer));
      const dependabotResultCode = await dependabotTool.execAsync({
        outStream: this.outputLogStream,
        errStream: this.outputLogStream,
        ignoreReturnCode: true,
        failOnStdErr: false,
        env: env,
      });
      jobOutputStream.end();
      if (dependabotResultCode != 0) {
        error(`Dependabot failed with exit code ${dependabotResultCode}`);
        return [{ success: false }];
      }

      // If flamegraph is enabled, upload the report to the pipeline timeline so the use can download it
      const flamegraphPath = path.join(process.cwd(), 'flamegraph.html');
      if (options?.flamegraph && existsSync(flamegraphPath)) {
        section(`Processing Dependabot flame graph report`);
        const jobFlamegraphPath = path.join(process.cwd(), `dependabot-${operation.job.id}-flamegraph.html`);
        rename(flamegraphPath, jobFlamegraphPath);
        command('task.uploadfile', {}, jobFlamegraphPath);
      }

      // Process the job output
      const operationResults = Array<DependabotOperationResult>();
      if ((await stat(jobOutputPath))?.size != 0) {
        const jobOutputs = await readDependabotDataFile(jobOutputPath);
        if (jobOutputs?.length > 0) {
          section(`Processing job outputs from '${jobOutputPath}'`);
          for (const output of jobOutputs) {
            // Documentation on the scenario model can be found here:
            // https://github.com/dependabot/cli/blob/main/internal/model/scenario.go
            const operationResult: DependabotOperationResult = { success: true, output };
            try {
              const { success, pr } = await this.outputProcessor.process(operation, output);
              operationResult.success = success;
              operationResult.pr = pr;
            } catch (e) {
              const err = e as Error;
              operationResult.success = false;
              operationResult.error = err;
              error(`An unhandled exception occurred while processing '${output.type}': ${e}`);
              console.debug(e); // Dump the stack trace to help with debugging
            } finally {
              operationResults.push(operationResult);
            }
          }
        }
      }

      return operationResults.length > 0 ? operationResults : undefined;
    } finally {
      endgroup();
    }
  }

  // Get the dependabot tool path and install if missing
  private async getDependabotToolPath(installIfMissing: boolean = true): Promise<string> {
    debug('Checking for `dependabot` install...');
    this.toolPath ||= which('dependabot', false);
    if (this.toolPath) {
      return this.toolPath;
    }
    if (!installIfMissing) {
      throw new Error('Dependabot CLI install not found');
    }

    debug('Dependabot CLI install was not found, installing now with `go install dependabot`...');
    section('Installing Dependabot CLI');
    const goTool: ToolRunner = tool(which('go', true));
    goTool.arg(['install', this.toolPackage]);
    await goTool.execAsync();

    // Depending on how Go is configured on the host agent, the "go/bin" path may not be in the PATH environment variable.
    // If dependabot still cannot be found using `which()` after install, we must manually resolve the path;
    // It will either be "$GOPATH/bin/dependabot" or "$HOME/go/bin/dependabot", if GOPATH is not set.
    const goBinPath = process.env.GOPATH ? path.join(process.env.GOPATH, 'bin') : path.join(os.homedir(), 'go', 'bin');
    return (this.toolPath ||= which('dependabot', false) || path.join(goBinPath, 'dependabot'));
  }

  // Ensure the file exists and is empty, creating it if necessary
  private ensureFileExists(filepath: string): void {
    closeSync(openSync(filepath, 'w'));
  }

  // Clean up the jobs directory and its contents
  public cleanup(): void {
    if (existsSync(this.jobsPath)) {
      rm(this.jobsPath, {
        recursive: true,
        force: true,
      });
    }
  }
}

// Documentation on the job model can be found here:
// https://github.com/dependabot/cli/blob/main/internal/model/job.go
async function writeJobConfigFile(path: string, input: DependabotInput): Promise<void> {
  const contents = yaml.dump(
    {
      job: input.job,
      credentials: input.credentials,
    },
    { noRefs: true /* Dereference objects that may be repeated */ },
  );
  debug(`JobConfig:\r\n${contents}`);
  await writeFile(path, contents);
}

async function readDependabotDataFile(path: string): Promise<DependabotData[]> {
  if ((await stat(path))?.size <= 0) {
    return []; // No outputs or failed job
  }

  // create a readline interface for reading the file line by line
  const rl = readline.createInterface({
    input: createReadStream(path, { encoding: 'utf-8' }),
    crlfDelay: Infinity
  });

  const outputArray : DependabotData[] = [];
  for await (const line of rl) {
    const json = JSON.parse(line);
    const output = await DependabotDataSchema.parseAsync(json);
    outputArray.push(output);
  }

  return outputArray;
}

// Log output from Dependabot based on the sub-component it originates from
function logComponentOutput(
  verbose: boolean,
  chunk: any, // eslint-disable-line @typescript-eslint/no-explicit-any
  encoding: BufferEncoding,
  callback: (error?: Error | null) => void,
): void {
  chunk
    .toString()
    .split('\n')
    .map((line: string) => line.trim())
    .filter((line: string) => line)
    .forEach((line: string) => {
      const component = line.split('|')?.[0]?.trim();
      switch (component) {
        // Don't log highly verbose components that are not useful to the user, unless debugging
        case 'collector':
        case 'proxy':
          if (verbose) {
            debug(line);
          }
          break;

        // Log output from all other components
        default:
          console.info(line);
          break;
      }
    });
  callback();
}

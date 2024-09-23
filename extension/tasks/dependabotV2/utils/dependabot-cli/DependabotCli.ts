import { debug, error, tool, which } from 'azure-pipelines-task-lib/task';
import { ToolRunner } from 'azure-pipelines-task-lib/toolrunner';
import * as fs from 'fs';
import * as yaml from 'js-yaml';
import * as os from 'os';
import * as path from 'path';
import { IDependabotUpdateJobConfig } from './interfaces/IDependabotUpdateJobConfig';
import { IDependabotUpdateOperation } from './interfaces/IDependabotUpdateOperation';
import { IDependabotUpdateOperationResult } from './interfaces/IDependabotUpdateOperationResult';
import { IDependabotUpdateOutputProcessor } from './interfaces/IDependabotUpdateOutputProcessor';

/**
 * Wrapper class for running updates using dependabot-cli
 */
export class DependabotCli {
  private readonly jobsPath: string;
  private readonly toolImage: string;
  private readonly outputProcessor: IDependabotUpdateOutputProcessor;
  private readonly debug: boolean;

  private toolPath: string;

  public static readonly CLI_IMAGE_LATEST = 'github.com/dependabot/cli/cmd/dependabot@latest';

  constructor(cliToolImage: string, outputProcessor: IDependabotUpdateOutputProcessor, debug: boolean) {
    this.jobsPath = path.join(os.tmpdir(), 'dependabot-jobs');
    this.toolImage = cliToolImage;
    this.outputProcessor = outputProcessor;
    this.debug = debug;
    this.ensureJobsPathExists();
  }

  /**
   * Run dependabot update job
   * @param operation
   * @param options
   * @returns
   */
  public async update(
    operation: IDependabotUpdateOperation,
    options?: {
      gitHubAccessToken?: string;
      collectorImage?: string;
      proxyImage?: string;
      updaterImage?: string;
    },
  ): Promise<IDependabotUpdateOperationResult[] | undefined> {
    // Find the dependabot tool path, or install it if missing
    const dependabotPath = await this.getDependabotToolPath();

    // Create the job directory
    const jobId = operation.job.id;
    const jobPath = path.join(this.jobsPath, jobId.toString());
    const jobInputPath = path.join(jobPath, 'job.yaml');
    const jobOutputPath = path.join(jobPath, 'scenario.yaml');
    this.ensureJobsPathExists();
    if (!fs.existsSync(jobPath)) {
      fs.mkdirSync(jobPath);
    }

    // Compile dependabot cmd arguments
    // See: https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/root.go
    //      https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/update.go
    let dependabotArguments = ['update', '-f', jobInputPath, '-o', jobOutputPath];
    if (options?.collectorImage) {
      dependabotArguments.push('--collector-image', options.collectorImage);
    }
    if (options?.proxyImage) {
      dependabotArguments.push('--proxy-image', options.proxyImage);
    }
    if (options?.updaterImage) {
      dependabotArguments.push('--updater-image', options.updaterImage);
    }

    // Generate the job input file
    writeJobConfigFile(jobInputPath, operation);

    // Run dependabot update
    if (!fs.existsSync(jobOutputPath) || fs.statSync(jobOutputPath)?.size == 0) {
      console.info(`Running Dependabot update job '${jobInputPath}'...`);
      const dependabotTool = tool(dependabotPath).arg(dependabotArguments);
      const dependabotResultCode = await dependabotTool.execAsync({
        failOnStdErr: false,
        ignoreReturnCode: true,
        env: {
          DEPENDABOT_JOB_ID: jobId.replace(/-/g, '_'), // replace hyphens with underscores
          LOCAL_GITHUB_ACCESS_TOKEN: options?.gitHubAccessToken, // avoid rate-limiting when pulling images from GitHub container registries
        },
      });
      if (dependabotResultCode != 0) {
        error(`Dependabot failed with exit code ${dependabotResultCode}`);
      }
    }

    // Process the job output
    const operationResults = Array<IDependabotUpdateOperationResult>();
    if (fs.existsSync(jobOutputPath)) {
      const jobOutputs = readJobScenarioOutputFile(jobOutputPath);
      if (jobOutputs?.length > 0) {
        console.info(`Processing outputs from '${jobOutputPath}'...`);
        for (const output of jobOutputs) {
          // Documentation on the scenario model can be found here:
          // https://github.com/dependabot/cli/blob/main/internal/model/scenario.go
          const type = output['type'];
          const data = output['expect']?.['data'];
          var operationResult = {
            success: true,
            error: null,
            output: {
              type: type,
              data: data,
            },
          };
          try {
            operationResult.success = await this.outputProcessor.process(operation, type, data);
          } catch (e) {
            operationResult.success = false;
            operationResult.error = e;
          } finally {
            operationResults.push(operationResult);
          }
        }
      }
    }

    return operationResults.length > 0 ? operationResults : undefined;
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

    console.info('Dependabot CLI install was not found, installing now with `go install dependabot`...');
    const goTool: ToolRunner = tool(which('go', true));
    goTool.arg(['install', this.toolImage]);
    goTool.execSync();

    // Depending on how Go is configured on the host agent, the "go/bin" path may not be in the PATH environment variable.
    // If dependabot still cannot be found using `which()` after install, we must manually resolve the path;
    // It will either be "$GOPATH/bin/dependabot" or "$HOME/go/bin/dependabot", if GOPATH is not set.
    const goBinPath = process.env.GOPATH ? path.join(process.env.GOPATH, 'bin') : path.join(os.homedir(), 'go', 'bin');
    return (this.toolPath ||= which('dependabot', false) || path.join(goBinPath, 'dependabot'));
  }

  // Create the jobs directory if it does not exist
  private ensureJobsPathExists(): void {
    if (!fs.existsSync(this.jobsPath)) {
      fs.mkdirSync(this.jobsPath);
    }
  }

  // Clean up the jobs directory and its contents
  public cleanup(): void {
    if (fs.existsSync(this.jobsPath)) {
      fs.rmSync(this.jobsPath, {
        recursive: true,
        force: true,
      });
    }
  }
}

// Documentation on the job model can be found here:
// https://github.com/dependabot/cli/blob/main/internal/model/job.go
function writeJobConfigFile(path: string, config: IDependabotUpdateJobConfig): void {
  fs.writeFileSync(
    path,
    yaml.dump({
      job: config.job,
      credentials: config.credentials,
    }),
  );
}

// Documentation on the scenario model can be found here:
// https://github.com/dependabot/cli/blob/main/internal/model/scenario.go
function readJobScenarioOutputFile(path: string): any[] {
  const scenarioContent = fs.readFileSync(path, 'utf-8');
  if (!scenarioContent || typeof scenarioContent !== 'string') {
    return []; // No outputs or failed scenario
  }

  const scenario: any = yaml.load(scenarioContent);
  if (scenario === null || typeof scenario !== 'object') {
    throw new Error('Invalid scenario object');
  }

  return scenario['output'] || [];
}

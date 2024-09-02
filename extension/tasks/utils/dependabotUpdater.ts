import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { which, tool } from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import * as yaml from 'js-yaml';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

export interface IUpdateJobConfig {
    job: {
        // See: https://github.com/dependabot/dependabot-core/blob/main/updater/lib/dependabot/job.rb
        'id': string,
        'package-manager': string,
        'updating-a-pull-request': boolean,
        'dependency-group-to-refresh'?: string,
        'dependency-groups'?: {
            'name': string,
            'applies-to'?: string,
            'update-types'?: string[],
            'rules': {
                'patterns'?: string[]
                'exclude-patterns'?: string[],
                'dependency-type'?: string
            }[]
        }[],
        'dependencies'?: string[],
        'allowed-updates'?: {
            'dependency-name'?: string,
            'dependency-type'?: string,
            'update-type'?: string
        }[],
        'ignore-conditions'?: {
            'dependency-name'?: string,
            'version-requirement'?: string,
            'source'?: string,
            'update-types'?: string[]
        }[],
        'security-updates-only': boolean,
        'security-advisories'?: {
            'dependency-name': string,
            'affected-versions': string[],
            'patched-versions': string[],
            'unaffected-versions': string[],
            'title'?: string,
            'description'?: string,
            'source-name'?: string,
            'source-url'?: string
        }[],
        'source': {
            'provider': string,
            'api-endpoint'?: string,
            'hostname': string,
            'repo': string,
            'branch'?: string,
            'commit'?: string,
            'directory'?: string,
            'directories'?: string[]
        },
        'existing-pull-requests'?: {
            'dependencies': {
                'name': string,
                'version'?: string,
                'removed': boolean,
                'directory'?: string
            }[]
        },
        'existing-group-pull-requests'?: {
            'dependency-group-name': string,
            'dependencies': {
                'name': string,
                'version'?: string,
                'removed': boolean,
                'directory'?: string
            }[]
        },
        'commit-message-options'?: {
            'prefix'?: string,
            'prefix-development'?: string,
            'include'?: string,
        },
        'experiments'?: any,
        'max-updater-run-time'?: number,
        'reject-external-code'?: boolean,
        'repo-contents-path'?: string,
        'requirements-update-strategy'?: string,
        'lockfile-only'?: boolean
    },
    credentials: {
        // See: https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/credential.rb
        'type': string,
        'host'?: string,
        'region'?: string,
        'url'?: string,
        'registry'?: string,
        'username'?: string,
        'password'?: string,
        'token'?: string,
        'replaces-base'?: boolean
    }[]
}

export interface IUpdateScenarioOutput {
    // See: https://github.com/dependabot/smoke-tests/tree/main/tests
    type: string,
    data: any
}

export class DependabotUpdater {
    private readonly jobsPath: string;
    private readonly toolImage: string;
    private readonly debug: boolean;

    constructor(cliToolImage?: string, debug?: boolean) {
        this.jobsPath = path.join(os.tmpdir(), 'dependabot-jobs');
        this.toolImage = cliToolImage ?? "github.com/dependabot/cli/cmd/dependabot@latest";
        this.debug = debug ?? false;
        this.ensureJobsPathExists();
    }

    // Run dependabot update
    public async update(
        config: IUpdateJobConfig, 
        options?: {
            collectorImage?: string,
            proxyImage?: string,
            updaterImage?: string
        }
    ): Promise<IUpdateScenarioOutput[]> {

        // Install dependabot if not already installed
        await this.ensureToolsAreInstalled();

        // Create the job directory
        const jobId = config.job.id;
        const jobPath = path.join(this.jobsPath, jobId.toString());
        const jobInputPath = path.join(jobPath, 'job.yaml');
        const jobOutputPath = path.join(jobPath, 'scenario.yaml');
        this.ensureJobsPathExists();
        if (!fs.existsSync(jobPath)){
          fs.mkdirSync(jobPath);
        }

        // Generate the job input file
        writeJobInput(jobInputPath, config);

        // Compile dependabot cmd arguments
        // See: https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/root.go
        //      https://github.com/dependabot/cli/blob/main/cmd/dependabot/internal/cmd/update.go
        let dependabotArguments = [
            "update", "-f", jobInputPath, "-o", jobOutputPath
        ];
        if (options?.collectorImage) {
            dependabotArguments.push("--collector-image", options.collectorImage);
        }
        if (options?.proxyImage) {
            dependabotArguments.push("--proxy-image", options.proxyImage);
        }
        if (options?.updaterImage) {
            dependabotArguments.push("--updater-image", options.updaterImage);
        }

        console.info("Running dependabot update...");
        const dependabotTool = tool(which("dependabot", true)).arg(dependabotArguments);
        const dependabotResultCode = await dependabotTool.execAsync({
            silent: !this.debug
        });
        if (dependabotResultCode != 0) {
            throw new Error(`Dependabot update failed with exit code ${dependabotResultCode}`);
        }

        return readScenarioOutputs(jobOutputPath); 
    }

    // Install dependabot if not already installed
    private async ensureToolsAreInstalled(): Promise<void> {

        debug('Checking for `dependabot` install...');
        if (which("dependabot", false)) {
            return;
        }

        debug("Dependabot CLI was not found, installing with `go install`...");
        const goTool: ToolRunner = tool(which("go", true));
        goTool.arg(["install", this.toolImage]);
        goTool.execSync({
            silent: !this.debug
        });
    }

    // Create the jobs directory if it does not exist
    private ensureJobsPathExists(): void {
        if (!fs.existsSync(this.jobsPath)){
            fs.mkdirSync(this.jobsPath);
        }
    }

    // Clean up the jobs directory and its contents
    public cleanup(): void {
        if (fs.existsSync(this.jobsPath)){
            fs.rmSync(this.jobsPath, {
                recursive: true,
                force: true
            });
        }
    }
}

function writeJobInput(path: string, config: IUpdateJobConfig): void {
    fs.writeFileSync(path, yaml.dump(config));
}

function readScenarioOutputs(path: string): IUpdateScenarioOutput[] {
    if (!path) {
        throw new Error("Scenario file path is required");
    }

    const scenarioContent = fs.readFileSync(path, 'utf-8');
    if (!scenarioContent || typeof scenarioContent !== 'string') {
      throw new Error(`Scenario file could not be read at '${path}'`);
    }
  
    const scenario: any = yaml.load(scenarioContent);
    if (scenario === null || typeof scenario !== 'object') {
      throw new Error('Invalid scenario object');
    }
  
    let outputs = new Array<IUpdateScenarioOutput>();
    scenario['output']?.forEach((output: any) => {
        outputs.push({
            type: output['type'],
            data: output['expect']?.['data']
        });
    });

    return outputs;
}
import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { which, tool } from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import * as yaml from 'js-yaml';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

export interface IUpdateJobConfig {
    id: string,
    job: {
        'package-manager': string,
        'allowed-updates': {
            'update-type': string
        }[],
        source: {
            provider: string,
            repo: string,
            directory: string,
            commit: string
        }
    },
    credentials: {
        type: string,
        host?: string,
        username?: string,
        password?: string,
        url?: string,
        token?: string
    }[]
}

export interface IUpdateScenarioOutput {
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
    public async update(options: {
        job: IUpdateJobConfig,
        collectorImage?: string,
        proxyImage?: string,
        updaterImage?: string
    }): Promise<IUpdateScenarioOutput[]> {

        // Install dependabot if not already installed
        await this.ensureToolsAreInstalled();

        // Create the job directory
        const jobId = options.job.id;
        const jobPath = path.join(this.jobsPath, jobId.toString());
        const jobInputPath = path.join(jobPath, 'job.yaml');
        const jobOutputPath = path.join(jobPath, 'scenario.yaml');
        this.ensureJobsPathExists();
        if (!fs.existsSync(jobPath)){
          fs.mkdirSync(jobPath);
        }

        // Generate the job input file
        writeJobInput(jobInputPath, options.job);

        // Compile dependabot cmd arguments
        let dependabotArguments = [
            "update", "-f", jobInputPath, "-o", jobOutputPath
        ];
        if (options.collectorImage) {
            dependabotArguments.push("--collector-image", options.collectorImage);
        }
        if (options.proxyImage) {
            dependabotArguments.push("--proxy-image", options.proxyImage);
        }
        if (options.updaterImage) {
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

function writeJobInput(path: string, job: IUpdateJobConfig): void {
    fs.writeFileSync(path, yaml.dump(job));
}

function readScenarioOutputs(scenarioFilePath: string): IUpdateScenarioOutput[] {
    if (!scenarioFilePath) {
        throw new Error("Scenario file path is required");
    }

    const scenarioContent = fs.readFileSync(scenarioFilePath, 'utf-8');
    if (!scenarioContent || typeof scenarioContent !== 'string') {
      throw new Error(`Scenario file could not be read at '${scenarioFilePath}'`);
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
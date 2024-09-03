import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { which, tool } from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import { IDependabotUpdateOutputProcessor, IDependabotUpdateJob, IDependabotUpdateOutput } from "./dependabotTypes";
import * as yaml from 'js-yaml';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

// Wrapper class for running updates using dependabot-cli
export class DependabotUpdater {
    private readonly jobsPath: string;
    private readonly toolImage: string;
    private readonly outputProcessor: IDependabotUpdateOutputProcessor;
    private readonly debug: boolean;

    public static readonly CLI_IMAGE_LATEST = "github.com/dependabot/cli/cmd/dependabot@latest";

    constructor(cliToolImage: string, outputProcessor: IDependabotUpdateOutputProcessor, debug: boolean) {
        this.jobsPath = path.join(os.tmpdir(), 'dependabot-jobs');
        this.toolImage = cliToolImage;
        this.outputProcessor = outputProcessor;
        this.debug = debug;
        this.ensureJobsPathExists();
    }

    // Run dependabot update
    public async update(
        config: IDependabotUpdateJob,
        options?: {
            collectorImage?: string,
            proxyImage?: string,
            updaterImage?: string
        }
    ): Promise<IDependabotUpdateOutput[]> {

        // Install dependabot if not already installed
        await this.ensureToolsAreInstalled();

        // Create the job directory
        const jobId = config.job.id;
        const jobPath = path.join(this.jobsPath, jobId.toString());
        const jobInputPath = path.join(jobPath, 'job.yaml');
        const jobOutputPath = path.join(jobPath, 'scenario.yaml');
        this.ensureJobsPathExists();
        if (!fs.existsSync(jobPath)) {
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
        if (!fs.existsSync(jobOutputPath)) {
            const dependabotTool = tool(which("dependabot", true)).arg(dependabotArguments);
            const dependabotResultCode = await dependabotTool.execAsync({
                silent: !this.debug
            });
            if (dependabotResultCode != 0) {
                throw new Error(`Dependabot update failed with exit code ${dependabotResultCode}`);
            }
        }

        console.info("Processing dependabot update outputs...");
        const processedOutputs = Array<IDependabotUpdateOutput>();
        for (const output of readScenarioOutputs(jobOutputPath)) {
            const type = output['type'];
            const data = output['expect']?.['data'];
            var processedOutput = {
                success: true,
                error: null,
                output: {
                    type: type,
                    data: data
                }
            };
            try {
                processedOutput.success = await this.outputProcessor.process(config, type, data);
            }
            catch (e) {
                processedOutput.success = false;
                processedOutput.error = e;
            }
            finally {
                processedOutputs.push(processedOutput);
            }
        }

        return processedOutputs;
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
        if (!fs.existsSync(this.jobsPath)) {
            fs.mkdirSync(this.jobsPath);
        }
    }

    // Clean up the jobs directory and its contents
    public cleanup(): void {
        if (fs.existsSync(this.jobsPath)) {
            fs.rmSync(this.jobsPath, {
                recursive: true,
                force: true
            });
        }
    }
}

function writeJobInput(path: string, config: IDependabotUpdateJob): void {
    fs.writeFileSync(path, yaml.dump(config));
}

function readScenarioOutputs(path: string): any[] {
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

    return scenario['output'] || [];
}
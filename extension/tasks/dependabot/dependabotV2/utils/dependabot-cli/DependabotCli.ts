import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { which, tool } from "azure-pipelines-task-lib/task"
import { ToolRunner } from "azure-pipelines-task-lib/toolrunner"
import { IDependabotUpdateOutputProcessor } from "./interfaces/IDependabotUpdateOutputProcessor";
import { IDependabotUpdateOperationResult } from "./interfaces/IDependabotUpdateOperationResult";
import { IDependabotUpdateOperation } from "./interfaces/IDependabotUpdateOperation";
import * as yaml from 'js-yaml';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
import { IDependabotUpdateJobConfig } from "./interfaces/IDependabotUpdateJobConfig";

/**
 * Wrapper class for running updates using dependabot-cli
 */
export class DependabotCli {
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

    /**
     * Run dependabot update job
     * @param operation 
     * @param options 
     * @returns 
     */
    public async update(
        operation: IDependabotUpdateOperation,
        options?: {
            collectorImage?: string,
            proxyImage?: string,
            updaterImage?: string
        }
    ): Promise<IDependabotUpdateOperationResult[] | undefined> {

        // Install dependabot if not already installed
        await this.ensureToolsAreInstalled();

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

        // Generate the job input file
        writeJobConfigFile(jobInputPath, operation);

        // Run dependabot update
        if (!fs.existsSync(jobOutputPath) || fs.statSync(jobOutputPath)?.size == 0) {
            console.info(`Running dependabot update job '${jobInputPath}'...`);
            const dependabotTool = tool(which("dependabot", true)).arg(dependabotArguments);
            const dependabotResultCode = await dependabotTool.execAsync({
                silent: !this.debug,
                failOnStdErr: false,
                ignoreReturnCode: true
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
                console.info("Processing Dependabot outputs...");
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
                            data: data
                        }
                    };
                    try {
                        operationResult.success = await this.outputProcessor.process(operation, type, data);
                    }
                    catch (e) {
                        operationResult.success = false;
                        operationResult.error = e;
                    }
                    finally {
                        operationResults.push(operationResult);
                    }
                }
            }
        }

        return operationResults.length > 0 ? operationResults : undefined;
    }

    // Install dependabot if not already installed
    private async ensureToolsAreInstalled(): Promise<void> {

        debug('Checking for `dependabot` install...');
        if (which("dependabot", false)) {
            return;
        }

        console.info("Dependabot CLI install was not found, installing now with `go install`...");
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

// Documentation on the job model can be found here:
// https://github.com/dependabot/cli/blob/main/internal/model/job.go
function writeJobConfigFile(path: string, config: IDependabotUpdateJobConfig): void {
    fs.writeFileSync(path, yaml.dump({
        job: config.job,
        credentials: config.credentials
    }));
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
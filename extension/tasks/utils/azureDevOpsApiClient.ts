import { debug, warning, error } from "azure-pipelines-task-lib/task"
import { WebApi, getPersonalAccessTokenHandler } from "azure-devops-node-api";

// Wrapper for DevOps API actions
export class AzureDevOpsClient {
    private readonly connection: WebApi;
    private userId: string | null = null;

    constructor(apiEndpointUrl: string, accessToken: string) {
        this.connection = new WebApi(
            apiEndpointUrl,
            getPersonalAccessTokenHandler(accessToken)
        );
    }

    private async getUserId(): Promise<string> {
        return (this.userId ||= (await this.connection.connect()).authenticatedUser?.id || "");
    }
}
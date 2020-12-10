import tl = require('azure-pipelines-task-lib/task');
import tr = require('azure-pipelines-task-lib/toolrunner');

function getGithubEndPointToken(githubEndpoint: string): string {
    const githubEndpointObject = tl.getEndpointAuthorization(githubEndpoint, false);
    let githubEndpointToken: string = null;

    if (!!githubEndpointObject) {
        tl.debug('Endpoint scheme: ' + githubEndpointObject.scheme);

        if (githubEndpointObject.scheme === 'PersonalAccessToken') {
            githubEndpointToken = githubEndpointObject.parameters.accessToken;
        } else if (githubEndpointObject.scheme === 'OAuth') {
            githubEndpointToken = githubEndpointObject.parameters.AccessToken;
        } else if (githubEndpointObject.scheme === 'Token') {
            githubEndpointToken = githubEndpointObject.parameters.AccessToken;
        } else if (githubEndpointObject.scheme) {
            throw new Error(tl.loc('InvalidEndpointAuthScheme', githubEndpointObject.scheme));
        }
    }

    if (!githubEndpointToken) {
        throw new Error(tl.loc('InvalidGitHubEndpoint', githubEndpoint));
    }

    return githubEndpointToken;
}

function extractOrganization (organisationUrl: string) : string
{
    let parts = organisationUrl.split("/");

    // Check for new style: https://dev.azure.com/x/
    if (parts.length === 5)
    {
        return parts[3];
    }

    // Check for old style: https://x.visualstudio.com/
    if (parts.length === 4)
    {
        // Get x.visualstudio.com part.
        let part = parts[2];

        // Return organisation part (x).
        return part.split(".")[0];
    }

    tl.setResult(tl.TaskResult.Failed, `Error parsing organisation from organisation url: '${organisationUrl}'.`);
}

async function run() {
    try {
        // Checking if docker is installed
        tl.debug('Checking for docker install ...');
        tl.which('docker', true);

        // Prepare the docker task
        let dockerRunner: tr.ToolRunner = tl.tool(tl.which('docker', true));
        dockerRunner.arg(['run']);  // run command
        dockerRunner.arg(['--rm']); //remove after execution
        dockerRunner.arg(['-it']);  // interactive so that we can see the output

        // Set the organization
        var organizationUrl = tl.getVariable("System.TeamFoundationCollectionUri");
        let organization: string = extractOrganization(organizationUrl);
        dockerRunner.arg(['-e', `ORGANIZATION=${organization}`]);

        // Set the project
        let project: string = tl.getVariable('System.TeamProject');
        dockerRunner.arg(['-e', `PROJECT=${project}`]);

        // Set the repository
        let repository: string = tl.getVariable('Build.Repository.Name');
        dockerRunner.arg(['-e', `REPOSITORY=${repository}`]);

        // Set the package manager
        let packageManager: string = tl.getInput('packageManager', true);
        dockerRunner.arg(['-e', `PACKAGE_MANAGER=${packageManager}`]);

        // Set the access token for Azure DevOps Repos
        // If the user has not provided one, we use the one from the build
        let systemAccessToken: string = tl.getInput('azureDevOpsAccessToken');
        if (!systemAccessToken)
        {
            tl.debug("No custom token provided. The SYSTEM_ACCESSTOKEN environment variable shall be used.");
            systemAccessToken = tl.getVariable('System.AccessToken');
        }
        dockerRunner.arg(['-e', `SYSTEM_ACCESSTOKEN=${systemAccessToken}`]);

        // Set the github token, if one is provided
        const githubEndpointId = tl.getInput("gitHubConnection");
        if (githubEndpointId)
        {
            tl.debug("GitHub connection supplied. A token shall be extracted from it.");
            let githubAccessToken: string = getGithubEndPointToken(githubEndpointId);
            dockerRunner.arg(['-e', `GITHUB_ACCESS_TOKEN=${githubAccessToken}`]);
        }

        // Set the name of the private feed
        let privateFeedName: string = tl.getInput('feedName', false);
        if (privateFeedName)
        {
            dockerRunner.arg(['-e', `PRIVATE_FEED_NAME=${privateFeedName}`]);
        }

        // Set the directory
        let directory: string = tl.getInput("directory", false);
        if (directory)
        {
            dockerRunner.arg(['-e', `DIRECTORY=${directory}`]);
        }

        // Set the target branch
        let targetBranch: string = tl.getInput('targetBranch', false);
        if (targetBranch)
        {
            dockerRunner.arg(['-e', `TARGET_BRANCH=${targetBranch}`]);
        }

        // The docker image to run is not provided as an in put because of the chances of error.
        // May be in the future it can be made optional. The difficulty in making it optional arises
        // form compatibility if the image envrionment variables change names or format.
        const dockerImage = 'tingle/dependabot-azure-devops:0.1.1';
        tl.debug(`Running docker container using '${dockerImage}' ...`);
        dockerRunner.arg([dockerImage]);

        // Now execute using docker
        await dockerRunner.exec();

        tl.debug("Docker container execution completed!");
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();

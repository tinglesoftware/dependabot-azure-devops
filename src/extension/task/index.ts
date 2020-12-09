import tl = require('azure-pipelines-task-lib/task');
import tr = require('azure-pipelines-task-lib/toolrunner');

function getGitHubAccessToken(endpoint: string): string {
    let result: string = null;

    const geo = tl.getEndpointAuthorization(endpoint, false);
    if (!!geo)
    {
        tl.debug("Endpoint scheme: " + geo.scheme);
        if(geo.scheme == 'PersonalAccessToken') {
            result = geo.parameters.accessToken
        } else if (geo.scheme == 'OAuth') {
            result = geo.parameters.AccessToken
        } else if (geo.scheme) {
            throw new Error(tl.loc("InvalidEndpointAuthScheme", geo.scheme));
        }
    }

    if (!result) {
        throw new Error(tl.loc("InvalidGitHubEndpoint", endpoint));
    }

    return result;
}

function getOrganization (organisationUrl: string) : string
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

        let dockerImage: string = 'tingle/dependabot-azure-devops:0.1.1';

        // Prepare the variables for execution
        var organizationUrl = tl.getVariable("System.TeamFoundationCollectionUri");
        let organization: string = getOrganization(organizationUrl);
        let project: string = tl.getVariable('System.TeamProject');
        let repository: string = tl.getVariable('Build.Repository.Name');
        let packageManager: string = tl.getInput('packageManager', true);
        let systemAccessToken: string = tl.getVariable('System.AccessToken');

        // Now execute using docker
        tl.debug("Running docker container ...");
        let dockerRunner: tr.ToolRunner = tl.tool(tl.which('docker', true));
        dockerRunner.arg(['run', '--rm', '-it']);
        dockerRunner.arg(['-e', `ORGANIZATION=${organization}`]);
        dockerRunner.arg(['-e', `PROJECT=${project}`]);
        dockerRunner.arg(['-e', `REPOSITORY=${repository}`]);
        dockerRunner.arg(['-e', `PACKAGE_MANAGER=${packageManager}`]);
        dockerRunner.arg(['-e', `SYSTEM_ACCESSTOKEN=${systemAccessToken}`]);

        // Add optional variables
        const githubEndpoint = tl.getInput("gitHubConnection");
        if (githubEndpoint)
        {
            let githubAccessToken: string = getGitHubAccessToken(githubEndpoint);
            dockerRunner.arg(['-e', `GITHUB_ACCESS_TOKEN=${githubAccessToken}`]);
        }

        let privateFeedName: string = tl.getInput('feedName', false);
        if (privateFeedName)
        {
            dockerRunner.arg(['-e', `PRIVATE_FEED_NAME=${privateFeedName}`]);
        }

        let directory: string = tl.getInput("directory", false);
        if (directory)
        {
            dockerRunner.arg(['-e', `DIRECTORY=${directory}`]);
        }

        let targetBranch: string = tl.getInput('targetBranch', false);
        if (targetBranch)
        {
            dockerRunner.arg(['-e', `TARGET_BRANCH=${targetBranch}`]);
        }

        dockerRunner.arg([dockerImage]);
        await dockerRunner.exec();

        tl.debug("Docker container execution completed!");
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();

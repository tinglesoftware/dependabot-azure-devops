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
        // Checking if bundler and ruby are installed
        tl.debug('Checking for bundler install...');
        tl.which('bundler', true);
        tl.debug('Checking for ruby install...');
        tl.which('ruby', true);

        // Install Gem files
        tl.debug("Installing Gem files");
        let bundlerRunner: tr.ToolRunner = tl.tool(tl.which('bundler', true));
        bundlerRunner.arg(['install']);
        bundlerRunner.arg(['-j', '3']);
        bundlerRunner.arg(['--path', 'vendor']);
        bundlerRunner.arg(['--gemfile', 'script/Gemfile']);
        await bundlerRunner.exec();

        // Prepare the variables for execution
        var organizationUrl = tl.getVariable("System.TeamFoundationCollectionUri");
        let organization: string = getOrganization(organizationUrl);
        let project: string = tl.getVariable('System.TeamProject');
        let repository: string = tl.getVariable('Build.Repository.Name');
        let packageManager: string = tl.getInput('packageManager', true);
        let systemAccessToken: string = tl.getVariable('System.AccessToken');

        const githubEndpoint = tl.getInput("gitHubConnection");
        let githubAccessToken: string = null;
        if (githubEndpoint)
        {
            githubAccessToken = getGitHubAccessToken(githubEndpoint);
        }

        let privateFeedName: string = tl.getInput('feedName', false);
        let directory: string = tl.getInput("directory", false);
        let targetBranch: string = tl.getInput('targetBranch', false);

        // Now execute the dependabot script
        tl.debug("Running update script");
        let scriptRunner: tr.ToolRunner = tl.tool(tl.which('bundler', true));
        scriptRunner.arg(['exec', 'ruby', 'script/update-script.rb']);
        
        await scriptRunner.exec();
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();

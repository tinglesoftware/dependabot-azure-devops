# Dependabot for Azure DevOps

This repository contains convenience tool(s) for updating dependencies in Azure DevOps repositories using [Dependabot](https://dependabot.com).

The work in this repository is based on inspired and occasionally guided by:

1. Official Script support: [code](https://github.com/dependabot/dependabot-script)
2. Andrew Craven's work: [blog](https://medium.com/@acraven/updating-dependencies-in-azure-devops-repos-773cbbb6029d), [code](https://github.com/acraven/azure-dependabot)
3. Chris' work: [code](https://github.com/chris5287/dependabot-for-azuredevops)
4. andrcun's work on GitLab: [code](https://gitlab.com/dependabot-gitlab/dependabot)
5. WeWork's work for GitLab: [code](https://github.com/wemake-services/kira-dependencies)

In this repository you'll find:

1. Dependabot's [Update script](./src/script/update-script.rb) in Ruby.
2. Dockerfile and build/image for running the script via Docker [here](./src/docker).
3. Azure DevOps [Extension](https://marketplace.visualstudio.com/items?itemName=tingle-software.dependabot) and [source](./src/extension).
4. Kubernetes CronJob [template](./templates).
5. Hosted versions: [fully hosted](#hosted-version), self hosted (source code and instructions coming soon).

## Hosted version

The hosted version for Azure DevOps would work almost similar to the native version of dependabot on GitHub.
It would support:

1. Pulling configuration from a file located at `.azuredevops/dependabot.yml`. The options would be the same as [the official .github/dependabot.yml](https://help.github.com/github/administering-a-repository/configuration-options-for-dependency-updates)
2. Adding/updating the file, wold immediately trigger a run.
3. Viewing the most recent runs for each repository, project and organization configured.
4. Hosted on Kubernetes is easier, but using build agents, would be an option to explore albeit limited.
5. Extra credentials for things like private registries, feeds and package repositories.

Currently, we have an implementation that works internally but is still a work in progress.
If you would like to join the private test send a request via support@tingle.software with the subject `Dependabot for Azure DevOps Hosted (Private Test)`.
In the body include:

1. Details of your organization (name should be enough),
2. Number of projects to target,
3. Number or repositories to target,
4. Number of updates in one configuration file (approximate is enough)
5. Average size of a repository

### Issues &amp; Comments

Please leave all comments, bugs, requests, and issues on the Issues page. We'll respond to your request ASAP!

### License

The code is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](./LICENSE) file for more information.

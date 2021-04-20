require "json"
require "dependabot/file_fetchers"
require "dependabot/file_parsers"
require "dependabot/update_checkers"
require "dependabot/file_updaters"
require "dependabot/pull_request_creator"
require "dependabot/omnibus"

def azure_get_active_prs(client, source, default_branch)
    response = client.get(source.api_endpoint +
        source.organization + "/" + source.project +
        "/_apis/git/repositories/" + source.unscoped_repo +
        "/pullrequests?searchCriteria.status=active" \
        "&searchCriteria.targetRefName=refs/heads/" + default_branch)

    JSON.parse(response.body).fetch("value")
end

def azure_abandon_pr(client, source, pull_request_id)
    # TODO: implement this
    puts "Abandoning PR is not yet implemented"
end

def azure_delete_branch(client, source, name)
    # TODO: implement this
    puts "Deleting branch is not yet implemented"
end

def azure_pull_request_commits(client, source, pull_request_id)
    response = client.get(source.api_endpoint +
        source.organization + "/" + source.project +
        "/_apis/git/repositories/" + source.unscoped_repo +
        "/pullrequests/" + "#{pull_request_id}" +
        "/commits")

    JSON.parse(response.body).fetch("value")
end

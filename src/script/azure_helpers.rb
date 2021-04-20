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
    branch_name = name.gsub?("refs/heads/", "")
    branch = client.branch(branch_name)
    branch_object_id = branch["objectId"]

    # https://developercommunity.visualstudio.com/t/delete-tags-or-branches-using-rest-apis/698220
    # https://github.com/MicrosoftDocs/azure-devops-docs/issues/2648
    content = [
        {
            name: name,
            oldObjectId: branch_object_id,
            newObjectId: "0000000000000000000000000000000000000000"
        }
    ]

    response = client.post(source.api_endpoint +
                source.organization + "/" + source.project +
                "/_apis/git/repositories/" + source.unscoped_repo +
                "/refs?api-version=5.0", content.to_json)
end

def azure_pull_request_commits(client, source, pull_request_id)
    response = client.get(source.api_endpoint +
        source.organization + "/" + source.project +
        "/_apis/git/repositories/" + source.unscoped_repo +
        "/pullrequests/" + "#{pull_request_id}" +
        "/commits")

    JSON.parse(response.body).fetch("value")
end

def azure_set_auto_complete(client, source, pull_request_id)
    # TODO: implement this
    puts "Setting auto complete is not yet implemented"
end

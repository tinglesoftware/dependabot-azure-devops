require "json"
require "dependabot/shared_helpers"
require "excon"

module Dependabot
    module Clients
        class Azure

            def pull_requests_active(default_branch)
                response = get(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests?searchCriteria.status=active" \
                    "&searchCriteria.targetRefName=refs/heads/" + default_branch)

                JSON.parse(response.body).fetch("value")
            end

            def pull_request_abandon(pull_request_id)
                # TODO: implement this
                puts "Abandoning PR is not yet implemented"
            end

            def branch_delete(name)
                branch_name = name.gsub?("refs/heads/", "")
                branch = branch(branch_name)
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

                response = post(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/refs?api-version=5.0", content.to_json)
            end

            def pull_request_commits(pull_request_id)
                response = get(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests/" + "#{pull_request_id}" +
                    "/commits")

                JSON.parse(response.body).fetch("value")
            end

            def pull_request_auto_complete(pull_request_id)
                # TODO: implement this
                puts "Setting auto complete is not yet implemented"
            end

        end
    end
end

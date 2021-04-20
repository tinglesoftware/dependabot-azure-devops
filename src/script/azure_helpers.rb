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
                content = {
                    status: "abandoned"
                }

                response = patch(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests/#{pull_request_id}?api-version=5.0", content.to_json)
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

            def pull_request_auto_complete(pull_request_id, auto_complete_user_id, merge_strategy, delete_source_branch = false)
                # https://docs.microsoft.com/en-us/rest/api/azure/devops/git/pull%20requests/update?view=azure-devops-rest-6.0
                content = {
                    autoCompleteSetBy: {
                        id: auto_complete_user_id
                    },
                    completionOptions: {
                        mergeStrategy: merge_strategy,
                        deleteSourceBranch: delete_source_branch
                    }
                }

                response = patch(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests/#{pull_request_id}?api-version=6.0", content.to_json)
            end

            def patch(url, json)
                response = Excon.patch(
                    url,
                    body: json,
                    user: credentials&.fetch("username", nil),
                    password: credentials&.fetch("password", nil),
                    idempotent: true,
                    **SharedHelpers.excon_defaults(
                        headers: auth_header.merge(
                            {
                                "Content-Type" => "application/json"
                            }
                        )
                    )
                )
                raise NotFound if response.status == 404

                response
            end
        end
    end
end

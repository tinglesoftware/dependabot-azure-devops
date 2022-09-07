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
                branch_name = name.gsub("refs/heads/", "")
                branch_object = branch(branch_name)
                branch_object_id = branch_object["objectId"]

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

            def pull_request_auto_complete(pull_request_id, auto_complete_user_id, merge_strategy)
                # https://docs.microsoft.com/en-us/rest/api/azure/devops/git/pull%20requests/update?view=azure-devops-rest-6.0
                content = {
                    autoCompleteSetBy: {
                        id: auto_complete_user_id
                    },
                    completionOptions: {
                        mergeStrategy: merge_strategy,
                        deleteSourceBranch: true,
                        transitionWorkItems: false
                    }
                }

                response = patch(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests/#{pull_request_id}?api-version=5.0", content.to_json)
            end

            def pull_request_approve(pull_request_id, reviewer_email, reviewer_token)
                # https://docs.microsoft.com/en-us/rest/api/azure/devops/memberentitlementmanagement/user%20entitlements/search%20user%20entitlements?view=azure-devops-rest-6.0
                response = get(source.api_endpoint + source.organization + "/_apis/userentitlements?$filter=name eq '#{reviewer_email}'&api-version=6.0-preview.3")

                user_id = JSON.parse(response.body).fetch("members")[0]['id']

                # https://docs.microsoft.com/en-us/rest/api/azure/devops/git/pull%20request%20reviewers/create%20pull%20request%20reviewer?view=azure-devops-rest-6.0
                content = {
                    # 10 - approved 5 - approved with suggestions 0 - no vote -5 - waiting for author -10 - rejected
                    vote: 10
                }

                response = put_with_token(source.api_endpoint +
                    source.organization + "/" + source.project +
                    "/_apis/git/repositories/" + source.unscoped_repo +
                    "/pullrequests/#{pull_request_id}/reviewers/#{user_id}?api-version=6.0", content.to_json, reviewer_token)
            end

            def put_with_token(url, json, token)
                response = Excon.put(
                    url,
                    body: json,
                    user: credentials&.fetch("username", nil),
                    password: token,
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

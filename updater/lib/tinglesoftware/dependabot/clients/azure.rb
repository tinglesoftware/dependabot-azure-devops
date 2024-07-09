# typed: false
# frozen_string_literal: true

require "json"
require "dependabot/shared_helpers"
require "excon"

module TingleSoftware
  module Dependabot
    module Clients
      class Azure < ::Dependabot::Clients::Azure
        def pull_requests_active(user_id, default_branch)
          # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/get-pull-requests?view=azure-devops-rest-6.0&tabs=HTTP
          response = get(source.api_endpoint +
            source.organization + "/" + source.project +
            "/_apis/git/repositories/" + source.unscoped_repo +
            "/pullrequests?api-version=7.1&searchCriteria.status=active" \
            "&searchCriteria.creatorId=#{user_id}" \
            "&searchCriteria.targetRefName=refs/heads/#{default_branch}")

          JSON.parse(response.body).fetch("value")
        end

        def pull_request_abandon(pull_request_id)
          content = {
            status: "abandoned"
          }

          patch(source.api_endpoint +
            source.organization + "/" + source.project +
            "/_apis/git/repositories/" + source.unscoped_repo +
            "/pullrequests/#{pull_request_id}?api-version=7.1", content.to_json)
        end

        def branch_delete(name)
          branch_name = name.gsub("refs/heads/", "")
          branch_object_id = branch(branch_name)["objectId"]

          # https://developercommunity.visualstudio.com/t/delete-tags-or-branches-using-rest-apis/698220
          # https://github.com/MicrosoftDocs/azure-devops-docs/issues/2648
          update_ref(branch_name, branch_object_id, "0000000000000000000000000000000000000000")
        end

        def pull_request_commits(pull_request_id)
          response = get(source.api_endpoint +
            source.organization + "/" + source.project +
            "/_apis/git/repositories/" + source.unscoped_repo +
            "/pullrequests/#{pull_request_id}/commits?api-version=7.1")

          JSON.parse(response.body).fetch("value")
        end

        def get_user_id(token = nil)
          # https://learn.microsoft.com/en-us/javascript/api/azure-devops-extension-api/connectiondata
          # https://stackoverflow.com/a/53227325
          response = if token
                       get_with_token(source.api_endpoint + source.organization + "/_apis/connectionData", token)
                     else
                       get(source.api_endpoint + source.organization + "/_apis/connectionData")
                     end
          JSON.parse(response.body).fetch("authenticatedUser")["id"]
        end

        def pull_request_approve(pull_request_id, reviewer_token)
          user_id = get_user_id(reviewer_token)

          # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-reviewers/create-pull-request-reviewers?view=azure-devops-rest-6.0
          content = {
            # 10 - approved 5 - approved with suggestions 0 - no vote -5 - waiting for author -10 - rejected
            vote: 10,
            isReapprove: true
          }

          put_with_token(source.api_endpoint + source.organization + "/" + source.project +
            "/_apis/git/repositories/" + source.unscoped_repo +
            "/pullrequests/#{pull_request_id}/reviewers/#{user_id}" \
            "?api-version=7.1", content.to_json, reviewer_token)
        end

        def get_with_token(url, token)
          response = Excon.get(
            url,
            user: credentials&.fetch("username", nil),
            password: token,
            idempotent: true,
            **::Dependabot::SharedHelpers.excon_defaults(
              headers: auth_header
            )
          )

          raise Unauthorized if response.status == 401
          raise Forbidden if response.status == 403
          raise NotFound if response.status == 404

          response
        end

        def put_with_token(url, json, token)
          response = Excon.put(
            url,
            body: json,
            user: credentials&.fetch("username", nil),
            password: token,
            idempotent: true,
            **::Dependabot::SharedHelpers.excon_defaults(
              headers: auth_header.merge(
                {
                  "Content-Type" => "application/json"
                }
              )
            )
          )
          raise Unauthorized if response.status == 401
          raise Forbidden if response.status == 403
          raise NotFound if response.status == 404

          response
        end
      end
    end
  end
end

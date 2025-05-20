# typed: true
# frozen_string_literal: true

require "json"
require "dependabot/shared_helpers"
require "excon"

#
# Azure DevOps client that provides additional helper methods not available in the dependabot-core client.
#
module TingleSoftware
  module Dependabot
    module Clients
      class Azure < ::Dependabot::Clients::Azure
        API_VERSION = "7.1"

        # https://learn.microsoft.com/en-us/javascript/api/azure-devops-extension-api/connectiondata
        def get_user_id(token = nil)
          # https://stackoverflow.com/a/53227325
          response = if token
                       get_with_token(source.api_endpoint + source.organization + "/_apis/connectionData", token)
                     else
                       get(source.api_endpoint + source.organization + "/_apis/connectionData")
                     end
          JSON.parse(response.body).fetch("authenticatedUser")["id"]
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/get-pull-requests?view=azure-devops-rest-7.1
        def pull_requests_active_for_user_and_targeting_branch(user_id, default_branch)
          response = get(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests",
              "searchCriteria.status=active",
              "searchCriteria.creatorId=#{user_id}",
              "searchCriteria.targetRefName=refs/heads/#{default_branch}"
            )
          )
          JSON.parse(response.body).fetch("value")
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-requests/get-pull-request?view=azure-devops-rest-7.1
        def pull_request_abandon(pull_request_id)
          content = {
            status: "abandoned"
          }
          patch(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/#{pull_request_id}"
            ),
            content.to_json
          )
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-commits/get-pull-request-commits?view=azure-devops-rest-7.1
        def pull_request_commits(pull_request_id)
          response = get(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/#{pull_request_id}/commits"
            )
          )
          JSON.parse(response.body).fetch("value")
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-reviewers/create-pull-request-reviewers?view=azure-devops-rest-7.1
        def pull_request_approve(pull_request_id, reviewer_token)
          user_id = get_user_id(reviewer_token)
          content = {
            vote: 10, # 10 - approved 5 - approved with suggestions 0 - no vote -5 - waiting for author -10 - rejected
            isReapprove: true
          }
          put_with_token(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/#{pull_request_id}/reviewers/#{user_id}"
            ),
            content.to_json,
            reviewer_token
          )
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-threads/create?view=azure-devops-rest-7.1
        def pull_request_thread_with_comments(pull_request_id, type, comments, status)
          content = {
            comments: comments.map { |c| { commentType: type || "text", content: c } },
            status: status || "active"
          }
          post(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/" + pull_request_id + "/threads"
            ),
            content.to_json
          )
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties/list?view=azure-devops-rest-7.1
        def pull_request_properties_list(pull_request_id)
          response = get(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/" + pull_request_id + "/properties"
            )
          )
          JSON.parse(response.body).fetch("value")
        end

        # https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties/update?view=azure-devops-rest-7.1
        def pull_request_properties_update(pull_request_id, properties)
          content = properties.map do |key, value|
            {
              "op" => "replace", # update if exists; create if not exists
              "path" => "/#{key}",
              "value" => value.to_s
            }
          end
          patch_as_json_patch(
            azure_devops_api_url(
              "git/repositories/" + source.unscoped_repo + "/pullrequests/" + pull_request_id + "/properties"
            ),
            content.to_json
          )
        end

        def branch_delete(name)
          # https://developercommunity.visualstudio.com/t/delete-tags-or-branches-using-rest-apis/698220
          # https://github.com/MicrosoftDocs/azure-devops-docs/issues/2648
          branch_name = name.gsub("refs/heads/", "")
          branch_object_id = branch(branch_name)["objectId"]
          update_ref(branch_name, branch_object_id, "0000000000000000000000000000000000000000")
        end

        private

        def azure_devops_api_url(path, *query_string_params)
          source.api_endpoint + source.organization + "/" +
            source.project + "/_apis/" + path + "?api-version=" + API_VERSION + "&" + query_string_params&.join("&")
        end

        def patch_as_json_patch(url, json)
          response = Excon.patch(
            url,
            body: json,
            user: credentials&.fetch("username", nil),
            password: credentials&.fetch("password", nil),
            idempotent: true,
            **::Dependabot::SharedHelpers.excon_defaults(
              headers: auth_header.merge(
                {
                  "Content-Type" => "application/json-patch+json"
                }
              )
            )
          )

          raise Unauthorized if response&.status == 401
          raise Forbidden if response&.status == 403
          raise NotFound if response&.status == 404

          response
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

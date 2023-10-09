import * as tl from "azure-pipelines-task-lib/task";
import axios from "axios";
import extractOrganization from "./extractOrganization";

export interface IIdentity {
  /**
   * The identity id to use for PR reviewer or assignee Id.
   */
  id: string,
  /**
   * Human readable Username.
   */
  displayName?: string,
  /**
   * The provided input to use for searching an identity.
   */
  input: string,
}

/**
 * Resolves the given input email addresses to an array of IIdentity information.
 * It also handles non email input, which is assumed to be already an identity id 
 * to pass as reviewer id to an PR.
 * 
 * @param organizationUrl 
 * @param inputs 
 * @returns 
 */
export async function resolveAzureDevOpsIdentities(organizationUrl: URL, inputs: string[]): Promise<IIdentity[]> {
  const result: IIdentity[] = [];

  tl.debug(`Attempting to fetch configuration file via REST API ...`);
  for (const input of inputs) {
    if (input.indexOf("@") > 0 ) {
      // input is email to look-up
      const identityInfo = await querySubject(organizationUrl, input);
      if (identityInfo) {
        result.push(identityInfo);
      }
    } else {
      // input is already identity id
      result.push({id: input, input: input});
    }
  }
  return result;
}

/**
 * Returns whether the extension is run in a hosted environment (as opposed to an on-premise environment).
 * In Azure DevOps terms, hosted environment is also known as "Azure DevOps Services" and on-premise environment is known as
 * "Team Foundation Server" or "Azure DevOps Server".
 */
export function isHostedAzureDevOps(uri: URL): boolean {
  const hostname = uri.hostname.toLowerCase();
  return hostname === 'dev.azure.com' || hostname.endsWith('.visualstudio.com');
}

function decodeBase64(input: string):string {
  return Buffer.from(input, 'base64').toString('utf8');
}

function encodeBase64(input: string):string {
  return Buffer.from(input, 'utf8').toString('base64');
}

function isSuccessStatusCode(statusCode?: number) : boolean {
  return (statusCode >= 200) && (statusCode <= 299);
}

async function querySubject(organizationUrl: URL, email: string): Promise<IIdentity | undefined> {

  if (isHostedAzureDevOps(organizationUrl)) {
    const organization: string = extractOrganization(organizationUrl.toString());
    return await querySubjectHosted(organization, email);
  } else {
    return await querySubjectOnPrem(organizationUrl, email);
  }
}

/**
 * Make the HTTP Request for an OnPrem Azure DevOps Server to resolve an email to an IIdentity
 * @param organizationUrl 
 * @param email 
 * @returns 
 */
async function querySubjectOnPrem(organizationUrl: URL, email: string): Promise<IIdentity | undefined> {
  const url = `${organizationUrl}_apis/identities?searchFilter=MailAddress&queryMembership=None&filterValue=${email}`;
  tl.debug(`GET ${url}`);
  try {
    const response = await axios.get(url, {
      headers: {
        Authorization: `Basic ${encodeBase64("PAT:" + tl.getVariable("System.AccessToken"))}`,
        Accept: "application/json;api-version=5.0",
      },
    });

    if (isSuccessStatusCode(response.status)) {
      return {
        id: response.data.value[0]?.id,
        displayName: response.data.value[0]?.providerDisplayName,
        input: email}
    }
  } catch (error) {
    const responseStatusCode = error?.response?.status;
    tl.debug(`HTTP Response Status: ${responseStatusCode}`)
    if (responseStatusCode > 400 && responseStatusCode < 500) {
      tl.debug(`Access token is ${tl.getVariable("System.AccessToken")?.length > 0 ? "not" : ""} null or empty.`);
      throw new Error(
        `The access token provided is empty or does not have permissions to access '${url}'`
      );
    } else {
      throw error;
    }
  }
}

/**
 *  * Make the HTTP Request for a hosted Azure DevOps Service, to resolve an email to an IIdentity
 * @param organization
 * @param email
 * @returns
 */
async function querySubjectHosted(organization: string, email: string): Promise<IIdentity | undefined> {
  // make HTTP request
  const url = `https://vssps.dev.azure.com/${organization}/_apis/graph/subjectquery`;
  tl.debug(`GET ${url}`);
  try {
    const response = await axios.post(url, {
      headers: {
        Authorization: `Basic ${encodeBase64("PAT:" + tl.getVariable("System.AccessToken"))}`,
        Accept: "application/json;api-version=6.0-preview.1",
        "Content-Type": "application/json",
      },
      data: {
        "query": email,
        "subjectKind": [ "User" ]
      }
    });

    if (isSuccessStatusCode(response.status)) {
      const descriptor: string = response.data.value[0]?.descriptor || "";
      const id = decodeBase64(descriptor.substring(descriptor.indexOf(".") + 1))
      return {
        id: id,
        displayName: response.data.value[0]?.displayName,
        input: email}
    }
  } catch (error) {
    const responseStatusCode = error?.response?.status;
    tl.debug(`HTTP Response Status: ${responseStatusCode}`)
    if (responseStatusCode > 400 && responseStatusCode < 500) {
      tl.debug(`Access token is ${tl.getVariable("System.AccessToken")?.length > 0 ? "not" : ""} null or empty.`);
      throw new Error(
        `The access token provided is empty or does not have permissions to access '${url}'`
      );
    } else {
      throw error;
    }
  }
}

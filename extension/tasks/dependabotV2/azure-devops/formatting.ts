/**
 * Formats the logs into groups and sections to allow for easier navigation and readability.
 * https://learn.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands?view=azure-devops&tabs=bash#formatting-commands
 */

export function group(name: string) {
  console.log(`##[group]${name}`);
}

export function endgroup() {
  console.log(`##[endgroup]`);
}

export function section(name: string) {
  console.log(`##[section]${name}`);
}

/**
 * Masks the supplied values in the task log output.
 * https://learn.microsoft.com/en-us/azure/devops/pipelines/scripts/logging-commands?view=azure-devops&tabs=bash#setsecret-register-a-value-as-a-secret
 */

import { setSecret } from 'azure-pipelines-task-lib';
export function setSecrets(...args: string[]) {
  for (const arg of args.filter((a) => a && a?.toLowerCase() !== 'dependabot')) {
    // Mask the value and the uri encoded value. This is required to ensure that API and package feed url don't expose the value.
    // e.g. "Contoso Ltd" would appear as "Contoso%20Ltd" unless the uri encoded value was set as a secret.
    setSecret(arg);
    setSecret(encodeURIComponent(arg));
  }
}

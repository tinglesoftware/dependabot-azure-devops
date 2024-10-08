/**
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

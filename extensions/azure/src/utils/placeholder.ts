import { getVariable } from 'azure-pipelines-task-lib/task';

function convertPlaceholder(input: string): string {
  const matches: RegExpExecArray[] = extractPlaceholder(input);
  let result = input;
  for (const match of matches) {
    const placeholder = match[0];
    const name = match[1]!;
    const value = getVariable(name) ?? placeholder;
    result = result.replace(placeholder, value);
  }
  return result;
}

function extractPlaceholder(input: string) {
  const regexp: RegExp = new RegExp('\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9\\._-]*)\\s*}}', 'g');

  return matchAll(input, regexp);
}

function matchAll(input: string, rExp: RegExp, matches: Array<RegExpExecArray> = []) {
  const matchIfAny = rExp.exec(input);
  if (matchIfAny) {
    matches.push(matchIfAny);

    // recurse until no more matches
    matchAll(input, rExp, matches);
  }
  return matches;
}

export { convertPlaceholder, extractPlaceholder };

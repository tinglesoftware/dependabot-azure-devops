import { getVariable } from "azure-pipelines-task-lib/task";

function convertPlaceholder(input: string): string {
  var matches: RegExpExecArray[] = extractPlaceholder(input);
  var result = input;
  for (const match of matches) {
    var placeholder = match[0];
    var name = match[1];
    var value = getVariable(name) ?? placeholder;
    result = result.replace(placeholder, value);
  }
  return result;
}

function extractPlaceholder(input: string) {
  const regexp: RegExp = new RegExp("\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9_-]*)\\s*}}", 'g');

  return matchAll(input, regexp);
}

function matchAll(input: string, rExp: RegExp, matches: Array<RegExpExecArray> = []) {
  const matchIfAny = rExp.exec(input);
  matchIfAny && matches.push(matchIfAny) && matchAll(input, rExp, matches);
  return matches;
}

export { convertPlaceholder, extractPlaceholder };

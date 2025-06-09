export type VariableFinderFn = (name: string) => string | undefined;

function convertPlaceholder({
  input,
  variableFinder,
}: {
  input?: string;
  variableFinder: VariableFinderFn;
}): string | undefined {
  if (!input) return undefined;

  const matches: RegExpExecArray[] = extractPlaceholder(input);
  let result = input;
  for (const match of matches) {
    const placeholder = match[0];
    const name = match[1]!;
    const value = variableFinder(name) ?? placeholder;
    result = result.replace(placeholder, value);
  }
  return result;
}

function extractPlaceholder(input: string) {
  const regexp: RegExp = new RegExp('\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9\\._-]*)\\s*}}', 'g');

  return matchAll(input, regexp);
}

function matchAll(input: string, regexp: RegExp, matches: Array<RegExpExecArray> = []) {
  const matchIfAny = regexp.exec(input);
  if (matchIfAny) {
    matches.push(matchIfAny);

    // recurse until no more matches
    matchAll(input, regexp, matches);
  }
  return matches;
}

export { convertPlaceholder, extractPlaceholder };

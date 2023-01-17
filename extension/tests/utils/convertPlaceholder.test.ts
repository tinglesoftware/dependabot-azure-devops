import { extractPlaceholder } from "../../task/utils/convertPlaceholder";

describe("Replace key with value", () => {
  it("Should replace the key with a value", () => {
    var matches: RegExpExecArray[] = extractPlaceholder(
      "PAT:${{MY_DEPENDABOT_ADO_PAT}}"
    );
    expect(matches[0][1]).toBe("MY_DEPENDABOT_ADO_PAT");
  });

  it("Should replace the key with a value", () => {
    var matches: RegExpExecArray[] = extractPlaceholder("PAT:${{PAT}}");
    expect(matches[0][1]).toBe("PAT");
  });
});

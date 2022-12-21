import extractVirtualDirectory from "../../task/utils/extractVirtualDirectory";

describe("Extract virtual directory", () => {
  it("Should extract virtual directory", () => {
    var url = new URL("https://server.domain.com/contoso/x/");
    var virtualDirectory = extractVirtualDirectory(url);

    expect(virtualDirectory).toBe("contoso");
  });

  it("Should return empty for dev.azure.com organization URL", () => {
    var url = new URL("https://dev.azure.com/contoso/");
    var virtualDirectory = extractVirtualDirectory(url);

    expect(virtualDirectory).toBe("");
  });
});

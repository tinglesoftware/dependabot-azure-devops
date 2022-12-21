import extractHostname from "../../task/utils/extractHostname";

describe("Should extract hostname", () => {
  it("Should convert old *.visualstudio.com hostname to dev.azure.com", () => {
    var url = new URL("https://contoso.visualstudio.com");
    var hostname = extractHostname(url);

    expect(hostname).toBe("dev.azure.com");
  });

  it("Should retain the hostname", () => {
    var url = new URL("https://dev.azure.com/Core/contoso");
    var hostname = extractHostname(url);

    expect(hostname).toBe("dev.azure.com");
  });

  it("Should retain localhost hostname", () => {
    var url = new URL("https://localhost:8080/contoso");
    var hostname = extractHostname(url);

    expect(hostname).toBe("localhost");
  });
});

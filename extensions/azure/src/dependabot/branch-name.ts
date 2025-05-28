import * as crypto from 'crypto';

export function getBranchNameForUpdate(
  packageEcosystem: string,
  targetBranchName: string,
  directory: string,
  dependencyGroupName: string | undefined,
  dependencies: Record<string, unknown>[],
  separator: string = '/',
): string {
  // Based on dependabot-core implementation:
  // https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/pull_request_creator/branch_namer/solo_strategy.rb
  // https://github.com/dependabot/dependabot-core/blob/main/common/lib/dependabot/pull_request_creator/branch_namer/dependency_group_strategy.rb
  let branchName: string;
  const branchNameMightBeTooLong = dependencyGroupName || dependencies.length > 1;
  if (branchNameMightBeTooLong) {
    // Group/multi dependency update
    // e.g. dependabot/nuget/main/microsoft-3b49c54d9e
    const dependencyDigest = crypto
      .createHash('md5')
      .update(dependencies.map((d) => `${d['dependency-name']}-${d['dependency-version']}`).join(','))
      .digest('hex')
      .substring(0, 10);
    branchName = `${dependencyGroupName || 'multi'}-${dependencyDigest}`;
  } else {
    // Single dependency update
    // e.g. dependabot/nuget/main/Microsoft.Extensions.Logging-1.0.0
    const dependencyNames = dependencies
      .map((d) => d['dependency-name'])
      .join('-and-')
      .replace(/[:[]]/g, '-') // Replace `:` and `[]` with `-`
      .replace(/@/g, ''); // Remove `@`
    const versionSuffix = dependencies[0]?.['removed'] ? 'removed' : dependencies[0]?.['dependency-version'];
    branchName = `${dependencyNames}-${versionSuffix}`;
  }

  return sanitizeRef(
    [
      'dependabot',
      packageEcosystem,
      targetBranchName,
      // normalize directory to remove leading/trailing slashes and replace remaining ones with the separator
      `${directory}`.replace(/^\/+|\/+$/g, '').replace(/\//g, separator),
      branchName,
    ],
    separator,
  );
}

export function sanitizeRef(refParts: string[], separator: string): string {
  // Based on dependabot-core implementation:
  // https://github.com/dependabot/dependabot-core/blob/fc31ae64f492dc977cfe6773ab13fb6373aabec4/common/lib/dependabot/pull_request_creator/branch_namer/base.rb#L99

  // This isn't a complete implementation of git's ref validation, but it
  // covers most cases that crop up. Its list of allowed characters is a
  // bit stricter than git's, but that's for cosmetic reasons.
  return (
    refParts
      // Join the parts with the separator, ignore empty parts
      .filter((p) => p?.trim()?.length > 0)
      .join(separator)
      // Remove forbidden characters (those not already replaced elsewhere)
      .replace(/[^A-Za-z0-9/\-_.(){}]/g, '')
      // Slashes can't be followed by periods
      .replace(/\/\./g, '/dot-')
      // Squeeze out consecutive periods and slashes
      .replace(/\.+/g, '.')
      .replace(/\/+/g, '/')
      // Trailing periods are forbidden
      .replace(/\.$/, '')
  );
}

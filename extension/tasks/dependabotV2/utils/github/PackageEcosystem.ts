export enum PackageEcosystem {
  Composer = 'COMPOSER',
  Erlang = 'ERLANG',
  Actions = 'ACTIONS',
  Go = 'GO',
  Maven = 'MAVEN',
  Npm = 'NPM',
  Nuget = 'NUGET',
  Pip = 'PIP',
  Pub = 'PUB',
  Rubygems = 'RUBYGEMS',
  Rust = 'RUST',
  Swift = 'SWIFT',
}

export function getGhsaPackageEcosystemFromDependabotPackageManager(
  dependabotPackageManager: string,
): PackageEcosystem {
  switch (dependabotPackageManager) {
    case 'composer':
      return PackageEcosystem.Composer;
    case 'elm':
      return PackageEcosystem.Erlang;
    case 'github_actions':
      return PackageEcosystem.Actions;
    case 'go_modules':
      return PackageEcosystem.Go;
    case 'maven':
      return PackageEcosystem.Maven;
    case 'npm_and_yarn':
      return PackageEcosystem.Npm;
    case 'nuget':
      return PackageEcosystem.Nuget;
    case 'pip':
      return PackageEcosystem.Pip;
    case 'pub':
      return PackageEcosystem.Pub;
    case 'bundler':
      return PackageEcosystem.Rubygems;
    case 'cargo':
      return PackageEcosystem.Rust;
    case 'swift':
      return PackageEcosystem.Swift;
    default:
      throw new Error(`Unknown dependabot package manager: ${dependabotPackageManager}`);
  }
}

import { type DependabotCredential } from './proxy';

export function makeCredentialsMetadata(credentials: DependabotCredential[]): DependabotCredential[] {
  const sensitive = ['username', 'token', 'password', 'key', 'auth-key'];
  return credentials.map((cred) =>
    Object.fromEntries(Object.entries(cred).filter(([key]) => !sensitive.includes(key))),
  );
}

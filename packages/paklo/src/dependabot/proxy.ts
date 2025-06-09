import { z } from 'zod/v4';

export const DependabotCredentialSchema = z.record(z.string(), z.any());
export type DependabotCredential = z.infer<typeof DependabotCredentialSchema>;

export const CertificateAuthoritySchema = z.object({
  cert: z.string(),
  key: z.string(),
});
export type CertificateAuthority = z.infer<typeof CertificateAuthoritySchema>;

export const DependabotProxyConfigSchema = z.object({
  all_credentials: DependabotCredentialSchema.array(),
  ca: CertificateAuthoritySchema,
});
export type DependabotProxyConfig = z.infer<typeof DependabotProxyConfigSchema>;

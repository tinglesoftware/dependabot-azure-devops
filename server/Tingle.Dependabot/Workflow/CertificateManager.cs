using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Tingle.Dependabot.Models.Dependabot;

namespace Tingle.Dependabot.Workflow;

public interface ICertificateManager
{
    /// <summary>
    /// Get the current authority.
    /// This may throw an exception if not initialized.
    /// </summary>
    CertificateAuthority Get();

    /// <summary>
    /// Initialize the manager.
    /// This should not be called by the application because once the application starts,
    /// it is automatically done via an implementation of <see cref="IHostedService"/>.
    /// </summary>
    /// <param name="cancellationToken"></param>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}

internal class CertificateManager(IOptions<WorkflowOptions> optionsAccessor, ILogger<CertificateManager> logger) : ICertificateManager
{
    private const int KeySize = 2048;
    private const int KeyExpiryYears = 2;
    private const string CertFile = "cert.crt";
    private const string KeyFile = "cert.key";

    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

    private static readonly X500DistinguishedName CertSubject = new(
        "CN=Dependabot Internal CA, OU=Dependabot, O=GitHub Inc., L=San Francisco, S=California, C=US");

    private CertificateAuthority? ca;

    public CertificateAuthority Get() => ca ?? throw new InvalidOperationException($"'{nameof(InitializeAsync)}()' needs to be called prior to this");

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (ca is not null) return;

        var certPath = Path.Join(options.CertsDirectory, CertFile);
        var keyPath = Path.Join(options.CertsDirectory, KeyFile);

        // if either of the files are missing, we should generate new ones
        if (!File.Exists(certPath) || !File.Exists(keyPath))
        {
            logger.ProxyCertificatesMissing();
            ca = null;
        }
        else
        {
            var certPem = await File.ReadAllTextAsync(certPath, cancellationToken);
            var keyPem = await File.ReadAllTextAsync(keyPath, cancellationToken);

            try
            {
                // check if cert is expired
                var cert = X509Certificate2.CreateFromPem(certPem, keyPem);
                if (cert.NotAfter <= DateTimeOffset.UtcNow)
                {
                    logger.ProxyCertificatesExpired(cert.NotAfter);
                    ca = null;
                }

                ca = new CertificateAuthority(certPem, keyPem);
            }
            catch (Exception ex)
            {
                // loading failed, so we generate a new one
                logger.ProxyCertificatesLoadingFailed(ex);
                ca = null;
            }
        }

        if (ca is null)
        {
            // generate a new one
            ca = Generate();

            // create the directory if it does not exist
            var directory = Path.GetDirectoryName(certPath)!;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // write the certificate
            if (File.Exists(certPath)) File.Delete(certPath);
            await File.WriteAllTextAsync(certPath, ca.Cert, cancellationToken);

            // write the key
            if (File.Exists(keyPath)) File.Delete(keyPath);
            await File.WriteAllTextAsync(keyPath, ca.Key, cancellationToken);
        }
    }

    // generates a new proxy keypair CA
    private static CertificateAuthority Generate()
    {
        using var rsa = RSA.Create(KeySize);

        var request = new CertificateRequest(
            CertSubject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true)); // Is CA = true

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddYears(KeyExpiryYears);

        using var certificate = request.CreateSelfSigned(notBefore, notAfter);

        var pemCert = certificate.ExportCertificatePem();
        var pemKey = rsa.ExportRSAPrivateKeyPem();

        return new CertificateAuthority(pemCert, pemKey);
    }
}

internal class CertificateManagerInitializerService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // use scope just to be safe
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<ICertificateManager>();
        await manager.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

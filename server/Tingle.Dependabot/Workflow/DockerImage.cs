namespace Tingle.Dependabot.Workflow;

public readonly struct DockerImage(string repository, string? tag, string? digest) : IEquatable<DockerImage>
{
    public string Repository { get; } = repository;
    public string? Tag { get; } = tag;
    public string? Digest { get; } = digest;

    public override string ToString() => Digest is not null ? $"{Repository}@{Digest}" : $"{Repository}:{Tag}";
    public override int GetHashCode() => HashCode.Combine(Repository, Tag, Digest);
    public override bool Equals(object? obj) => obj is DockerImage url && Equals(url);
    public bool Equals(DockerImage other) => Repository == other.Repository && Tag == other.Tag && Digest == other.Digest;

    public static bool operator ==(DockerImage left, DockerImage right) => left.Equals(right);
    public static bool operator !=(DockerImage left, DockerImage right) => !(left == right);

    public static implicit operator DockerImage(string value) => Parse(value);
    public static implicit operator string(DockerImage image) => image.ToString();

    public static DockerImage Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string repository;
        string? tag, digest;
        if (value.Contains('@')) // this uses a digest
        {
            var parts = value.Split('@');
            repository = parts[0];
            tag = null;
            digest = parts[1];
        }
        else if (value.Contains(':')) // this uses a tag
        {
            var parts = value.Split(':');
            repository = parts[0];
            tag = parts[1];
            digest = null;
        }
        else // this is just the repository, assume latest tag
        {
            repository = value;
            tag = "latest";
            digest = null;
        }
        return new DockerImage(repository, tag, digest);
    }
}

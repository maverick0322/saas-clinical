using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Clinical.IntegrationTests;

public sealed class ArchitectureDependencyTests
{
    private static readonly string[] ForbiddenDomainAndApplicationPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "KubernetesClient",
        "MediatR",
        "Grpc",
        "StackExchange.Redis",
        "RabbitMQ"
    ];

    [Fact]
    public void ProductionProjects_MatchAllowedProjectReferenceGraph()
    {
        var repoRoot = FindRepositoryRoot();

        Assert.Empty(GetProjectReferences(repoRoot, "Clinical.Domain"));
        Assert.Equal(
            ["Clinical.Domain"],
            GetProjectReferences(repoRoot, "Clinical.Application"));
        Assert.Equal(
            ["Clinical.Application", "Clinical.Domain"],
            GetProjectReferences(repoRoot, "Clinical.Infrastructure"));
        Assert.Equal(
            ["Clinical.Application", "Clinical.Infrastructure"],
            GetProjectReferences(repoRoot, "Clinical.Api"));
    }

    [Fact]
    public void DomainAndApplicationProjects_DoNotDependOnWebOrInfrastructurePackages()
    {
        var repoRoot = FindRepositoryRoot();

        var domainDoc = LoadProjectDocument(repoRoot, "Clinical.Domain");
        Assert.Equal("Microsoft.NET.Sdk", domainDoc.Root?.Attribute("Sdk")?.Value);
        Assert.Empty(GetPackageReferences(domainDoc));

        var applicationDoc = LoadProjectDocument(repoRoot, "Clinical.Application");
        Assert.Equal("Microsoft.NET.Sdk", applicationDoc.Root?.Attribute("Sdk")?.Value);
        var applicationPackages = GetPackageReferences(applicationDoc);
        Assert.DoesNotContain(
            applicationPackages,
            package => HasForbiddenPrefix(package, ForbiddenDomainAndApplicationPrefixes));
    }

    [Fact]
    public void CompiledProductionAssemblies_RespectLayerBoundaries()
    {
        var domainRefs = GetCompiledAssemblyReferences("Clinical.Domain");
        Assert.DoesNotContain(domainRefs, name => name.StartsWith("Clinical.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(domainRefs, name => HasForbiddenPrefix(name, ForbiddenDomainAndApplicationPrefixes));

        var applicationRefs = GetCompiledAssemblyReferences("Clinical.Application");
        Assert.DoesNotContain(applicationRefs, name => string.Equals(name, "Clinical.Api", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationRefs, name => string.Equals(name, "Clinical.Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationRefs, name => HasForbiddenPrefix(name, ForbiddenDomainAndApplicationPrefixes));

        var infrastructureRefs = GetCompiledAssemblyReferences("Clinical.Infrastructure");
        Assert.DoesNotContain(infrastructureRefs, name => string.Equals(name, "Clinical.Api", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Saas.Clinical.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing 'Saas.Clinical.sln'.");
    }

    private static XDocument LoadProjectDocument(string repoRoot, string projectName)
    {
        var csprojPath = Path.Combine(repoRoot, "src", projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(csprojPath), $"Expected project file '{csprojPath}' to exist.");
        return XDocument.Load(csprojPath);
    }

    private static string[] GetProjectReferences(string repoRoot, string projectName)
    {
        var doc = LoadProjectDocument(repoRoot, projectName);
        return doc
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetPackageReferences(XDocument doc) =>
        doc
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] GetCompiledAssemblyReferences(string assemblyName)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        Assert.True(File.Exists(assemblyPath), $"Expected compiled assembly '{assemblyPath}' to exist.");

        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    private static bool HasForbiddenPrefix(string name, string[] prefixes) =>
        prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

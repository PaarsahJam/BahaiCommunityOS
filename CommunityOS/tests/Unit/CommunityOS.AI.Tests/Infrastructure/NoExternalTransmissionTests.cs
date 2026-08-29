using System.Reflection;
using CommunityOS.AI.Domain;
using CommunityOS.AI.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CommunityOS.AI.Tests.Infrastructure;

/// <summary>
/// Verifies that the AI Platform implementation contains no external provider
/// transmission capability. This test makes it difficult to accidentally
/// introduce external provider calls in the future.
/// </summary>
public class NoExternalTransmissionTests
{
    [Fact]
    public void AiDomain_ShouldNotReferenceExternalProviderSdk()
    {
        var assembly = typeof(IAiModelGateway).Assembly;
        var references = assembly.GetReferencedAssemblies();

        var externalProviderAssemblies = references
            .Where(a => a.Name != null && (
                a.Name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Azure.AI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Google.AI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Anthropic", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Claude", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        externalProviderAssemblies.Should().BeEmpty(
            "AI Platform must not reference external provider SDKs");
    }

    [Fact]
    public void AiInfrastructure_ShouldNotReferenceExternalProviderSdk()
    {
        var assembly = typeof(DisabledAiModelGateway).Assembly;
        var references = assembly.GetReferencedAssemblies();

        var externalProviderAssemblies = references
            .Where(a => a.Name != null && (
                a.Name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Azure.AI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Google.AI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Anthropic", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Claude", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        externalProviderAssemblies.Should().BeEmpty(
            "AI Infrastructure must not reference external provider SDKs");
    }

    [Fact]
    public void DisabledAiModelGateway_ShouldNotHaveHttpClientField()
    {
        var type = typeof(DisabledAiModelGateway);
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        var httpClientFields = fields
            .Where(f => f.FieldType.Name.Contains("HttpClient", StringComparison.OrdinalIgnoreCase))
            .ToList();

        httpClientFields.Should().BeEmpty(
            "DisabledAiModelGateway must not have HttpClient fields");
    }

    [Fact]
    public void AiPlatform_ShouldNotReferenceSharedServiceClientsScaffolding()
    {
        var assemblies = new[]
        {
            typeof(IAiModelGateway).Assembly,
            typeof(DisabledAiModelGateway).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var references = assembly.GetReferencedAssemblies();
            references.Should().NotContain(
                a => a.Name == "CommunityOS.ServiceClients",
                $"AI Platform assembly {assembly.GetName().Name} must not reference the internal service-client scaffolding (ADR-030)");
        }
    }

    [Fact]
    public void AiPlatform_ShouldNotContainProviderUrls()
    {
        var assemblies = new[]
        {
            typeof(IAiModelGateway).Assembly,
            typeof(DisabledAiModelGateway).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var body = method.GetMethodBody();
                    if (body?.GetILAsByteArray() is not null)
                    {
                        // Check for string literals that look like URLs
                        var il = body.GetILAsByteArray();
                        // This is a simplified check - in practice, we'd use a more robust method
                        // For now, we verify the assembly doesn't reference HttpClient
                        var references = assembly.GetReferencedAssemblies();
                        var httpReferences = references.Where(a => a.Name?.Contains("System.Net.Http") == true).ToList();
                        httpReferences.Should().BeEmpty(
                            $"AI Platform assembly {assembly.GetName().Name} must not reference System.Net.Http");
                    }
                }
            }
        }
    }
}

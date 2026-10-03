using CommunityOS.Identity.Domain.Aggregates;
using CommunityOS.Identity.Domain.Repositories;
using CommunityOS.Identity.Domain.ValueObjects;
using CommunityOS.Identity.Infrastructure.Persistence;
using CommunityOS.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Identity.IntegrationTests.Persistence;

public sealed class OwnedKeyRegressionTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=communityos_identity;Username=communityos;Password=communityos";

    private readonly List<Guid> _createdAccountIds = [];
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = ConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName)));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        _provider = services.BuildServiceProvider();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupAsync();
        if (_provider is not null)
            await _provider.DisposeAsync();
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;
        return new IdentityDbContext(options);
    }

    private async Task CleanupAsync()
    {
        if (_createdAccountIds.Count == 0) return;
        await using var db = CreateContext();
        foreach (var id in _createdAccountIds)
        {
            var account = await db.UserAccounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account is not null)
            {
                db.UserAccounts.Remove(account);
            }
        }
        await db.SaveChangesAsync();
        _createdAccountIds.Clear();
    }

    [Fact]
    public async Task AddCredential_WithDomainGeneratedGuid_Succeeds()
    {
        using var scope = _provider!.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        
        var email = Email.Create($"cred-{Guid.NewGuid():N}@example.org");
        var user = UserAccount.Register(email, "Password123!");
        var accountId = user.Id;
        _createdAccountIds.Add(accountId);
        
        await accounts.AddAsync(user);
        
        var fetched = await accounts.GetByIdAsync(accountId);
        fetched.Should().NotBeNull();
        var newCred = CommunityOS.Identity.Domain.Entities.Credential.Create(
            CommunityOS.Identity.Domain.Enumerations.CredentialType.Password, "NewPass123!");
        var expectedId = newCred.Id;
        var _ = fetched!.Credentials.ToList();
        typeof(UserAccount).GetField("_credentials", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(fetched, new List<CommunityOS.Identity.Domain.Entities.Credential>(fetched.Credentials) { newCred });
        
        await accounts.UpdateAsync(fetched!);
        
        var reloaded = await accounts.GetByIdAsync(accountId);
        reloaded.Should().NotBeNull();
        reloaded!.Credentials.Should().Contain(c => c.Id == expectedId);
    }

    [Fact]
    public async Task AddMfaMethod_WithDomainGeneratedGuid_Succeeds()
    {
        using var scope = _provider!.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        
        var email = Email.Create($"mfa-{Guid.NewGuid():N}@example.org");
        var user = UserAccount.Register(email, "Password123!");
        var accountId = user.Id;
        _createdAccountIds.Add(accountId);
        
        await accounts.AddAsync(user);
        
        var fetched = await accounts.GetByIdAsync(accountId);
        fetched.Should().NotBeNull();
        var newMfa = CommunityOS.Identity.Domain.Entities.MfaMethod.Create(
            CommunityOS.Identity.Domain.Enumerations.MfaMethodType.AuthenticatorApp, "secret");
        var expectedId = newMfa.Id;
        var _ = fetched!.Credentials.ToList();
        typeof(UserAccount).GetField("_mfaMethods", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(fetched, new List<CommunityOS.Identity.Domain.Entities.MfaMethod>(fetched.MfaMethods) { newMfa });
        
        await accounts.UpdateAsync(fetched!);
        
        var reloaded = await accounts.GetByIdAsync(accountId);
        reloaded.Should().NotBeNull();
        reloaded!.MfaMethods.Should().Contain(c => c.Id == expectedId);
    }

    [Fact]
    public async Task AddDevice_WithDomainGeneratedGuid_Succeeds()
    {
        using var scope = _provider!.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        
        var email = Email.Create($"dev-{Guid.NewGuid():N}@example.org");
        var user = UserAccount.Register(email, "Password123!");
        var accountId = user.Id;
        _createdAccountIds.Add(accountId);
        
        await accounts.AddAsync(user);
        
        var fetched = await accounts.GetByIdAsync(accountId);
        fetched.Should().NotBeNull();
        var newDev = CommunityOS.Identity.Domain.Entities.Device.Create("device", "ios", "agent");
        var expectedId = newDev.Id;
        var _ = fetched!.Credentials.ToList();
        typeof(UserAccount).GetField("_devices", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(fetched, new List<CommunityOS.Identity.Domain.Entities.Device>(fetched.Devices) { newDev });
        
        await accounts.UpdateAsync(fetched!);
        
        var reloaded = await accounts.GetByIdAsync(accountId);
        reloaded.Should().NotBeNull();
        reloaded!.Devices.Should().Contain(c => c.Id == expectedId);
    }

    [Fact]
    public async Task AddExternalIdentity_WithDomainGeneratedGuid_Succeeds()
    {
        using var scope = _provider!.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        
        var email = Email.Create($"ext-{Guid.NewGuid():N}@example.org");
        var user = UserAccount.Register(email, "Password123!");
        var accountId = user.Id;
        _createdAccountIds.Add(accountId);
        
        await accounts.AddAsync(user);
        
        var fetched = await accounts.GetByIdAsync(accountId);
        fetched.Should().NotBeNull();
        var newExt = CommunityOS.Identity.Domain.Entities.ExternalIdentity.Create("oidc", "sub123");
        var expectedId = newExt.Id;
        var _ = fetched!.Credentials.ToList();
        typeof(UserAccount).GetField("_externalIdentities", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(fetched, new List<CommunityOS.Identity.Domain.Entities.ExternalIdentity>(fetched.ExternalIdentities) { newExt });
        
        await accounts.UpdateAsync(fetched!);
        
        var reloaded = await accounts.GetByIdAsync(accountId);
        reloaded.Should().NotBeNull();
        reloaded!.ExternalIdentities.Should().Contain(c => c.Id == expectedId);
    }

    [Fact]
    public async Task ReplaceCredential_WithNewDomainGeneratedGuid_Succeeds()
    {
        using var scope = _provider!.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        
        var email = Email.Create($"rep-{Guid.NewGuid():N}@example.org");
        var user = UserAccount.Register(email, "Password123!");
        var accountId = user.Id;
        _createdAccountIds.Add(accountId);
        
        await accounts.AddAsync(user);
        
        var fetched = await accounts.GetByIdAsync(accountId);
        fetched.Should().NotBeNull();
        var firstCred = fetched!.Credentials[0];
        var oldId = firstCred.Id;
        var newCred = CommunityOS.Identity.Domain.Entities.Credential.Create(
            firstCred.Type, "NewPassword123!");
        var newId = newCred.Id;
        var _ = fetched!.Credentials.ToList();
        typeof(UserAccount).GetField("_credentials", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(fetched, new List<CommunityOS.Identity.Domain.Entities.Credential> { newCred });
        
        await accounts.UpdateAsync(fetched!);
        
        var reloaded = await accounts.GetByIdAsync(accountId);
        reloaded.Should().NotBeNull();
        reloaded!.Credentials.Should().Contain(c => c.Id == newId);
        reloaded!.Credentials.Should().NotContain(c => c.Id == oldId);
    }
}

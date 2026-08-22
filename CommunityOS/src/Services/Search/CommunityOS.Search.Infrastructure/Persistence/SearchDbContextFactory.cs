using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommunityOS.Search.Infrastructure.Persistence;
public sealed class SearchDbContextFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    public SearchDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SearchDbContext>().UseNpgsql("Host=localhost;Port=5432;Database=communityos_search;Username=communityos;Password=communityos", x => x.MigrationsAssembly(typeof(SearchDbContext).Assembly.FullName)).Options);
}

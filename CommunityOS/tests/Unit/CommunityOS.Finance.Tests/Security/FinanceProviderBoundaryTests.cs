using System.Reflection;
using CommunityOS.Finance.API.Controllers;
using CommunityOS.Finance.Application;
using CommunityOS.Finance.Domain.Aggregates;
using CommunityOS.Finance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace CommunityOS.Finance.Tests.Security;

/// <summary>
/// Locks the Finance external-provider and deferred-functionality boundary
/// (ADR-032 OQ-3/OQ-4/OQ-7/OQ-8): no payment provider is implemented,
/// referenced or configured, and no deferred capability (budget, invoice,
/// bookkeeping/payout surface) leaks into the Finance public surface.
/// Providers remain prohibited while OQ-4 is unresolved.
/// </summary>
public class FinanceProviderBoundaryTests
{
    private static readonly string[] ForbiddenTypeTerms =
    [
        "Payment", "Payout", "Provider", "Gateway", "Processor",
        "Stripe", "PayPal", "Plaid", "Chargebee",
        "Budget", "Invoice", "LedgerEntry", "Account",
    ];

    private static readonly Assembly[] FinanceAssemblies =
    [
        typeof(FinancialTransaction).Assembly,                       // Domain
        typeof(FinanceApplicationServiceExtensions).Assembly,        // Application
        typeof(FinanceDbContext).Assembly,                           // Infrastructure
        typeof(FundsController).Assembly,                            // API
    ];

    [Fact]
    public void No_finance_assembly_exposes_a_payment_provider_or_deferred_capability_type()
    {
        var leaked = FinanceAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => ForbiddenTypeTerms.Any(term =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        AssertCleanBoundary(leaked);
    }

    [Fact]
    public void No_api_controller_is_routed_for_payments_invoices_budgets_or_providers()
    {
        var leaked = typeof(FundsController).Assembly.GetExportedTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => ForbiddenTypeTerms.Any(term =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        AssertCleanBoundary(leaked);
    }

    private static void AssertCleanBoundary(IReadOnlyCollection<string> leaked)
    {
        leaked.Should().BeEmpty(
            "the Finance public surface must stay within ADR-032: {0}",
            string.Join(", ", leaked));
    }
}
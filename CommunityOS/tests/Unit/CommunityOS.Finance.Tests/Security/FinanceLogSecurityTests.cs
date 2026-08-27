using System.Reflection;
using CommunityOS.Finance.Application.Logging;
using Microsoft.Extensions.Logging;

namespace CommunityOS.Finance.Tests.Security;

/// <summary>
/// Locks the finance log boundary (ADR-032): every structured log template may
/// carry only stable identifiers and lifecycle metadata. No template may ever
/// include amounts, currencies, transaction types, descriptions or transfer
/// destinations, so application logs can never leak financial data.
/// </summary>
public class FinanceLogSecurityTests
{
    private static List<(string Method, string Message, int EventId)> Templates()
    {
        return typeof(FinanceLog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => new { Method = m.Name, Attribute = m.GetCustomAttribute<LoggerMessageAttribute>() })
            .Where(x => x.Attribute is not null)
            .Select(x => (
                x.Method,
                x.Attribute!.Message ?? string.Empty,
                x.Attribute.EventId))
            .ToList();
    }

    private static readonly string[] FinancialDataTerms =
        ["Amount", "amount", "Currency", "currency", "MinorUnits", "minor units",
         "Description", "description", "TransferDestination", "destination",
         "attribution", "wallet", "budget"];

    [Fact]
    public void Every_template_uses_structured_logging()
    {
        Templates().Should().NotBeEmpty();
        Templates().Should().AllSatisfy(t =>
        {
            t.Message.Should().Contain("{", because: $"{t.Method} must be a structured template");
            t.Message.Should().Contain("}", because: $"{t.Method} must be a structured template");
        });
    }

    [Fact]
    public void No_template_carries_financial_data_terms()
    {
        Templates().ForEach(t =>
        {
            var matches = FinancialDataTerms.Where(
                term => t.Message.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
            matches.Should().BeEmpty(
                $"{t.Method} ('{t.Message}') leaks: {string.Join(", ", matches)}");
        });
    }

    [Fact]
    public void Placeholders_are_restricted_to_stable_identifiers_and_lifecycle_metadata()
    {
        Templates().Should().AllSatisfy(t =>
        {
            var tokens = Tokenize(t.Message);
            tokens.Should().OnlyContain(
                token => token == "TransactionId" || token == "FundId" || token == "Message",
                $"{t.Method} ('{t.Message}') carries only stable ids");
        });
    }

    [Fact]
    public void Event_ids_are_unique_and_positive()
    {
        var ids = Templates().Select(t => t.EventId).ToList();

        ids.Should().OnlyContain(id => id > 0);
        ids.Distinct().Should().HaveCount(ids.Count);
    }

    private static string[] Tokenize(string message)
    {
        var tokens = new List<string>();
var start = message.IndexOf('{');
            while (start >= 0)
            {
                var end = message.IndexOf('}', start);
                if (end < 0) break;
                var token = message[(start + 1)..end].Trim();
                if (token.Length > 0)
                    tokens.Add(token.Split(':')[0].Trim());
                start = message.IndexOf('{', end + 1);
            }

        return tokens.Distinct().ToArray();
    }
}
namespace CommunityOS.Finance.Application.Permissions;

/// <summary>
/// Central registry of well-known Finance permission names — the ratified
/// baseline matrix (ADR-032). Only the six baseline capabilities exist in this
/// gate; deferred capabilities (budgets, wallet links, confidential
/// attribution, document references) are intentionally NOT registered here.
/// Enforced by the Finance application layer through the Authorization guard.
/// No local RBAC and no direct Authorization database access: every decision
/// is delegated to the Authorization service over HTTP (ADR-018/019).
/// </summary>
public static class FinancePermissions
{
    public const string FundRead = "finance.fund.read";
    public const string FundManage = "finance.fund.manage";
    public const string TransactionRead = "finance.transaction.read";
    public const string TransactionRecord = "finance.transaction.record";
    public const string TransactionApprove = "finance.transaction.approve";
    public const string TransactionAdmin = "finance.transaction.admin";

    /// <summary>All ratified baseline finance permissions (exactly six).</summary>
    public static readonly IReadOnlyList<string> All =
        [FundRead, FundManage, TransactionRead, TransactionRecord, TransactionApprove, TransactionAdmin];
}
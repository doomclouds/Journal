using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;

namespace Journal.Tests;

public sealed class JournalHarnessPromptTests
{
    [Fact]
    public void Build_SplitsHistoricalRawInputsFromCurrentUserMessage()
    {
        var date = JournalDate.From(new DateOnly(2026, 5, 12));
        var historicalRawInputs = new[]
        {
            new RawInput(
                "raw-1",
                date,
                new DateTimeOffset(2026, 5, 12, 7, 20, 0, TimeSpan.FromHours(8)),
                "manual",
                "历史输入：昨晚完成了 prompt harness 计划。")
        };
        var currentInput = new RawInput(
            "raw-2",
            date,
            new DateTimeOffset(2026, 5, 12, 7, 45, 0, TimeSpan.FromHours(8)),
            "manual",
            "当前输入：今天先把上下文拆开。");

        var request = JournalHarnessPrompt.Build(
            date,
            historicalRawInputs,
            currentInput,
            "# Current Draft\n\n已有草稿内容",
            "# Confirmed Entry\n\n已确认正文");

        Assert.Contains("historicalRawInputs", request.ProtectedContext, StringComparison.Ordinal);
        Assert.Contains("历史输入：昨晚完成了 prompt harness 计划。", request.ProtectedContext, StringComparison.Ordinal);
        Assert.DoesNotContain("当前输入：今天先把上下文拆开。", request.ProtectedContext, StringComparison.Ordinal);
        Assert.Equal("当前输入：今天先把上下文拆开。", request.UserMessage);
        Assert.Contains("只能调用允许的工具", request.SystemInstructions, StringComparison.Ordinal);
        Assert.Contains("JSON", request.SystemInstructions, StringComparison.OrdinalIgnoreCase);
    }
}

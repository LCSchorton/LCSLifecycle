using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lcs.Web.Pages;

public sealed class IndexModel : PageModel
{
    private static readonly SourceRecord[] Facts =
    [
        new("ada_example@school.example", "Ada Example"),
        new("jordan_example@school.example", "Jordan Example"),
        new("avery_example@school.example", "Avery Example"),
        new("avery_example@school.example", "Avery Example (second roster entry)")
    ];

    private static readonly SourceRecord[] Entra =
    [
        new("ADA_EXAMPLE@school.example", "Ada Example"),
        new("avery_example@school.example", "Avery Example"),
        new("taylor_example@school.example", "Taylor Example")
    ];

    private static readonly SourceRecord[] Apple =
    [
        new("DEMO-IPAD-001", "iPad"),
        new("DEMO-IPAD-002", "iPad")
    ];

    private static readonly SourceRecord[] Securly =
    [
        new("demo-ipad-001", "iPad, enrolled"),
        new("DEMO-IPAD-003", "iPad, enrolled")
    ];

    [BindProperty(SupportsGet = true)]
    public bool ReviewOnly { get; set; } = true;

    public IReadOnlyList<ComparisonRow> Students { get; private set; } = [];
    public IReadOnlyList<ComparisonRow> Devices { get; private set; } = [];
    public int StudentReviewCount => Students.Count(row => row.NeedsReview);
    public int DeviceReviewCount => Devices.Count(row => row.NeedsReview);

    public void OnGet()
    {
        Students = Compare(Facts, Entra, "FACTS", "Entra");
        Devices = Compare(Apple, Securly, "Apple School Manager", "Securly");
    }

    private static ComparisonRow[] Compare(
        SourceRecord[] left, SourceRecord[] right, string leftName, string rightName)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var leftByKey = left.ToLookup(record => record.Key.Trim(), comparer);
        var rightByKey = right.ToLookup(record => record.Key.Trim(), comparer);

        return leftByKey.Select(group => group.Key)
            .Union(rightByKey.Select(group => group.Key), comparer)
            .OrderBy(key => key, comparer)
            .Select(key =>
            {
                var leftRecords = leftByKey[key].Select(record => record.Label).ToArray();
                var rightRecords = rightByKey[key].Select(record => record.Label).ToArray();
                var result = (leftRecords.Length, rightRecords.Length) switch
                {
                    (> 1, _) or (_, > 1) => "Multiple records",
                    (0, _) => $"Missing from {leftName}",
                    (_, 0) => $"Missing from {rightName}",
                    _ => "Matched"
                };

                return new ComparisonRow(key, leftRecords, rightRecords, result,
                    leftRecords.Length != 1 || rightRecords.Length != 1);
            })
            .ToArray();
    }

    private sealed record SourceRecord(string Key, string Label);

    public sealed record ComparisonRow(
        string Key, IReadOnlyList<string> Left, IReadOnlyList<string> Right,
        string Result, bool NeedsReview);
}

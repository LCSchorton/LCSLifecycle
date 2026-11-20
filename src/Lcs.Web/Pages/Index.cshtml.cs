using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Lcs.Web.Pages;

[RequestSizeLimit(1100000)]
[RequestFormLimits(MemoryBufferThreshold = 1100000, MultipartBodyLengthLimit = 262144)]
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

    private const int MaxFileBytes = 262144;
    private const int MaxRows = 3000;

    public bool IsUploadPreview { get; private set; }

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

    public IActionResult OnPostPreview(
        IFormFile? facts, IFormFile? entra, IFormFile? apple, IFormFile? securly)
    {
        if (!User.IsInRole("Lcs.Administrator"))
        {
            return Forbid();
        }

        IsUploadPreview = true;
        ReviewOnly = false;

        if ((facts is null) != (entra is null) || (apple is null) != (securly is null) ||
            (facts is null && apple is null))
        {
            ModelState.AddModelError(string.Empty, "Choose both files for at least one comparison.");
            return Page();
        }

        try
        {
            var students = facts is null ? [] : Compare(
                ReadCsv(facts, "FACTS", "username", "displayName"),
                ReadCsv(entra!, "Entra", "username", "displayName"), "FACTS", "Entra");
            var devices = apple is null ? [] : Compare(
                ReadCsv(apple, "Apple School Manager", "serial", "description"),
                ReadCsv(securly!, "Securly", "serial", "description"), "Apple School Manager", "Securly");

            Students = students;
            Devices = devices;
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return Page();
    }

    private static SourceRecord[] ReadCsv(IFormFile file, string source, string keyHeader, string labelHeader)
    {
        if (file.Length is <= 0 or > MaxFileBytes)
        {
            throw new InvalidDataException($"{source}: choose a CSV file up to 256 KB.");
        }

        using var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, true));
        using var parser = new TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var records = new List<SourceRecord>();

        try
        {
            var headers = parser.ReadFields();
            if (headers is not { Length: 2 } ||
                !string.Equals(headers[0], keyHeader, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(headers[1], labelHeader, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{source}: the header must be {keyHeader},{labelHeader}.");
            }

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is not { Length: 2 } || fields.Any(field =>
                    string.IsNullOrWhiteSpace(field) || field.Length > 254 || field.Any(char.IsControl)))
                {
                    throw new InvalidDataException($"{source}: each row needs two non-empty fields of up to 254 characters.");
                }

                records.Add(new SourceRecord(fields[0], fields[1]));
                if (records.Count > MaxRows)
                {
                    throw new InvalidDataException($"{source}: the file has more than 3,000 records.");
                }
            }
        }
        catch (MalformedLineException)
        {
            throw new InvalidDataException($"{source}: a CSV row could not be read. Check its quotes and commas.");
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException($"{source}: save the file as UTF-8 CSV and try again.");
        }

        if (records.Count == 0)
        {
            throw new InvalidDataException($"{source}: the file contains no records. Check the export before comparing.");
        }

        return records.ToArray();
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

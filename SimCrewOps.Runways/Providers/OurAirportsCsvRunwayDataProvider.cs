using System.Globalization;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Services;

namespace SimCrewOps.Runways.Providers;

public sealed class OurAirportsCsvRunwayDataProvider : IRunwayDataProvider
{
    private readonly IReadOnlyDictionary<string, AirportRunwayCatalog> _catalogsByAirport;

    public OurAirportsCsvRunwayDataProvider(TextReader csvReader)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        _catalogsByAirport = LoadCatalogs(csvReader);
    }

    public static OurAirportsCsvRunwayDataProvider FromFile(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);
        using var reader = File.OpenText(csvPath);
        return new OurAirportsCsvRunwayDataProvider(reader);
    }

    public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIcao = NormalizeAirportIcao(airportIcao);
        _catalogsByAirport.TryGetValue(normalizedIcao, out var catalog);
        return Task.FromResult(catalog);
    }

    private static IReadOnlyDictionary<string, AirportRunwayCatalog> LoadCatalogs(TextReader csvReader)
    {
        var rowsByAirport = new Dictionary<string, List<RunwayEnd>>(StringComparer.OrdinalIgnoreCase);
        var header = csvReader.ReadLine();
        if (string.IsNullOrWhiteSpace(header))
        {
            return new Dictionary<string, AirportRunwayCatalog>(StringComparer.OrdinalIgnoreCase);
        }

        var headerIndex = ParseCsvLine(header)
            .Select((name, index) => new { Name = name, Index = index })
            .ToDictionary(entry => entry.Name, entry => entry.Index, StringComparer.OrdinalIgnoreCase);

        string? line;
        while ((line = csvReader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            var airportIcao = Read(fields, headerIndex, "airport_ident");
            if (string.IsNullOrWhiteSpace(airportIcao))
            {
                continue;
            }

            var runwayEnds = CreateRunwayEnds(fields, headerIndex, airportIcao);
            if (runwayEnds.Count == 0)
            {
                continue;
            }

            var bucket = rowsByAirport.TryGetValue(airportIcao, out var existing)
                ? existing
                : rowsByAirport[airportIcao] = new List<RunwayEnd>();

            bucket.AddRange(runwayEnds);
        }

        return rowsByAirport.ToDictionary(
            entry => NormalizeAirportIcao(entry.Key),
            entry => new AirportRunwayCatalog
            {
                AirportIcao = NormalizeAirportIcao(entry.Key),
                DataSource = RunwayDataSource.OurAirportsFallback,
                Runways = entry.Value
                    .OrderBy(runway => runway.RunwayIdentifier, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<RunwayEnd> CreateRunwayEnds(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> headerIndex, string airportIcao)
    {
        var runways = new List<RunwayEnd>(capacity: 2);
        var normalizedIcao = NormalizeAirportIcao(airportIcao);
        var lengthFeet = ReadDouble(fields, headerIndex, "length_ft");

        AddRunwayEndIfValid("le");
        AddRunwayEndIfValid("he");
        return runways;

        void AddRunwayEndIfValid(string prefix)
        {
            var ident = Read(fields, headerIndex, $"{prefix}_ident");
            var latitude = ReadNullableDouble(fields, headerIndex, $"{prefix}_latitude_deg");
            var longitude = ReadNullableDouble(fields, headerIndex, $"{prefix}_longitude_deg");
            var heading = ReadNullableDouble(fields, headerIndex, $"{prefix}_heading_degT");
            var displacedThresholdFeet = ReadDouble(fields, headerIndex, $"{prefix}_displaced_threshold_ft");

            if (string.IsNullOrWhiteSpace(ident) || latitude is null || longitude is null || heading is null)
            {
                return;
            }

            // OurAirports stores end coordinates plus displaced threshold distance.
            var threshold = GeoMath.DestinationPoint(latitude.Value, longitude.Value, heading.Value, displacedThresholdFeet);

            runways.Add(new RunwayEnd
            {
                AirportIcao = normalizedIcao,
                RunwayIdentifier = ident.Trim(),
                TrueHeadingDegrees = NormalizeHeading(heading.Value),
                LengthFeet = lengthFeet,
                ThresholdLatitude = threshold.Latitude,
                ThresholdLongitude = threshold.Longitude,
                DisplacedThresholdFeet = displacedThresholdFeet,
                DataSource = RunwayDataSource.OurAirportsFallback,
            });
        }
    }

    private static string NormalizeAirportIcao(string airportIcao) =>
        airportIcao.Trim().ToUpperInvariant();

    private static double NormalizeHeading(double headingDegrees)
    {
        var normalized = headingDegrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static string Read(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> headerIndex, string columnName)
    {
        if (!headerIndex.TryGetValue(columnName, out var index) || index >= fields.Count)
        {
            return string.Empty;
        }

        return fields[index].Trim();
    }

    private static double ReadDouble(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> headerIndex, string columnName) =>
        ReadNullableDouble(fields, headerIndex, columnName) ?? 0;

    private static double? ReadNullableDouble(IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> headerIndex, string columnName)
    {
        var raw = Read(fields, headerIndex, columnName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    values.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        values.Add(current.ToString());
        return values;
    }
}

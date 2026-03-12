using ClosedXML.Excel;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TranscodificaBlazor.Components.Service;

public class Service : IService
{
    public async Task<(string json, string csv)> TranscodificaAsync(Dictionary<string, string> countries, IBrowserFile excelfile)
    {
        List<Location> locations = [];

        try
        {
            using var stream = excelfile.OpenReadStream(maxAllowedSize: 15_000_000); // 15 MB

            // Copia il contenuto in un MemoryStream in modo asincrono
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reimposta la posizione all'inizio

            var workbook = new XLWorkbook(memoryStream);
            var worksheet = workbook.Worksheet(1);

            foreach (var row in worksheet.RowsUsed().Skip(2))
            {
                if (row.Cell(9).GetValue<int>() == 1)
                {
                    var location = new Location();
                    location.CountryCodeISO_160 = row.Cell(1).GetValue<string>();
                    location.LocationPrimaryCode160 = row.Cell(2).GetValue<string>();
                    location.PrimaryLocationName160 = row.Cell(6).GetValue<string>();
                    location.PrimaryLocationNameASCII_160 = row.Cell(7).GetValue<string>();
                    string countryCode = row.Cell(1).GetValue<string>();
                    location.CountryCodeUIC_142 = countries[countryCode];

                    //I prossimi valori saranno inseriti da un ulteriore file
                    location.LocationCode_142 = row.Cell(2).GetValue<String>().PadLeft(6, '0');
                    location.LocationName_142 = row.Cell(6).GetValue<string>();

                    locations.Add(location);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        if (locations.Count > 0)
        {
            var wrappedLocations = locations.Select(loc => new LocationWrapper { Location = loc }).ToList();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonString = JsonSerializer.Serialize(wrappedLocations, options);
            string csvString = GeneraCsv(locations);
            return (jsonString, csvString);
        }
        else
        {
            Console.WriteLine("Nessuna location trovata da elaborare.");
            return (string.Empty, string.Empty);
        }
    }

    private string GeneraCsv(List<Location> dati)
    {
        if (dati == null || dati.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        // Header
        var headers = typeof(Location).GetProperties().Select(p => p.Name).ToList();
        sb.AppendLine(string.Join(";", headers));

        // Righe
        foreach (var riga in dati)
        {
            var valori = typeof(Location).GetProperties().Select(p => p.GetValue(riga)?.ToString() ?? "").ToList();
            sb.AppendLine(string.Join(";", valori));
        }

        return sb.ToString();
    }

    public async Task<Dictionary<string, string>?> ProcessaFileCsv(IBrowserFile file)
    {
        if (!IsValidCsv(file))
        {
            return null;
        }

        var dict = new Dictionary<string, string>();

        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10 MB max
        using var reader = new StreamReader(stream);

        // Salta l'header
        await reader.ReadLineAsync();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(';');

            if (parts.Length < 2)
                continue;

            var iso = parts[0].Trim();
            var uic = parts[1].Trim();

            if (!string.IsNullOrEmpty(iso) && !string.IsNullOrEmpty(uic))
                dict[iso] = uic;
        }

        return dict;
    }

    public bool IsValidExcel(IBrowserFile file)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var validExtensions = new[] { ".xlsx", ".xls" };

        return validExtensions.Contains(extension) &&
               (file.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                file.ContentType == "application/vnd.ms-excel");
    }

    private bool IsValidCsv(IBrowserFile file)
    {
        if (file == null) return false;

        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        return extension == ".csv" &&
               (file.ContentType == "text/csv" || file.ContentType == "application/vnd.ms-excel");
    }
}

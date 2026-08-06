using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RSYInventory.Data;
using RSYInventory.Data.Enums;
using RSYInventory.Data.Services;

namespace RSYInventory.Web.Services;

public sealed class DriverReportPdfService
{
    static DriverReportPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GeneratePdf(
        IReadOnlyList<DriverReportRow> rows,
        string? driverFilterName,
        DateOnly? from,
        DateOnly? to,
        string includeLabel)
    {
        var titleDriver = string.IsNullOrWhiteSpace(driverFilterName) ? "Todos los choferes" : driverFilterName.Trim();
        var range = FormatRange(from, to);
        var generatedAt = YardTimeZone.NowEastern().ToString("yyyy-MM-dd HH:mm") + " ET";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("RSY · Reporte de recolección por chofer")
                        .FontSize(14).SemiBold().FontColor(Colors.Grey.Darken4);
                    col.Item().PaddingTop(4).Text($"Chofer: {titleDriver}").FontSize(10);
                    col.Item().Text($"Rango: {range} · Incluye: {includeLabel}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Generado: {generatedAt} · {rows.Count} fila(s)")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2.0f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(0.9f);
                        c.RelativeColumn(1.1f);
                    });

                    table.Header(h =>
                    {
                        foreach (var label in new[]
                                 {
                                     "Chofer", "VIN", "Año / Modelo", "Precio", "Dirección",
                                     "Programado", "Ventana", "Status", "Recogida (ET)"
                                 })
                        {
                            h.Cell().Element(HeaderCellStyle).Text(label).SemiBold().FontSize(8);
                        }
                    });

                    var i = 0;
                    foreach (var r in rows)
                    {
                        var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        var model = $"{r.Year?.ToString() ?? "—"} {r.Make} {r.Model}".Trim();
                        var status = r.Status == ScheduledPickupStatus.Pending ? "Pendiente" : "Recogido";
                        var price = r.PurchasePrice is null
                            ? "—"
                            : r.PurchasePrice.Value.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

                        void Cell(string text) =>
                            table.Cell().Element(c => BodyCellStyle(c, bg)).Text(text).FontSize(8);

                        Cell(r.DriverName);
                        Cell(r.Vin);
                        Cell(model);
                        Cell(price);
                        Cell(r.PickupAddress ?? "—");
                        Cell(r.ScheduledPickupDate.ToString("yyyy-MM-dd"));
                        Cell(string.IsNullOrWhiteSpace(r.ScheduledPickupWindow) ? "—" : r.ScheduledPickupWindow!);
                        Cell(status);
                        Cell(r.PickedUpAtEastern?.ToString("yyyy-MM-dd HH:mm") ?? "—");
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer HeaderCellStyle(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(4);

    private static IContainer BodyCellStyle(IContainer c, string bg) =>
        c.Background(bg).Padding(4);

    private static string FormatRange(DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null) return "Sin límite de fechas";
        if (from is not null && to is not null)
            return $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}";
        if (from is not null) return $"Desde {from:yyyy-MM-dd}";
        return $"Hasta {to:yyyy-MM-dd}";
    }
}

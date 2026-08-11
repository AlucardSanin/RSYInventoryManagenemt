using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RSYInventory.Data.Entities;

namespace RSYInventory.Web.Services;

public sealed class RecycleWeeklyInvoicePdfService
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private readonly IWebHostEnvironment _env;

    static RecycleWeeklyInvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RecycleWeeklyInvoicePdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] GeneratePdf(
        int invoiceNumber,
        DateOnly invoiceDate,
        DateOnly weekStart,
        DateOnly weekEnd,
        RecycleBillToCompany billTo,
        IReadOnlyList<RecycleLoad> loads)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "invoice", "rodriguez-trucking-logo.png");
        var total = loads.Sum(l => l.RateUsd);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, logoPath, invoiceDate, invoiceNumber));
                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Element(c => ComposeBillTo(c, billTo));
                    col.Item().PaddingTop(14).Element(c => ComposeTable(c, loads));
                    col.Item().PaddingTop(4).AlignRight().Row(row =>
                    {
                        row.ConstantItem(80).AlignRight().Text("TOTAL").Bold().FontSize(10);
                        row.ConstantItem(90).Background(Colors.Grey.Lighten3).Padding(4)
                            .AlignRight().Text(total.ToString("C2", Us)).Bold().FontSize(10);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string logoPath, DateOnly invoiceDate, int invoiceNumber)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("Rodriguez Construction & Trucking Corp").FontSize(11);
                if (File.Exists(logoPath))
                {
                    left.Item().PaddingTop(6).Width(110).Image(logoPath);
                }

                left.Item().PaddingTop(6).Text("4417 US 70 Bus Hwy W").FontSize(9);
                left.Item().Text("Clayton NC 27520").FontSize(9);
                left.Item().Text("984-274-5081").FontSize(9);
            });

            row.ConstantItem(180).AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text("INVOICE").FontSize(28).FontColor(Colors.Grey.Medium);
                right.Item().AlignRight().PaddingTop(4).Text($"#{invoiceNumber}").FontSize(11).FontColor(Colors.Grey.Darken2);
                right.Item().AlignRight().PaddingTop(18).Text(t =>
                {
                    t.Span("DATE: ").Bold();
                    t.Span(invoiceDate.ToString("MMMM d, yyyy", Us));
                });
            });
        });
    }

    private static void ComposeBillTo(IContainer container, RecycleBillToCompany billTo)
    {
        container.Column(col =>
        {
            col.Item().Text("BILL TO:").Bold().FontSize(10);
            col.Item().Text(billTo.CompanyName).FontSize(10);
            col.Item().Text(billTo.AddressLine).FontSize(9);
            if (!string.IsNullOrWhiteSpace(billTo.ContactLine))
                col.Item().Text(billTo.ContactLine).FontSize(9);
            col.Item().PaddingTop(10).Background(Colors.Grey.Lighten2).Padding(4)
                .AlignCenter().Text("TICKET").Bold().FontSize(9);
        });
    }

    private static void ComposeTable(IContainer container, IReadOnlyList<RecycleLoad> loads)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f); // ticket
                c.RelativeColumn(1.0f); // date
                c.RelativeColumn(0.9f); // truck
                c.RelativeColumn(0.9f); // rate
                c.RelativeColumn(0.7f); // loads
                c.RelativeColumn(0.9f); // t/loads
                c.RelativeColumn(1.0f); // amount
            });

            table.Header(h =>
            {
                foreach (var label in new[] { "TICKET", "DATE", "TRUCK", "RATE", "LOADS", "T/LOADS", "AMOUNT" })
                    h.Cell().Element(HeaderCellStyle).AlignCenter().Text(label).Bold().FontSize(8);
            });

            DateOnly? lastDate = null;
            var i = 0;
            foreach (var load in loads)
            {
                if (lastDate is not null && load.LoadDate != lastDate)
                {
                    // spacer row between date groups
                    for (var s = 0; s < 7; s++)
                        table.Cell().Padding(2).Text(" ");
                }

                lastDate = load.LoadDate;
                var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                var amount = load.RateUsd;
                Cell(table, load.LoadExternalId, bg);
                Cell(table, load.LoadDate.ToString("M/d/yyyy", Us), bg);
                Cell(table, load.TruckNumber ?? "—", bg);
                Cell(table, amount.ToString("C2", Us), bg);
                Cell(table, "1", bg);
                Cell(table, amount.ToString("C2", Us), bg);
                table.Cell().Element(c => BodyCellStyle(c, Colors.Grey.Lighten3))
                    .AlignCenter().Text(amount.ToString("C2", Us)).FontSize(8);
            }
        });
    }

    private static IContainer HeaderCellStyle(IContainer c) =>
        c.Background(Colors.Grey.Lighten2).Padding(4);

    private static IContainer BodyCellStyle(IContainer c, string bg) =>
        c.Background(bg).Padding(4);

    private static void Cell(TableDescriptor table, string text, string bg) =>
        table.Cell().Element(c => BodyCellStyle(c, bg)).AlignCenter().Text(text).FontSize(8);
}

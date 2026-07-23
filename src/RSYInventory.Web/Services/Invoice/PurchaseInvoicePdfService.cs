using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RSYInventory.Data.Entities;

namespace RSYInventory.Web.Services.Invoice;

public sealed class PurchaseInvoicePdfService
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly string HeaderGrey = "#E6E6E8";

    private readonly CompanyInvoiceOptions _company;
    private readonly IWebHostEnvironment _env;

    static PurchaseInvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PurchaseInvoicePdfService(IOptions<CompanyInvoiceOptions> company, IWebHostEnvironment env)
    {
        _company = company.Value;
        _env = env;
    }

    public PurchaseInvoiceModel BuildModel(Vehicle vehicle, int invoiceNumber)
    {
        var yearMakeModel = string.Join(' ', new[]
        {
            vehicle.Year?.ToString(Us),
            vehicle.Make,
            vehicle.Model
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(yearMakeModel))
            yearMakeModel = "Vehicle purchase";

        return new PurchaseInvoiceModel
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = vehicle.AcquiredAt.Date,
            PaymentMethod = vehicle.PaymentMethod,
            CompanyName = _company.Name,
            CompanyAddressLine1 = _company.AddressLine1,
            CompanyCityStateZip = _company.CityStateZip,
            CompanyEmail = _company.Email,
            BilledToName = vehicle.SellerName,
            BilledToPhone = vehicle.SellerPhone,
            BilledToEmail = vehicle.SellerEmail,
            BilledToLocation = vehicle.AcquisitionLocation,
            ItemDescription = yearMakeModel,
            ItemVin = vehicle.Vin,
            Amount = vehicle.PurchasePrice ?? 0m
        };
    }

    public byte[] GeneratePdf(PurchaseInvoiceModel model)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "invoice", "rsy-invoice-logo.png");
        var footerPath = Path.Combine(_env.WebRootPath, "invoice", "rsy-invoice-footer.png");
        var money = model.Amount.ToString("C0", Us);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginLeft(56);
                page.MarginRight(56);
                page.MarginTop(36);
                page.MarginBottom(0);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black).FontFamily(Fonts.Arial));
                page.PageColor(Colors.White);

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c =>
                        {
                            if (File.Exists(logoPath))
                                c.Height(58).Width(150).Image(logoPath).FitArea();
                            else
                                c.Text("RODRÍGUEZ SALVAGE YARD").Bold().FontSize(14);
                        });

                        row.ConstantItem(140).AlignRight().AlignMiddle()
                            .Text($"NO. {model.InvoiceNumber:000000}")
                            .Bold().FontSize(14);
                    });

                    col.Item().PaddingTop(28).Text("INVOICE").Bold().FontSize(34).FontColor(Colors.Black);

                    col.Item().PaddingTop(18)
                        .Text(text =>
                        {
                            text.Span("Date: ").Bold();
                            text.Span(model.InvoiceDate.ToString("MMMM d, yyyy", Us));
                        });

                    col.Item().PaddingTop(18).Row(row =>
                    {
                        row.RelativeItem().Column(from =>
                        {
                            from.Item().Text("From:").Bold();
                            from.Item().PaddingTop(6).Text(model.CompanyName);
                            from.Item().Text(model.CompanyAddressLine1);
                            from.Item().Text(model.CompanyCityStateZip);
                            from.Item().Text(model.CompanyEmail);
                        });

                        row.RelativeItem().Column(billed =>
                        {
                            billed.Item().Text("Billed to").Bold();
                            billed.Item().PaddingTop(6).Text(
                                string.IsNullOrWhiteSpace(model.BilledToName) ? "—" : model.BilledToName!);

                            foreach (var line in SplitAddressLines(model.BilledToLocation))
                                billed.Item().Text(line);

                            if (!string.IsNullOrWhiteSpace(model.BilledToEmail))
                                billed.Item().Text(model.BilledToEmail!);
                        });
                    });

                    col.Item().PaddingTop(28).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4.2f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(TableHeaderCell).Text("Item");
                            header.Cell().Element(TableHeaderCell).AlignRight().Text("Price");
                            header.Cell().Element(TableHeaderCell).AlignRight().Text("Amount");
                        });

                        table.Cell().Element(TableBodyCell).Text(model.ItemDescription);
                        table.Cell().Element(TableBodyCell).AlignRight().Text(money);
                        table.Cell().Element(TableBodyCell).AlignRight().Text(money);
                    });

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4.2f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                        });

                        table.Cell().PaddingHorizontal(10);
                        table.Cell().PaddingHorizontal(10).AlignRight().Text("Total").Bold().FontSize(11);
                        table.Cell().PaddingHorizontal(10).AlignRight().Text(money).Bold().FontSize(11);
                    });

                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);

                    col.Item().PaddingTop(22)
                        .Text(text =>
                        {
                            text.Span("Payment method: ").Bold();
                            text.Span(string.IsNullOrWhiteSpace(model.PaymentMethod) ? "—" : model.PaymentMethod);
                        });

                    col.Item().PaddingTop(10)
                        .Text(text =>
                        {
                            text.Span("Note: ").Bold();
                            text.Span("Thank you for choosing us!");
                        });
                });

                // Full-bleed footer: FitWidth spans the page; FitArea left a white gap on the right.
                page.Footer().Height(168).PaddingHorizontal(-56).Element(footer =>
                {
                    if (File.Exists(footerPath))
                        footer.Width(PageSizes.Letter.Width).Image(footerPath).FitWidth();
                    else
                        footer.Height(168).Background("#1F424C");
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GeneratePdf(Vehicle vehicle, int invoiceNumber)
        => GeneratePdf(BuildModel(vehicle, invoiceNumber));

    private static IEnumerable<string> SplitAddressLines(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            yield break;

        foreach (var line in location.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return line;
    }

    private static IContainer TableHeaderCell(IContainer c) =>
        c.Background(HeaderGrey)
            .PaddingVertical(8)
            .PaddingHorizontal(10)
            .DefaultTextStyle(x => x.Bold().FontColor(Colors.Black));

    private static IContainer TableBodyCell(IContainer c) =>
        c.PaddingVertical(12)
            .PaddingHorizontal(10)
            .DefaultTextStyle(x => x.FontColor(Colors.Black));
}

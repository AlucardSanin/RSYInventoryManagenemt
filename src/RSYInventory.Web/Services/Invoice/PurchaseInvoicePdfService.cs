using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RSYInventory.Data.Entities;

namespace RSYInventory.Web.Services.Invoice;

/// <summary>
/// Generates a Vehicle Purchase Acknowledgement / sales receipt.
/// RSY is the buyer; the individual is the seller (not a customer invoice).
/// Designed to fit on a single Letter page.
/// </summary>
public sealed class PurchaseInvoicePdfService
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    private static readonly string HeaderGrey = "#E6E6E8";
    private static readonly string Muted = "#4B5563";

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

    public PurchaseInvoiceModel BuildModel(Vehicle vehicle, int documentNumber)
    {
        var description = string.Join(' ', new[]
        {
            vehicle.Year?.ToString(Us),
            vehicle.Make,
            vehicle.Model
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(description))
            description = "Vehicle";

        return new PurchaseInvoiceModel
        {
            DocumentNumber = documentNumber,
            PurchaseDate = vehicle.AcquiredAt.Date,
            PaymentMethod = vehicle.PaymentMethod,
            BuyerName = _company.Name,
            BuyerAddressLine1 = _company.AddressLine1,
            BuyerCityStateZip = _company.CityStateZip,
            BuyerEmail = _company.Email,
            SellerName = vehicle.SellerName,
            SellerPhone = vehicle.SellerPhone,
            SellerEmail = vehicle.SellerEmail,
            SellerAddress = vehicle.AcquisitionLocation,
            VehicleYear = vehicle.Year,
            VehicleMake = vehicle.Make,
            VehicleModel = vehicle.Model,
            VehicleVin = vehicle.Vin,
            VehicleDescription = description,
            PurchasePrice = vehicle.PurchasePrice ?? 0m
        };
    }

    public byte[] GeneratePdf(PurchaseInvoiceModel model)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "invoice", "rsy-invoice-logo.png");
        var footerPath = Path.Combine(_env.WebRootPath, "invoice", "rsy-invoice-footer.png");
        var money = model.PurchasePrice.ToString("C", Us);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginLeft(44);
                page.MarginRight(44);
                page.MarginTop(24);
                page.MarginBottom(0);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Colors.Black).FontFamily(Fonts.Arial));
                page.PageColor(Colors.White);

                page.Content().Column(col =>
                {
                    // Header: logo + document number (stays on page 1)
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c =>
                        {
                            if (File.Exists(logoPath))
                                c.Height(64).Width(168).Image(logoPath).FitArea();
                            else
                                c.Text("RODRÍGUEZ SALVAGE YARD").Bold().FontSize(14);
                        });

                        row.ConstantItem(150).AlignRight().AlignMiddle()
                            .Text($"NO. {model.DocumentNumber:000000}")
                            .Bold().FontSize(13);
                    });

                    // One flexible region: center the body in leftover space (do NOT use two ExtendVertical).
                    col.Item().ExtendVertical().AlignMiddle().Column(body =>
                    {
                        body.Item().Text("VEHICLE PURCHASE ACKNOWLEDGEMENT")
                            .Bold().FontSize(15).FontColor(Colors.Black);

                        body.Item().PaddingTop(2).Text("Bill of sale / ownership transfer receipt")
                            .FontSize(8).FontColor(Muted);

                        body.Item().PaddingTop(8)
                            .Text(text =>
                            {
                                text.Span("Purchase date: ").Bold();
                                text.Span(model.PurchaseDate.ToString("MMMM d, yyyy", Us));
                            });

                        body.Item().PaddingTop(10).Element(c => SectionTitle(c, "Vehicle"));
                        body.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Y/M/M: ").FontColor(Muted);
                            text.Span($"{Blank(model.VehicleYear?.ToString(Us))} / {Blank(model.VehicleMake)} / {Blank(model.VehicleModel)}");
                            text.Span("    VIN: ").FontColor(Muted);
                            text.Span(Blank(model.VehicleVin)).SemiBold();
                        });

                        body.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().PaddingRight(8).Column(seller =>
                            {
                                seller.Item().Element(c => SectionTitle(c, "Seller"));
                                seller.Item().PaddingTop(4).Text(Blank(model.SellerName)).SemiBold();
                                foreach (var line in SplitLines(model.SellerAddress))
                                    seller.Item().Text(line).FontSize(8.5f);
                                if (!string.IsNullOrWhiteSpace(model.SellerPhone))
                                    seller.Item().Text(model.SellerPhone!).FontSize(8.5f);
                                if (!string.IsNullOrWhiteSpace(model.SellerEmail))
                                    seller.Item().Text(model.SellerEmail!).FontSize(8.5f);
                            });

                            row.RelativeItem().PaddingLeft(8).Column(buyer =>
                            {
                                buyer.Item().Element(c => SectionTitle(c, "Buyer"));
                                buyer.Item().PaddingTop(4).Text(model.BuyerName).SemiBold();
                                buyer.Item().Text(model.BuyerAddressLine1).FontSize(8.5f);
                                buyer.Item().Text(model.BuyerCityStateZip).FontSize(8.5f);
                                buyer.Item().Text(model.BuyerEmail).FontSize(8.5f);
                            });
                        });

                        body.Item().PaddingTop(10).Element(c => SectionTitle(c, "Transaction"));
                        body.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3.5f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableHeaderCell).Text("Description");
                                header.Cell().Element(TableHeaderCell).AlignRight().Text("Price");
                                header.Cell().Element(TableHeaderCell).AlignRight().Text("Amount");
                            });

                            table.Cell().Element(TableBodyCell).Text($"Purchase of {model.VehicleDescription}");
                            table.Cell().Element(TableBodyCell).AlignRight().Text(money);
                            table.Cell().Element(TableBodyCell).AlignRight().Text(money);
                        });

                        body.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);
                        body.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(90).AlignRight().Text("Sale price").Bold();
                            row.ConstantItem(80).AlignRight().Text(money).Bold();
                        });
                        body.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);

                        body.Item().PaddingTop(6)
                            .Text(text =>
                            {
                                text.Span("Payment method: ").Bold();
                                text.Span(Blank(model.PaymentMethod));
                            });

                        body.Item().PaddingTop(10).Element(c => SectionTitle(c, "Condition"));
                        body.Item().PaddingTop(4).Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8.5f));
                            text.Span("AS-IS. ").Bold();
                            text.Span(
                                "Seller certifies legal ownership and transfers the vehicle to Buyer with no warranties. " +
                                "Buyer acknowledges receipt of ownership and possession on the purchase date above.");
                        });

                        body.Item().PaddingTop(14).Row(row =>
                        {
                            row.RelativeItem().PaddingRight(14).Column(sig =>
                            {
                                sig.Item().Text("Seller signature").Bold().FontSize(9);
                                sig.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Signature").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Printed name").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Date").FontSize(7.5f).FontColor(Muted);
                            });

                            row.RelativeItem().PaddingLeft(14).Column(sig =>
                            {
                                sig.Item().Text("Buyer (authorized)").Bold().FontSize(9);
                                sig.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Signature").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Printed name").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Date").FontSize(7.5f).FontColor(Muted);
                            });
                        });
                    });
                });

                // Full-bleed footer on the page background (outside content margins).
                var pageWidth = PageSizes.Letter.Width;
                var footerHeight = pageWidth * (456f / 1836f);
                page.MarginBottom(footerHeight);
                page.Background().AlignBottom().Height(footerHeight).Element(footer =>
                {
                    if (File.Exists(footerPath))
                        footer.Image(footerPath).FitWidth();
                    else
                        footer.Background("#1F424C");
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GeneratePdf(Vehicle vehicle, int documentNumber)
        => GeneratePdf(BuildModel(vehicle, documentNumber));

    private static void SectionTitle(IContainer container, string title)
        => container.Background(HeaderGrey).PaddingVertical(4).PaddingHorizontal(8)
            .Text(title).Bold().FontSize(9);

    private static string Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static IEnumerable<string> SplitLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (var line in value.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return line;
    }

    private static IContainer TableHeaderCell(IContainer c) =>
        c.Background(HeaderGrey)
            .PaddingVertical(5)
            .PaddingHorizontal(6)
            .DefaultTextStyle(x => x.Bold().FontSize(9).FontColor(Colors.Black));

    private static IContainer TableBodyCell(IContainer c) =>
        c.PaddingVertical(6)
            .PaddingHorizontal(6)
            .DefaultTextStyle(x => x.FontColor(Colors.Black));
}

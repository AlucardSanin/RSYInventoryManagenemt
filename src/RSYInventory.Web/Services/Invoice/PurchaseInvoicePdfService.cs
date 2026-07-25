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
    private readonly DocuSealOptions _docuSeal;
    private readonly IWebHostEnvironment _env;

    static PurchaseInvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PurchaseInvoicePdfService(
        IOptions<CompanyInvoiceOptions> company,
        IOptions<DocuSealOptions> docuSeal,
        IWebHostEnvironment env)
    {
        _company = company.Value;
        _docuSeal = docuSeal.Value;
        _env = env;
    }

    public PurchaseInvoiceModel BuildModel(Vehicle vehicle, int documentNumber, InvoiceTemplate? template = null)
    {
        var description = string.Join(' ', new[]
        {
            vehicle.Year?.ToString(Us),
            vehicle.Make,
            vehicle.Model
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(description))
            description = "Vehicle";

        var buyerName = template?.BuyerCompanyName ?? _company.Name;
        var buyerAuth = template?.BuyerAuthorizedName
                        ?? "RODRIGUEZ SALVAGE YARD";
        var address = template?.AddressLine1 ?? _company.AddressLine1;
        var city = template?.CityStateZip ?? _company.CityStateZip;
        // Prefer template email; do not fall back to RSY email when the template leaves it blank (e.g. Saul Motors).
        var email = template is null
            ? _company.Email
            : (template.Email ?? string.Empty);
        var logo = template?.LogoRelativePath ?? "/invoice/rsy-invoice-logo.png";
        var useRsyFooter = !IsSaulMotorsTemplate(template);

        return new PurchaseInvoiceModel
        {
            DocumentNumber = documentNumber,
            PurchaseDate = vehicle.AcquiredAt.Date,
            PaymentMethod = vehicle.PaymentMethod,
            BuyerName = buyerName,
            BuyerAuthorizedName = buyerAuth,
            BuyerAddressLine1 = address,
            BuyerCityStateZip = city,
            BuyerEmail = email,
            TemplateName = template?.Name,
            LogoRelativePath = logo,
            UseRsyFooter = useRsyFooter,
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
        => BuildReceiptPdf(model, ReceiptLayoutMode.Filled);

    /// <summary>Same branded layout with empty value areas — upload this to DocuSeal and place fields manually.</summary>
    public byte[] GenerateDocuSealBlankTemplatePdf(InvoiceTemplate? template = null)
        => BuildReceiptPdf(BuildShellModel(template), ReceiptLayoutMode.Blank);

    /// <summary>Same layout with [FieldName] markers showing where to place each DocuSeal field.</summary>
    public byte[] GenerateDocuSealFieldGuidePdf(InvoiceTemplate? template = null)
        => BuildReceiptPdf(BuildShellModel(template), ReceiptLayoutMode.Guide);

    public byte[] GeneratePdf(Vehicle vehicle, int documentNumber)
        => GeneratePdf(BuildModel(vehicle, documentNumber));

    private enum ReceiptLayoutMode { Filled, Blank, Guide }

    private static bool IsSaulMotorsTemplate(InvoiceTemplate? template)
    {
        if (template is null) return false;
        return template.Name.Contains("Saul", StringComparison.OrdinalIgnoreCase)
               || template.BuyerAuthorizedName.Contains("SAUL", StringComparison.OrdinalIgnoreCase)
               || (template.LogoRelativePath?.Contains("saul-motors", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private PurchaseInvoiceModel BuildShellModel(InvoiceTemplate? template = null)
    {
        if (template is null)
        {
            return new PurchaseInvoiceModel
            {
                DocumentNumber = 0,
                PurchaseDate = DateTime.Today,
                BuyerName = _company.Name,
                BuyerAuthorizedName = "RODRIGUEZ SALVAGE YARD",
                BuyerAddressLine1 = _company.AddressLine1,
                BuyerCityStateZip = _company.CityStateZip,
                BuyerEmail = _company.Email,
                TemplateName = "Rodriguez Salvage Yard",
                LogoRelativePath = "/invoice/rsy-invoice-logo.png",
                UseRsyFooter = true,
                VehicleDescription = "Vehicle"
            };
        }

        return new PurchaseInvoiceModel
        {
            DocumentNumber = 0,
            PurchaseDate = DateTime.Today,
            BuyerName = template.BuyerCompanyName,
            BuyerAuthorizedName = template.BuyerAuthorizedName,
            BuyerAddressLine1 = template.AddressLine1,
            BuyerCityStateZip = template.CityStateZip,
            BuyerEmail = template.Email,
            TemplateName = template.Name,
            LogoRelativePath = template.LogoRelativePath ?? "/invoice/rsy-invoice-logo.png",
            UseRsyFooter = !IsSaulMotorsTemplate(template),
            VehicleDescription = "Vehicle"
        };
    }

    private string ResolveWebRootFile(string? relativeWebPath, string fallbackRelative)
    {
        var rel = string.IsNullOrWhiteSpace(relativeWebPath) ? fallbackRelative : relativeWebPath.Trim();
        rel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolute = Path.Combine(_env.WebRootPath, rel);
        if (File.Exists(absolute))
            return absolute;

        var fb = fallbackRelative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_env.WebRootPath, fb);
    }

    private byte[] BuildReceiptPdf(PurchaseInvoiceModel model, ReceiptLayoutMode mode)
    {
        var logoPath = ResolveWebRootFile(model.LogoRelativePath, "invoice/rsy-invoice-logo.png");
        var footerPath = Path.Combine(_env.WebRootPath, "invoice", "rsy-invoice-footer.png");
        var guideColor = "#0F766E";

        string V(string fieldName, string? filled)
        {
            return mode switch
            {
                ReceiptLayoutMode.Guide => $"[{fieldName}]",
                ReceiptLayoutMode.Blank => " ",
                _ => Blank(filled)
            };
        }

        var money = mode == ReceiptLayoutMode.Filled
            ? model.PurchasePrice.ToString("C", Us)
            : V(DocuSealReceiptFields.Price, null);

        var ymmFilled = mode == ReceiptLayoutMode.Filled
            ? $"{Blank(model.VehicleYear?.ToString(Us))} / {Blank(model.VehicleMake)} / {Blank(model.VehicleModel)}"
            : V(DocuSealReceiptFields.VehicleYmm, null);

        var descriptionFilled = mode == ReceiptLayoutMode.Filled
            ? $"Purchase of {model.VehicleDescription}"
            : V(DocuSealReceiptFields.Description, null);

        var docNo = mode switch
        {
            ReceiptLayoutMode.Guide => $"NO. [{DocuSealReceiptFields.DocumentNumber}]",
            ReceiptLayoutMode.Blank => "NO. ______",
            _ => $"NO. {model.DocumentNumber:000000}"
        };

        var purchaseDate = mode == ReceiptLayoutMode.Filled
            ? model.PurchaseDate.ToString("MMMM d, yyyy", Us)
            : V(DocuSealReceiptFields.PurchaseDate, null);

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
                    if (mode == ReceiptLayoutMode.Guide)
                    {
                        col.Item().Background("#ECFDF5").Padding(6).Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8).FontColor(guideColor));
                            text.Span("GUÍA DocuSeal — ").Bold();
                            text.Span("arrastra campos Text/Signature/Date sobre cada [Nombre] y usa ese nombre exacto. Rol: Seller.");
                        });
                    }

                    col.Item().PaddingTop(mode == ReceiptLayoutMode.Guide ? 6 : 0).Row(row =>
                    {
                        row.RelativeItem().Element(c =>
                        {
                            if (File.Exists(logoPath))
                                c.Height(64).Width(168).Image(logoPath).FitArea();
                            else
                                c.Text(model.BuyerAuthorizedName).Bold().FontSize(14);
                        });

                        row.ConstantItem(170).AlignRight().AlignMiddle().Element(c =>
                        {
                            if (mode == ReceiptLayoutMode.Guide)
                                c.Text(docNo).Bold().FontSize(11).FontColor(guideColor);
                            else
                                c.Text(docNo).Bold().FontSize(13);
                        });
                    });

                    col.Item().ExtendVertical().AlignMiddle().Column(body =>
                    {
                        body.Item().Text("VEHICLE PURCHASE ACKNOWLEDGEMENT")
                            .Bold().FontSize(15).FontColor(Colors.Black);

                        body.Item().PaddingTop(2).Text("Bill of sale / ownership transfer receipt")
                            .FontSize(8).FontColor(Muted);

                        body.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Purchase date: ").Bold();
                            if (mode == ReceiptLayoutMode.Guide)
                                text.Span(purchaseDate).FontColor(guideColor);
                            else
                                text.Span(purchaseDate);
                        });
                        if (mode == ReceiptLayoutMode.Blank)
                            body.Item().PaddingTop(2).LineHorizontal(1).LineColor("#CBD5E1");

                        body.Item().PaddingTop(10).Element(c => SectionTitle(c, "Vehicle"));
                        body.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Y/M/M: ").FontColor(Muted);
                            if (mode == ReceiptLayoutMode.Guide)
                                text.Span(ymmFilled).FontColor(guideColor);
                            else
                                text.Span(ymmFilled);
                            text.Span("    VIN: ").FontColor(Muted);
                            if (mode == ReceiptLayoutMode.Guide)
                                text.Span(V(DocuSealReceiptFields.Vin, model.VehicleVin)).SemiBold().FontColor(guideColor);
                            else
                                text.Span(V(DocuSealReceiptFields.Vin, model.VehicleVin)).SemiBold();
                        });
                        if (mode == ReceiptLayoutMode.Blank)
                            body.Item().PaddingTop(2).LineHorizontal(1).LineColor("#CBD5E1");

                        body.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().PaddingRight(8).Column(seller =>
                            {
                                seller.Item().Element(c => SectionTitle(c, "Seller"));
                                if (mode == ReceiptLayoutMode.Filled)
                                {
                                    seller.Item().PaddingTop(4).Text(Blank(model.SellerName)).SemiBold();
                                    foreach (var line in SplitLines(model.SellerAddress))
                                        seller.Item().Text(line).FontSize(8.5f);
                                    if (!string.IsNullOrWhiteSpace(model.SellerPhone))
                                        seller.Item().Text(model.SellerPhone!).FontSize(8.5f);
                                    if (!string.IsNullOrWhiteSpace(model.SellerEmail))
                                        seller.Item().Text(model.SellerEmail!).FontSize(8.5f);
                                }
                                else if (mode == ReceiptLayoutMode.Guide)
                                {
                                    seller.Item().PaddingTop(4).Text($"[{DocuSealReceiptFields.SellerName}]").SemiBold().FontColor(guideColor);
                                    seller.Item().Text($"[{DocuSealReceiptFields.SellerAddress}]").FontSize(8.5f).FontColor(guideColor);
                                    seller.Item().Text($"[{DocuSealReceiptFields.SellerPhone}]").FontSize(8.5f).FontColor(guideColor);
                                    seller.Item().Text($"[{DocuSealReceiptFields.SellerEmail}]").FontSize(8.5f).FontColor(guideColor);
                                }
                                else
                                {
                                    seller.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CBD5E1");
                                    seller.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");
                                    seller.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");
                                    seller.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");
                                }
                            });

                            row.RelativeItem().PaddingLeft(8).Column(buyer =>
                            {
                                buyer.Item().Element(c => SectionTitle(c, "Buyer"));
                                if (mode == ReceiptLayoutMode.Filled)
                                {
                                    buyer.Item().PaddingTop(4).Text(model.BuyerName).SemiBold();
                                    buyer.Item().Text(model.BuyerAddressLine1).FontSize(8.5f);
                                    buyer.Item().Text(model.BuyerCityStateZip).FontSize(8.5f);
                                    if (!string.IsNullOrWhiteSpace(model.BuyerEmail))
                                        buyer.Item().Text(model.BuyerEmail!).FontSize(8.5f);
                                }
                                else if (mode == ReceiptLayoutMode.Guide)
                                {
                                    buyer.Item().PaddingTop(4).Text($"[{DocuSealReceiptFields.BuyerName}]").SemiBold().FontColor(guideColor);
                                    buyer.Item().Text($"[{DocuSealReceiptFields.BuyerAddress}]").FontSize(8.5f).FontColor(guideColor);
                                    buyer.Item().Text($"[{DocuSealReceiptFields.BuyerEmail}]").FontSize(8.5f).FontColor(guideColor);
                                }
                                else
                                {
                                    buyer.Item().PaddingTop(10).LineHorizontal(1).LineColor("#CBD5E1");
                                    buyer.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");
                                    buyer.Item().PaddingTop(12).LineHorizontal(1).LineColor("#CBD5E1");
                                }
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

                            var amountGuide = mode == ReceiptLayoutMode.Guide
                                ? $"[{DocuSealReceiptFields.Amount}]"
                                : money;

                            table.Cell().Element(TableBodyCell).Element(c =>
                            {
                                if (mode == ReceiptLayoutMode.Guide)
                                    c.Text(descriptionFilled).FontColor(guideColor);
                                else
                                    c.Text(descriptionFilled);
                            });
                            table.Cell().Element(TableBodyCell).AlignRight().Element(c =>
                            {
                                if (mode == ReceiptLayoutMode.Guide)
                                    c.Text($"[{DocuSealReceiptFields.Price}]").FontColor(guideColor);
                                else
                                    c.Text(money);
                            });
                            table.Cell().Element(TableBodyCell).AlignRight().Element(c =>
                            {
                                if (mode == ReceiptLayoutMode.Guide)
                                    c.Text(amountGuide).FontColor(guideColor);
                                else
                                    c.Text(money);
                            });
                        });

                        body.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);
                        body.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(90).AlignRight().Text("Sale price").Bold();
                            row.ConstantItem(100).AlignRight().Element(c =>
                            {
                                if (mode == ReceiptLayoutMode.Guide)
                                    c.Text($"[{DocuSealReceiptFields.Price}]").Bold().FontColor(guideColor);
                                else
                                    c.Text(money).Bold();
                            });
                        });
                        body.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);

                        body.Item().PaddingTop(6).Text(text =>
                        {
                            text.Span("Payment method: ").Bold();
                            var pay = V(DocuSealReceiptFields.PaymentMethod, model.PaymentMethod);
                            if (mode == ReceiptLayoutMode.Guide)
                                text.Span(pay).FontColor(guideColor);
                            else
                                text.Span(pay);
                        });
                        if (mode == ReceiptLayoutMode.Blank)
                            body.Item().PaddingTop(2).Width(180).LineHorizontal(1).LineColor("#CBD5E1");

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
                            var buyerAuthName = string.IsNullOrWhiteSpace(model.BuyerAuthorizedName)
                                ? "RODRIGUEZ SALVAGE YARD"
                                : model.BuyerAuthorizedName;

                            row.RelativeItem().PaddingRight(14).Column(sig =>
                            {
                                sig.Item().Text("Seller signature").Bold().FontSize(9);
                                if (mode == ReceiptLayoutMode.Guide)
                                {
                                    sig.Item().PaddingTop(10).Text($"[{DocuSealReceiptFields.SellerSignature}]  ← Signature")
                                        .FontSize(9).FontColor(guideColor);
                                    sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                    sig.Item().PaddingTop(2).Text("Printed name").FontSize(7.5f).FontColor(Muted);
                                    sig.Item().PaddingTop(10).Text($"[{DocuSealReceiptFields.SellerDate}]  ← Date")
                                        .FontSize(9).FontColor(guideColor);
                                    sig.Item().PaddingTop(2).Text("Date").FontSize(7.5f).FontColor(Muted);
                                }
                                else
                                {
                                    sig.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Black);
                                    sig.Item().PaddingTop(2).Text("Signature").FontSize(7.5f).FontColor(Muted);
                                    sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                    sig.Item().PaddingTop(2).Text("Printed name").FontSize(7.5f).FontColor(Muted);
                                    sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                    sig.Item().PaddingTop(2).Text("Date").FontSize(7.5f).FontColor(Muted);
                                }
                            });

                            row.RelativeItem().PaddingLeft(14).Column(sig =>
                            {
                                sig.Item().Text("Buyer (authorized)").Bold().FontSize(9);
                                sig.Item().PaddingTop(10).Text(buyerAuthName).SemiBold().FontSize(10);
                                sig.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Signature").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(8).Text(buyerAuthName).SemiBold().FontSize(10);
                                sig.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Printed name").FontSize(7.5f).FontColor(Muted);
                                sig.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Black);
                                sig.Item().PaddingTop(2).Text("Date").FontSize(7.5f).FontColor(Muted);
                                if (mode == ReceiptLayoutMode.Guide)
                                {
                                    sig.Item().PaddingTop(6).Text("(preimpreso — sin campo DocuSeal)")
                                        .FontSize(7.5f).FontColor(guideColor);
                                }
                            });
                        });
                    });
                });

                var pageWidth = PageSizes.Letter.Width;
                var footerHeight = pageWidth * (456f / 1836f);
                page.MarginBottom(footerHeight);
                page.Background().AlignBottom().Height(footerHeight).Element(footer =>
                {
                    if (model.UseRsyFooter && File.Exists(footerPath))
                        footer.Image(footerPath).FitWidth();
                    else
                        footer.Background("#1F424C");
                });
            });

            if (mode == ReceiptLayoutMode.Guide)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(48);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));
                    page.Content().Column(col =>
                    {
                        col.Item().Text("Lista de campos DocuSeal").Bold().FontSize(16);
                        col.Item().PaddingTop(4).Text(
                            "Nombra cada campo exactamente así (mayúsculas/minúsculas). Rol del firmante: Seller.")
                            .FontColor(Muted);
                        col.Item().PaddingTop(12).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(3f);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Element(TableHeaderCell).Text("Nombre del campo");
                                h.Cell().Element(TableHeaderCell).Text("Tipo");
                                h.Cell().Element(TableHeaderCell).Text("Dónde colocarlo");
                            });

                            void Row(string name, string type, string where)
                            {
                                t.Cell().Element(TableBodyCell).Text(name).FontFamily("Courier New").FontSize(9);
                                t.Cell().Element(TableBodyCell).Text(type);
                                t.Cell().Element(TableBodyCell).Text(where).FontSize(8.5f);
                            }

                            Row(DocuSealReceiptFields.DocumentNumber, "Text", "Arriba a la derecha: solo el número (el «NO.» es fijo)");
                            Row(DocuSealReceiptFields.PurchaseDate, "Text", "Junto a Purchase date");
                            Row(DocuSealReceiptFields.VehicleYmm, "Text", "Y/M/M");
                            Row(DocuSealReceiptFields.Vin, "Text", "VIN");
                            Row(DocuSealReceiptFields.SellerName, "Text", "Seller — nombre");
                            Row(DocuSealReceiptFields.SellerAddress, "Text", "Seller — dirección");
                            Row(DocuSealReceiptFields.SellerPhone, "Text", "Seller — teléfono");
                            Row(DocuSealReceiptFields.SellerEmail, "Text", "Seller — correo");
                            Row(DocuSealReceiptFields.BuyerName, "Text", "Buyer — empresa");
                            Row(DocuSealReceiptFields.BuyerAddress, "Text", "Buyer — dirección (una línea)");
                            Row(DocuSealReceiptFields.BuyerEmail, "Text", "Buyer — correo");
                            Row(DocuSealReceiptFields.Description, "Text", "Tabla Transaction — Description");
                            Row(DocuSealReceiptFields.Price, "Text", "Columna Price y/o Sale price");
                            Row(DocuSealReceiptFields.Amount, "Text", "Columna Amount (mismo valor que Price)");
                            Row(DocuSealReceiptFields.PaymentMethod, "Text", "Payment method");
                            Row(DocuSealReceiptFields.SellerSignature, "Signature", "Seller signature — línea Signature");
                            Row(DocuSealReceiptFields.SellerDate, "Date", "Seller signature — línea Date");
                        });
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

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

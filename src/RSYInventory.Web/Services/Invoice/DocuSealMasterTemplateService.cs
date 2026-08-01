using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;

namespace RSYInventory.Web.Services.Invoice;

/// <summary>
/// Builds a DOCX with DocuSeal {{tags}}. Free DocuSeal UI detects DOCX tags reliably
/// (unlike QuestPDF-generated PDFs, where tags often stay as plain text).
/// </summary>
public sealed class DocuSealMasterTemplateService
{
    private readonly DocuSealOptions _options;

    public DocuSealMasterTemplateService(IOptions<DocuSealOptions> options)
    {
        _options = options.Value;
    }

    public byte[] GenerateDocx()
    {
        var role = string.IsNullOrWhiteSpace(_options.SellerRole)
            ? DocuSealReceiptFields.DefaultSellerRole
            : _options.SellerRole.Trim();

        string T(string name, string type = "text", bool readOnly = true)
            => DocuSealReceiptFields.TextTag(name, type, role, readOnly);

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.Append(Title("VEHICLE PURCHASE ACKNOWLEDGEMENT"));
            body.Append(Muted("Bill of sale / ownership transfer receipt"));
            body.Append(Line($"NO. {T(DocuSealReceiptFields.DocumentNumber)}"));
            body.Append(Line($"Purchase date: {T(DocuSealReceiptFields.PurchaseDate)}"));
            body.Append(Spacer());

            body.Append(Heading("Vehicle"));
            body.Append(Line($"Y/M/M: {T(DocuSealReceiptFields.VehicleYmm)}"));
            body.Append(Line($"VIN: {T(DocuSealReceiptFields.Vin)}"));
            body.Append(Spacer());

            body.Append(Heading("Seller"));
            body.Append(Line(T(DocuSealReceiptFields.SellerName)));
            body.Append(Line(T(DocuSealReceiptFields.SellerAddress)));
            body.Append(Line(T(DocuSealReceiptFields.SellerPhone)));
            body.Append(Line(T(DocuSealReceiptFields.SellerEmail)));
            body.Append(Spacer());

            body.Append(Heading("Buyer"));
            body.Append(Line(T(DocuSealReceiptFields.BuyerName)));
            body.Append(Line(T(DocuSealReceiptFields.BuyerAddress)));
            body.Append(Line(T(DocuSealReceiptFields.BuyerEmail)));
            body.Append(Spacer());

            body.Append(Heading("Transaction"));
            body.Append(Line($"Description: {T(DocuSealReceiptFields.Description)}"));
            body.Append(Line($"Sale price: {T(DocuSealReceiptFields.Price)}"));
            body.Append(Line($"Payment method: {T(DocuSealReceiptFields.PaymentMethod)}"));
            body.Append(Spacer());

            body.Append(Heading("Condition"));
            body.Append(Line(
                "AS-IS. Seller certifies legal ownership and transfers the vehicle to Buyer with no warranties. " +
                "Buyer acknowledges receipt of ownership and possession on the purchase date above."));
            body.Append(Spacer());

            body.Append(Heading("Seller signature"));
            body.Append(Line(T(DocuSealReceiptFields.SellerSignature, "signature", readOnly: false)));
            body.Append(Muted("Signature"));
            body.Append(Line(T(DocuSealReceiptFields.SellerDate, "date", readOnly: false)));
            body.Append(Muted("Date"));
            body.Append(Spacer());

            body.Append(Heading("Buyer (authorized)"));
            body.Append(Line("RODRIGUEZ SALVAGE YARD"));
            body.Append(Muted("Pre-printed — no DocuSeal fields"));

            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph Title(string text) => Para(text, bold: true, size: 32);
    private static Paragraph Heading(string text) => Para(text, bold: true, size: 22);
    private static Paragraph Muted(string text) => Para(text, bold: false, size: 18, color: "666666");
    private static Paragraph Line(string text) => Para(text, bold: false, size: 20);
    private static Paragraph Spacer() => new(new ParagraphProperties(new SpacingBetweenLines { After = "120" }));

    private static Paragraph Para(string text, bool bold, int size, string? color = null)
    {
        var runProps = new RunProperties(new FontSize { Val = size.ToString() });
        if (bold) runProps.Append(new Bold());
        if (!string.IsNullOrEmpty(color))
            runProps.Append(new Color { Val = color });

        return new Paragraph(
            new Run(runProps, new Text(text)));
    }
}

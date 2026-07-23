using RSYInventory.Data.Entities;
using RSYInventory.Data.Services;

namespace RSYInventory.Web.Services.Invoice;

public sealed class VehicleInvoiceService
{
    private readonly VehicleService _vehicles;
    private readonly PurchaseInvoicePdfService _pdf;
    private readonly InvoiceEmailService _email;

    public VehicleInvoiceService(
        VehicleService vehicles,
        PurchaseInvoicePdfService pdf,
        InvoiceEmailService email)
    {
        _vehicles = vehicles;
        _pdf = pdf;
        _email = email;
    }

    public bool CanEmail => _email.IsConfigured;

    public async Task<VehicleInvoiceResult> GenerateAsync(int vehicleId, CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var invoiceNumber = await _vehicles.EnsureInvoiceNumberAsync(vehicleId, ct);

        vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var model = _pdf.BuildModel(vehicle, invoiceNumber);
        var pdfBytes = _pdf.GeneratePdf(model);
        var fileName = $"Invoice-{invoiceNumber}.pdf";

        return new VehicleInvoiceResult(vehicle, model, pdfBytes, fileName);
    }

    public Task SendEmailAsync(VehicleInvoiceResult invoice, CancellationToken ct = default)
        => _email.SendInvoiceAsync(
            invoice.Vehicle.SellerEmail ?? string.Empty,
            invoice.Vehicle.SellerName,
            invoice.Model.InvoiceNumber,
            invoice.PdfBytes,
            invoice.FileName,
            ct);
}

public sealed record VehicleInvoiceResult(
    Vehicle Vehicle,
    PurchaseInvoiceModel Model,
    byte[] PdfBytes,
    string FileName);

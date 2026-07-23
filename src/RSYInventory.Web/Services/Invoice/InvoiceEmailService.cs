using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RSYInventory.Web.Services.Invoice;

public sealed class InvoiceEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<InvoiceEmailService> _logger;

    public InvoiceEmailService(IOptions<SmtpOptions> options, ILogger<InvoiceEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.Host)
        && !string.IsNullOrWhiteSpace(_options.FromEmail);

    public async Task SendInvoiceAsync(
        string toEmail,
        string? toName,
        int invoiceNumber,
        byte[] pdfBytes,
        string fileName,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "El correo no está configurado. Completa la sección Smtp en appsettings o User Secrets.");

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("No hay correo del vendedor para enviar el recibo de compra.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromEmail));
        message.To.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(toName) ? toEmail.Trim() : toName.Trim(),
            toEmail.Trim()));
        message.Subject = $"Signed Vehicle Purchase Acknowledgement #{invoiceNumber} — Rodriguez Salvage Yard";

        var body = new TextPart("plain")
        {
            Text = $"""
                Hello{(string.IsNullOrWhiteSpace(toName) ? "" : $" {toName.Trim()}")},

                Attached is the signed Vehicle Purchase Acknowledgement #{invoiceNumber} for the vehicle you sold to Rodriguez Salvage Yard, CORP.

                Please keep a copy for your records.

                Thank you,
                Rodriguez Salvage Yard
                """
        };

        var attachment = new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(pdfBytes)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = fileName
        };

        message.Body = new Multipart("mixed") { body, attachment };

        using var client = new SmtpClient();
        try
        {
            var secure = _options.UseSsl
                ? (_options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, secure, ct);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email to {Email}", toEmail);
            throw new InvalidOperationException($"No se pudo enviar el correo: {ex.Message}", ex);
        }
    }

    public async Task SendSigningLinkAsync(
        string toEmail,
        string? toName,
        int invoiceNumber,
        string signingUrl,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "El correo no está configurado. Completa la sección Smtp en appsettings o User Secrets.");

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("No hay correo del vendedor para enviar el enlace de firma.");

        if (string.IsNullOrWhiteSpace(signingUrl))
            throw new InvalidOperationException("No hay enlace de firma para enviar.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromEmail));
        message.To.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(toName) ? toEmail.Trim() : toName.Trim(),
            toEmail.Trim()));
        message.Subject = $"Firma el acuse de compra #{invoiceNumber} — Rodriguez Salvage Yard";

        message.Body = new TextPart("plain")
        {
            Text = $"""
                Hola{(string.IsNullOrWhiteSpace(toName) ? "" : $" {toName.Trim()}")},

                Rodriguez Salvage Yard necesita tu firma en el acuse de compra #{invoiceNumber}.

                Abre este enlace para firmar electrónicamente:
                {signingUrl.Trim()}

                Si el enlace no abre, cópialo y pégalo en tu navegador.

                Gracias,
                Rodriguez Salvage Yard
                """
        };

        using var client = new SmtpClient();
        try
        {
            var secure = _options.UseSsl
                ? (_options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, secure, ct);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signing-link email to {Email}", toEmail);
            throw new InvalidOperationException($"No se pudo enviar el correo: {ex.Message}", ex);
        }
    }
}

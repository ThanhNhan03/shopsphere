# Customer email delivery

The Notification worker handles order-confirmation and cancellation events. It uses `EmailDelivery.EventId` as a durable deduplication key and records delivery status and attempts in `notification_db`. The ledger intentionally stores no recipient address, subject or message body. MailKit sends plain-text messages through SMTP when enabled.

Local Compose defaults to `EMAIL_MODE=Console`, which records a simulated delivery and sends nothing. Configure these values in the ignored root `.env` to send real mail:

| Setting | Purpose |
| --- | --- |
| `EMAIL_MODE` | Set to `Smtp` to deliver; `Console` is the local default. |
| `SMTP_HOST`, `SMTP_PORT` | Provider hostname and port (default port 587). |
| `SMTP_SECURITY` | `StartTls` or `SslOnConnect`; plaintext SMTP is rejected. |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | Optional only when the provider permits unauthenticated SMTP; set both or neither. |
| `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` | Sender mailbox and display name. |

The worker retries failed sends through MassTransit and records a sanitized exception type, not provider response text or credentials. The stable event ID is used as the message ID and a custom header, but SMTP cannot guarantee exactly-once delivery: if a process crashes after the provider accepts a message and before PostgreSQL records success, retrying may send a duplicate. Configure provider-side suppression if available and monitor the RabbitMQ notification error queue and `notification_db` delivery records.

Production startup requires SMTP mode, a host, sender and TLS mode. Supply provider credentials using your deployment secret store. SMTP delivery has not been tested against an external provider in this checkout.

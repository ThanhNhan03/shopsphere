# Deployment security

Production startup runs configuration guards in each service and exits when required settings are missing or use known demo values. These checks reduce accidental exposure; they do not provision secrets, certificates, network policy, backups or monitoring. A deployment still needs an operator to supply and verify these settings.

| Area | Production requirement |
| --- | --- |
| Storefront | `Frontend__Url` must use HTTPS. Terminate TLS at a trusted reverse proxy and preserve secure forwarded-header handling. |
| PostgreSQL | Each service's `ConnectionStrings__Database` must use a secret password and `SSL Mode=VerifyFull`; use trusted server certificates and private network access. |
| RabbitMQ | Private endpoint, non-demo credential and `RabbitMq__UseSsl=true`. Restrict management access and monitor retry/error queues. |
| Redis | Authenticated connection string with TLS enabled. Keep Redis private and restrict credentials to the Basket service. |
| Gateway cookies | `Auth__KeyPath` must be an absolute, durable shared path. Persist and protect ASP.NET Data Protection keys across restarts and replicas. |
| Product images | `S3__Endpoint` must use HTTPS; set a non-demo `S3__AccessKey` and `S3__SecretKey` from a secret store, with least-privilege bucket access. |
| Customer email | `Email__Mode=Smtp`, SMTP host/from address and `StartTls` or `SslOnConnect`; inject SMTP credentials from a secret store. |
| Payments | `Payment__Mode=Stripe`, an `sk_live_` secret key and its matching `Stripe__WebhookSecret`. Use a public HTTPS webhook route and alert on delivery failures. |
| Logs | Production writes to console instead of the unauthenticated local Seq sink. Route logs to an access-controlled platform and avoid recording secrets or message bodies. |
| Network | Expose only the reverse proxy/storefront and required public webhook path. PostgreSQL, Redis, RabbitMQ, MinIO/S3 and internal APIs should remain private. |

The checked-in Compose file and `.env.example` are for local demonstration. Their HTTP endpoints, credentials and loopback port publishing are not a production deployment template. Use environment-specific secret injection and network configuration. Keep Gateway's rate limiter in mind: its quotas are process-local, so a multi-replica Gateway needs a shared/distributed limiter before relying on the configured limit as a global abuse control.

Repository validation covers the configuration guards and Compose syntax only. No production target, certificate chain, secret store, deployment network or multi-instance setup was available for verification.

# ShopSphere

ShopSphere is a mock E-commerce platform for technology products built with **.NET 10 Microservices Architecture**, **Clean Architecture**, **Next.js**, and **Event-Driven Architecture**.

The project focuses on demonstrating practical distributed-system concepts through a complete E-commerce checkout flow including product browsing, shopping basket, inventory reservation, Stripe payment, asynchronous messaging, and order processing.

---

## Project Scope

ShopSphere is designed as a short-term Microservices project developed by a **3-person team with AI-assisted development**.

The primary goal is to deliver a working end-to-end E-commerce flow while demonstrating:

- Microservices Architecture
- Clean Architecture
- Database per Service
- REST communication
- Event-Driven Architecture
- RabbitMQ messaging
- Saga-style compensation
- Transactional Outbox
- Idempotent message processing
- Redis caching
- Stripe payment integration
- API Gateway
- Containerized services

---

# Tech Stack

## Backend

- **.NET 10**
- **ASP.NET Core Web API**
- **Entity Framework Core 10**
- **Clean Architecture**
- **REST API**
- **MassTransit**
- **RabbitMQ**
- **Serilog**
- **xUnit**

## Frontend

- **Next.js**
- **React**
- **TypeScript**
- **Tailwind CSS**
- **TanStack Query**
- **Zod**
- **Fetch API**

## Database & Cache

- **PostgreSQL**
- **Redis**
- **Npgsql**
- **EF Core Migrations**

## Messaging

- **RabbitMQ**
- **AMQP**
- **MassTransit**

## Payment

- **Stripe**
- **Stripe Checkout**
- **Stripe Webhooks**
- **Stripe Test Mode**

## API Gateway

- **YARP Reverse Proxy**

## Logging

- **Serilog**
- **Seq**

## Infrastructure

- **Docker**
- **Docker Compose**

---

# Architecture

```text
                              Next.js
                                 │
                              HTTP/REST
                                 │
                                 ▼
                            YARP Gateway
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
          ▼                      ▼                      ▼
       Catalog                 Basket                Ordering
          │                      │                      │
     PostgreSQL                Redis               PostgreSQL
                                                        │
                                                     RabbitMQ
                                               ┌────────┴────────┐
                                               ▼                 ▼
                                           Inventory          Payment
                                               │                 │
                                          PostgreSQL        PostgreSQL
                                                                 │
                                                                 ▼
                                                              Stripe
                                                                 │
                                                              Webhook
                                                                 │
                                                                 ▼
                                                              Payment
                                                                 │
                                                              RabbitMQ
                                                        ┌────────┴────────┐
                                                        ▼                 ▼
                                                    Ordering        Notification
```

---

# Microservices

## Catalog Service

Responsible for product information.

### Responsibilities

- Product listing
- Product details
- Categories
- Brands
- Product pricing
- Seed demo products

### Storage

```text
PostgreSQL
```

### Main APIs

```http
GET /api/products

GET /api/products/{id}

GET /api/categories
```

Example product:

```text
Product
├── Id
├── Name
├── Description
├── Price
├── ImageUrl
├── Brand
└── Category
```

Admin product management is outside the current scope.

---

## Basket Service

Responsible for customer shopping baskets.

### Responsibilities

- Get basket
- Add item
- Update quantity
- Remove item
- Clear basket after checkout
- Calculate basket total

### Storage

```text
Redis
```

### Main APIs

```http
GET /api/basket/{customerId}

POST /api/basket/{customerId}/items

PUT /api/basket/{customerId}/items/{productId}

DELETE /api/basket/{customerId}/items/{productId}

DELETE /api/basket/{customerId}
```

Example Redis key:

```text
basket:{customerId}
```

---

## Ordering Service

Ordering is the main owner of the Order lifecycle.

### Responsibilities

- Checkout
- Create Order
- Store Order Items
- Maintain order state
- Consume Inventory events
- Consume Payment events
- Handle order cancellation
- Publish integration events

### Storage

```text
PostgreSQL
```

### Order Status

```text
Pending

InventoryReserved

AwaitingPayment

Confirmed

Cancelled
```

### Main APIs

```http
POST /api/orders

GET /api/orders/{id}
```

Example Order:

```text
Order
├── Id
├── CustomerId
├── CustomerName
├── Email
├── TotalAmount
├── Status
├── CreatedAt
└── OrderItems
```

---

## Inventory Service

Responsible for stock management.

### Responsibilities

- Check stock
- Reserve inventory
- Release inventory
- Prevent overselling
- Consume order-related events

### Storage

```text
PostgreSQL
```

Example:

```text
Inventory
├── ProductId
├── AvailableQuantity
└── ReservedQuantity
```

Inventory operations should be idempotent.

---

## Payment Service

Responsible for payment processing and Stripe integration.

### Responsibilities

- Create Stripe Checkout Session
- Track payment transactions
- Receive Stripe Webhooks
- Validate Stripe Webhook signatures
- Prevent duplicate webhook processing
- Publish payment events

### Storage

```text
PostgreSQL
```

### Main APIs

```http
POST /api/payments/checkout-session

POST /api/payments/webhooks/stripe
```

Example Payment:

```text
Payment
├── Id
├── OrderId
├── StripeSessionId
├── StripePaymentIntentId
├── Amount
├── Currency
├── Status
├── CreatedAt
└── CompletedAt
```

Payment status:

```text
Pending

Processing

Completed

Failed

Cancelled
```

---

## Notification Worker

A lightweight background worker consuming order events.

### Responsibilities

- Consume OrderConfirmed event
- Consume OrderCancelled event
- Simulate email notification
- Log notification output

No database is required.

---

# Clean Architecture

Each major service follows:

```text
Service
│
├── Service.Api
│
├── Service.Application
│
├── Service.Domain
│
└── Service.Infrastructure
```

Smaller services may use fewer projects when a separate layer provides no practical benefit.

---

## Domain Layer

Contains core business rules.

```text
Domain/
├── Entities/
├── Aggregates/
├── ValueObjects/
├── Enums/
├── DomainEvents/
└── Exceptions/
```

The Domain layer must not depend on Infrastructure.

---

## Application Layer

Contains application use cases.

```text
Application/
├── Commands/
├── Queries/
├── DTOs/
├── Interfaces/
├── Validators/
└── EventHandlers/
```

Responsibilities:

- Business use cases
- Application orchestration
- Commands
- Queries
- Validation
- Messaging interfaces

---

## Infrastructure Layer

Contains technical implementations.

```text
Infrastructure/
├── Persistence/
├── Messaging/
├── Outbox/
├── Stripe/
├── Redis/
└── Repositories/
```

Responsibilities:

- EF Core
- PostgreSQL
- Redis
- RabbitMQ
- MassTransit
- Stripe
- Outbox implementation

---

## API Layer

Exposes HTTP endpoints.

```text
Api/
├── Controllers/
├── Webhooks/
├── Middleware/
├── Extensions/
└── Program.cs
```

Controllers should not contain business logic.

---

# Project Structure

```text
ShopSphere/
│
├── ShopSphere.sln
├── README.md
├── docker-compose.yml
├── .env.example
├── Directory.Build.props
├── Directory.Packages.props
│
├── src/
│
│   ├── Gateway/
│   │   └── ShopSphere.Gateway/
│   │
│   ├── Services/
│   │
│   │   ├── Catalog/
│   │   │   ├── ShopSphere.Catalog.Api/
│   │   │   ├── ShopSphere.Catalog.Application/
│   │   │   ├── ShopSphere.Catalog.Domain/
│   │   │   └── ShopSphere.Catalog.Infrastructure/
│   │   │
│   │   ├── Basket/
│   │   │   ├── ShopSphere.Basket.Api/
│   │   │   ├── ShopSphere.Basket.Application/
│   │   │   └── ShopSphere.Basket.Infrastructure/
│   │   │
│   │   ├── Ordering/
│   │   │   ├── ShopSphere.Ordering.Api/
│   │   │   ├── ShopSphere.Ordering.Application/
│   │   │   ├── ShopSphere.Ordering.Domain/
│   │   │   └── ShopSphere.Ordering.Infrastructure/
│   │   │
│   │   ├── Inventory/
│   │   │   ├── ShopSphere.Inventory.Api/
│   │   │   ├── ShopSphere.Inventory.Application/
│   │   │   ├── ShopSphere.Inventory.Domain/
│   │   │   └── ShopSphere.Inventory.Infrastructure/
│   │   │
│   │   └── Payment/
│   │       ├── ShopSphere.Payment.Api/
│   │       ├── ShopSphere.Payment.Application/
│   │       ├── ShopSphere.Payment.Domain/
│   │       └── ShopSphere.Payment.Infrastructure/
│   │
│   ├── Workers/
│   │   └── ShopSphere.Notification.Worker/
│   │
│   └── BuildingBlocks/
│       ├── ShopSphere.EventBus/
│       ├── ShopSphere.Messaging/
│       └── ShopSphere.SharedKernel/
│
├── contracts/
│   └── ShopSphere.Contracts/
│
├── frontend/
│   └── shopsphere-web/
│       ├── src/
│       │   ├── app/
│       │   ├── components/
│       │   ├── features/
│       │   │   ├── catalog/
│       │   │   ├── basket/
│       │   │   ├── checkout/
│       │   │   └── orders/
│       │   ├── hooks/
│       │   ├── lib/
│       │   ├── services/
│       │   ├── schemas/
│       │   └── types/
│       ├── public/
│       ├── package.json
│       └── next.config.ts
│
├── tests/
│   └── ShopSphere.Tests/
│
└── docs/
    ├── architecture.md
    ├── order-flow.md
    └── events.md
```

---

# Service Communication

## Synchronous Communication

HTTP / REST is used when an immediate response is required.

```text
Next.js
   │
   ▼
YARP Gateway
   │
   ▼
Microservice
```

Long synchronous service chains should be avoided.

---

## Asynchronous Communication

RabbitMQ is used for distributed business workflows.

```text
Ordering
    │
    │ OrderCreated
    ▼
RabbitMQ
    │
    ▼
Inventory
    │
    │ InventoryReserved
    ▼
RabbitMQ
    │
    ▼
Payment
```

MassTransit is used to simplify:

- Producers
- Consumers
- Message routing
- Retry
- RabbitMQ configuration

---

# Integration Events

The project keeps the number of events intentionally small.

```text
OrderCreatedIntegrationEvent

InventoryReservedIntegrationEvent

InventoryReservationFailedIntegrationEvent

PaymentCompletedIntegrationEvent

PaymentFailedIntegrationEvent

InventoryReleaseRequestedIntegrationEvent

OrderConfirmedIntegrationEvent

OrderCancelledIntegrationEvent
```

Integration events should include common metadata:

```text
EventId

CorrelationId

OccurredAt
```

---

# Main Checkout Flow

```text
Browse Products
      ↓
Add To Basket
      ↓
Checkout
      ↓
Create Order
      ↓
OrderCreatedEvent
      ↓
RabbitMQ
      ↓
Inventory Service
      ↓
Reserve Stock
      ↓
InventoryReservedEvent
      ↓
Payment Service
      ↓
Create Stripe Checkout Session
      ↓
Stripe Checkout
      ↓
Customer Pays
      ↓
Stripe Webhook
      ↓
Payment Service
      ↓
PaymentCompletedEvent
      ↓
RabbitMQ
      ↓
Ordering Service
      ↓
Order Confirmed
      ↓
OrderConfirmedEvent
      ↓
Notification Worker
```

---

# Failure Flow

Example payment failure:

```text
Payment Failed
      ↓
Stripe Webhook
      ↓
Payment Service
      ↓
PaymentFailedEvent
      ↓
RabbitMQ
      ↓
Ordering Service
      ↓
Order Cancelled
      ↓
InventoryReleaseRequestedEvent
      ↓
Inventory Service
      ↓
Release Reserved Stock
```

This demonstrates **Saga-style choreography and compensation** without introducing a dedicated Saga orchestrator.

---

# Stripe Integration

Stripe Test Mode is used as the external payment provider.

```text
Next.js
   │
   │ Request Checkout
   ▼
Payment Service
   │
   │ Secret Key
   ▼
Stripe
   │
   │ Checkout
   ▼
Customer
```

The Stripe Secret Key must never be exposed to the frontend.

The frontend only receives data required to redirect or interact with Stripe Checkout.

---

## Stripe Webhook

Example endpoint:

```http
POST /api/payments/webhooks/stripe
```

Flow:

```text
Stripe
   │
   │ Webhook
   ▼
Payment Service
   │
   ├── Verify Signature
   ├── Check Event Idempotency
   ├── Update Payment
   ├── Create Outbox Message
   └── Return HTTP 2xx
```

The backend must use Stripe Webhooks as the source of truth for payment completion rather than relying only on frontend redirects.

---

# Idempotency

Distributed operations must tolerate duplicate delivery.

Important cases include:

```text
Stripe Webhook

PaymentCompletedEvent

Inventory Reservation

Inventory Release
```

Example:

```text
Stripe Event
     ↓
StripeEventId
     ↓
Already Processed?
   /            \
 Yes            No
  │              │
Ignore         Process
```

The same payment must never be charged or processed twice.

---

# Transactional Outbox

Ordering and Payment may use a lightweight Transactional Outbox.

Instead of:

```text
Save Database
      ↓
Publish RabbitMQ Event
```

use:

```text
Database Transaction

├── Business Change
└── Outbox Message

        ↓

      COMMIT
```

A background publisher then publishes pending messages to RabbitMQ.

The initial implementation should remain simple and should not introduce a large generic messaging framework.

---

# Database Strategy

Each service owns its data.

```text
Catalog
   ↓
catalog_db

Ordering
   ↓
ordering_db

Inventory
   ↓
inventory_db

Payment
   ↓
payment_db

Basket
   ↓
Redis
```

The PostgreSQL databases may run on the same PostgreSQL instance.

Services must never directly query another service's database.

For example:

```text
Ordering Service
      X
      │
      ▼
Catalog Database
```

is not allowed.

Services communicate through APIs or integration events.

---

# Frontend Scope

The frontend focuses on the main purchasing journey.

## Pages

```text
/
Product Listing

/products/[id]
Product Detail

/cart
Shopping Basket

/checkout
Checkout Summary

/payment/success
Payment Result

/orders/[id]
Order Detail
```

The following features are outside the current scope:

- Admin Dashboard
- Authentication
- User Profile
- Reviews
- Wishlist
- Recommendation Engine
- Promotion
- Voucher
- Shipping Tracking
- Advanced Search

---

# Logging

Services use structured logging with:

```text
Serilog
   ↓
Seq
```

Important values should be included in logs where appropriate:

```text
OrderId

PaymentId

CorrelationId

TraceId
```

---

# Testing

Testing focuses on critical business logic rather than broad coverage.

Use:

```text
xUnit
```

Priority scenarios:

```text
Create Order

Insufficient Inventory

Inventory Reservation

Payment Success

Payment Failure

Duplicate Stripe Webhook

Order Cancellation
```

Large integration-test infrastructure is outside the initial scope.

---

# Docker

The system should be runnable through Docker Compose.

```text
Next.js

YARP Gateway

Catalog Service

Basket Service

Ordering Service

Inventory Service

Payment Service

Notification Worker

PostgreSQL

Redis

RabbitMQ

Seq
```

Run:

```bash
docker compose up -d
```

---

# Team Distribution

The project is designed for a 3-person team.

## Developer 1 — Commerce

```text
Catalog Service

Basket Service

Redis

Product Seed Data

YARP Routes
```

## Developer 2 — Order Processing

```text
Ordering Service

Inventory Service

RabbitMQ

Integration Events

Saga Compensation

Outbox
```

## Developer 3 — Payment & Frontend

```text
Payment Service

Stripe Checkout

Stripe Webhooks

Next.js

Notification Worker
```

Each developer owns their assigned services end-to-end.

---

# Development Plan

## Day 1 — Core Features

Target:

```text
Catalog working

Basket working

Order creation working

Inventory working

Next.js connected through Gateway

PostgreSQL and Redis running
```

End-of-day expected flow:

```text
Next.js
   ↓
Gateway
   ↓
Catalog

Product browsing ✅

Basket ✅

Order creation ✅
```

---

## Day 2 — Distributed Checkout

Implement:

```text
RabbitMQ

Inventory reservation

Stripe Checkout

Stripe Webhook

PaymentCompletedEvent

PaymentFailedEvent
```

End-of-day target:

```text
Product
   ↓
Basket
   ↓
Checkout
   ↓
Order
   ↓
Inventory
   ↓
Stripe
   ↓
Payment
   ↓
Order Confirmed
```

The complete happy path should work before additional features are added.

---

## Day 3 — Reliability & Demo

Focus on:

```text
Payment failure

Inventory release

Transactional Outbox

Idempotency

Notification Worker

Serilog + Seq

Critical tests

Docker Compose

UI polish

README

Demo preparation
```

No major new business features should be introduced on Day 3.

---

# Definition of Done

The project is considered complete when the following end-to-end scenario works:

```text
1. Browse technology products

2. View product details

3. Add products to Basket

4. Checkout

5. Create Order

6. Reserve Inventory

7. Create Stripe Checkout Session

8. Complete payment using Stripe Test Mode

9. Stripe sends Webhook

10. Payment Service processes Webhook

11. Payment Service publishes RabbitMQ event

12. Ordering Service consumes event

13. Order becomes Confirmed

14. Notification Worker receives OrderConfirmed event
```

The failure scenario should also demonstrate:

```text
Payment Failed
      ↓
Order Cancelled
      ↓
Inventory Released
```

---

# Development Principles

- Each microservice owns one business capability.
- Each microservice owns its own data.
- Services must not directly access another service's database.
- Domain code must not depend on Infrastructure.
- Controllers must not contain business logic.
- Cross-service workflows should use integration events.
- Message consumers should be idempotent.
- Stripe integration belongs only to Payment Service.
- Stripe secrets must never be exposed to the frontend.
- Payment state must be confirmed through Stripe Webhooks.
- Prefer working end-to-end functionality over unnecessary abstractions.
- AI-generated code must be reviewed before integration.
- Do not introduce infrastructure that does not contribute directly to the project demo.

---

# Stretch Goals

Only implement these after the main checkout flow is stable:

- OpenTelemetry
- Distributed tracing
- Testcontainers
- Retry / Circuit Breaker
- Dead Letter Queue configuration
- Additional integration tests
- Authentication / Authorization

---

# Goal

ShopSphere demonstrates a practical implementation of **.NET 10 Microservices**, **Clean Architecture**, **Next.js**, **RabbitMQ**, **Redis**, **Stripe**, and **Event-Driven Architecture** through a complete technology E-commerce checkout workflow.

The project prioritizes a working distributed business flow over unnecessary infrastructure complexity.
# Kế hoạch hoàn thiện ShopSphere theo lịch học

Ngày đối chiếu: 07/10/2026. Đây là kế hoạch đề xuất, chưa phải các thay đổi đã triển khai.

## Căn cứ và cách đọc trạng thái

- Nguồn học: `C:/Users/Admin/Downloads/schedule_fast_track.xlsx`, sheet `Sheet1`. File liệt kê chương trình Week 1–5, không ghi nhận buổi nào Hung đã hoàn thành. Vì vậy, “theo kiến thức đã học” trong kế hoạch được hiểu là đối chiếu với nội dung lịch học; không suy ra tiến độ học cá nhân.
- Các assignment và hướng dẫn trong workbook là nội dung chương trình, không phải yêu cầu tự động thực hiện. Workbook được đọc và giữ nguyên.
- Trạng thái dự án dựa trên code hiện tại và kết quả kiểm tra đã ghi trong `docs/validation.md`. Đợt lập kế hoạch này không chạy lại toàn bộ build/test, không kiểm tra CI từ xa và không thử thanh toán Stripe thật.
- “Đã có” nghĩa là có implementation và bằng chứng tương ứng; không đồng nghĩa đã đáp ứng vận hành production. “Một phần” nghĩa là đã có nền nhưng chưa đủ nội dung bài học. “Chưa có” nghĩa là chưa thấy implementation trong source được kiểm tra.
- Capstone ở D134 yêu cầu mini-eShop có Catalog, Cart, Gateway, IAM; giao tiếp gRPC/RabbitMQ và chạy Docker. Ordering, Inventory và Payment hiện có là phần mở rộng hữu ích của ShopSphere.
- Một số checklist trong `docs/feature-roadmap.md` còn mô tả auth trước khi Google login được thêm. Kế hoạch này ưu tiên code hiện tại khi có khác biệt.

## Những chức năng cần có và hiện trạng

| Hạng mục | Hiện tại | Việc còn cần làm | Mức cần thiết |
|---|---|---|---|
| Danh mục, danh sách, chi tiết sản phẩm | Có API, 6 sản phẩm seed và UI | Search, filter giá/brand, sort, phân trang ở server; bộ dữ liệu lớn hơn | Cốt lõi; là nền cho SQL/cache |
| Hình ảnh các dòng thiết bị | Có registry SVG chung và fallback | Dùng lại theo loại thiết bị khi thêm sản phẩm | Giữ cách hiện tại; không cần ảnh riêng cho từng SKU |
| Giỏ hàng | Redis, TTL, giới hạn số lượng, thao tác atomic | Giảm nhiều lượt gọi Catalog khi đọc giỏ; thêm integration test | Cốt lõi |
| Đăng nhập và dữ liệu cá nhân | Google OAuth/cookie, chọn tài khoản, ownership check | IAM bằng Keycloak/OIDC/JWT để bám bài học; giữ liên kết tài khoản cũ | Cần cho coverage IAM/JWT/Keycloak |
| Chống spam | Rate limit Gateway, HTTP 429 và Retry-After | Đo lại với luồng mới; quota dùng chung chỉ khi chạy nhiều Gateway | Giữ và mở rộng đúng quy mô |
| Checkout, tồn kho, trạng thái đơn | Đã có; giá tính ở server, idempotency, giữ/trả kho, chống tranh chấp | Test tự động với DB/broker thật; lịch sử đơn theo người dùng | Checkout cốt lõi; lịch sử đơn là mở rộng hợp lý |
| Thanh toán | Demo và code Stripe; signed webhook fixtures đã kiểm tra | Tách provider để dễ test; xác minh checkout Stripe Test Mode thật | Hợp lý cho luồng đang có; chưa cần tiền thật |
| Thông báo đơn | Consumer ghi log mô phỏng email | Nếu gửi email thật: provider, delivery ledger, retry/idempotency | Email thật là mở rộng, không bắt buộc capstone |
| Quản trị sản phẩm/tồn kho | Chưa có UI/API CRUD có role admin | CRUD tối thiểu, điều chỉnh tồn kho có kiểm soát và invalidation cache | Sau phần cốt lõi; hữu ích để demo AuthZ và cache |
| Triển khai và kiểm tra | Docker Compose, CI build/test/lint, Seq, health endpoint | Testcontainers trong CI, readiness kiểm tra dependency, runbook demo | Cần để chứng minh hệ thống chạy lại được |

## Đối chiếu từng nhóm kiến thức

P1 = cần để hoàn thiện bản học tập/capstone. P2 = mở rộng có ích sau P1. Các phần “đã có” không cần làm lại chỉ để đổi tên công nghệ.

| Nội dung lịch học / vùng nguồn | Đã có gì | Thiếu gì và áp dụng thế nào | Đánh giá |
|---|---|---|---|
| Clean Code, SOLID, refactoring; D4:D19 | Layer Application/Domain/Infrastructure, interface/DI; domain có quy tắc trạng thái thật | Payment đang chọn Demo/Stripe trong cùng implementation và tạo Stripe SDK trực tiếp. Tách `IPaymentProvider`, adapter SDK; giữ transaction/outbox ở lớp điều phối | P1; có vấn đề cụ thể để áp dụng SRP/DIP/OCP |
| Factory Method, Singleton; D23 | Redis ConnectionMultiplexer đăng ký singleton qua DI | Lifetime singleton đã có ví dụ thực tế. Factory Method chỉ thêm khi có nhu cầu tạo đối tượng với quy trình khác nhau; chọn provider bằng DI không tự động chứng minh Factory Method | Không ép thêm pattern chỉ để tick checklist; có thể làm bài minh họa riêng nếu cần chấm đúng pattern |
| Observer, Strategy, DI; D27 | DI và pub/sub integration event | Strategy phù hợp cho Demo/Stripe. Pub/sub RabbitMQ minh họa event-driven, nhưng không tự động tương đương bài Observer trong-process | Strategy P1; Observer riêng chỉ khi rubric yêu cầu hoặc có use case |
| SQL, Explain Plan, B-tree, composite/covering index; D32:D45 | PostgreSQL, EF Core, PK và index phục vụ messaging ở một số DB | Catalog mới truy vấn đơn giản, chưa có benchmark/index theo các bộ lọc mới. Đo `EXPLAIN (ANALYZE, BUFFERS)` trước/sau; chọn index theo query và phân bố dữ liệu | P1; không thêm index tùy tiện vào mọi cột |
| Connection pooling; D46:D49 | Dùng Npgsql; pooling kết nối mặc định của driver | Chưa có bằng chứng tuning/load test. Đo pool exhaustion, thời gian giữ connection, đặt giới hạn theo số service/replica và DB budget | Một phần; không nhầm với `AddDbContextPool` [1] |
| Cache-aside/read-through, Redis, multi-level; D50:D60 | Redis đang lưu giỏ hàng, dùng hash/TTL/Lua | Chưa có cache truy vấn Catalog. Thêm cache-aside với L1 memory + L2 Redis, TTL, chống stampede, invalidation và fallback | P1; Redis basket không đủ để kết luận đã học xong caching [2] |
| Design thinking, system design, UML, NFR; D61:D78 | Có architecture, project brief, hợp đồng event và mô tả luồng | Bổ sung capacity assumption, sequence checkout thành công/thất bại, quyết định cache/gRPC/IAM và benchmark đo được | P1; ưu tiên tài liệu giải thích được lựa chọn |
| API design, contract-first, REST; D70:D76 | REST endpoints, shared event records, ProblemDetails | Thiết kế OpenAPI/DTO cho search/pagination trước khi code; contract `.proto` cho gRPC; quy ước lỗi, giới hạn và tương thích contract | P1; có record dùng chung chưa chứng minh đầy đủ contract-first |
| Testing pyramid, xUnit; D79:D85 | Domain tests và HTTP Gateway tests qua WebApplicationFactory | Bổ sung test cho query mới, cache, provider và IAM; phân biệt unit/integration/end-to-end | Một phần; không bắt đầu test từ số 0 |
| Moq: Verify, Callback, CallBase; D83:D90 | Có test doubles/stubs, chưa dùng Moq | Mock provider/catalog client; Verify không gọi payment khi đơn không hợp lệ, capture tham số idempotency. Callback/CallBase chỉ dùng khi có hành vi cần quan sát | P1 cho Moq; không viết test chỉ để gọi đủ mọi API của thư viện |
| Testcontainers + xUnit; D91:D93 | Có script smoke với Compose và signed webhook fixtures | Chưa có Testcontainers. Thêm PostgreSQL/Redis/RabbitMQ fixtures cho migrations, race, outbox, duplicate event và cache | P1; môi trường test tách dữ liệu demo [3] |
| Microservices, DDD, Clean Architecture; D95:D105 | Service boundaries, DB ownership, domain methods và event orchestration | Tinh chỉnh invariant/encapsulation; Money hoặc value object khi giúp tránh lỗi thật; kiểm tra dependency direction | Phần lớn nền đã có; không cần viết lại toàn bộ solution |
| REST/gRPC, protobuf; D107:D110 | Service-to-service đang dùng HTTP/REST | Chưa có gRPC. Catalog cung cấp batch `GetProductsByIds`; Basket gọi một lần thay vì tuần tự gọi mỗi sản phẩm | P1; lý do nghiệp vụ rõ ràng, public API vẫn REST [4] |
| RabbitMQ, async/pub-sub; D112:D117 | MassTransit, RabbitMQ, event contract, EF outbox/inbox ở các service stateful, retry | Bổ sung test mất kết nối/duplicate, quan sát backlog và runbook replay error queue. Notification gửi thật cần ledger riêng | Nền đã có; không thêm broker thứ hai |
| Gateway/BFF YARP/Ocelot; D118:D121 | YARP Gateway, Next.js proxy, auth/ownership/rate limit tại Gateway | Chuẩn hóa policy khi thêm routes, propagate correlation và identity đã xác thực | Đã có nền; không cần thêm Ocelot cạnh YARP |
| Identity, JWT, Keycloak; D123:D126 | Google login, cookie HttpOnly, chọn tài khoản, ownership và Origin checks | Chưa có Keycloak/JWT bearer/role admin. Dùng Keycloak làm IAM, Google identity broker, token được kiểm tra issuer/audience/expiry và policy | P1 nếu mục tiêu bám lịch học; giữ Google login qua broker [5] |
| Docker, multistage, Compose; D128:D131 | Dockerfiles/Compose và CI workflow | Thêm cấu hình Keycloak, gRPC HTTP/2, test job có Docker; kiểm chứng môi trường sạch và readiness | Phần lớn đã có; không cần Kubernetes cho capstone |
| Capstone; D133:D136 | Catalog, Cart, Gateway, RabbitMQ, Docker cùng checkout mở rộng | Thiếu phần IAM theo Keycloak/JWT và gRPC để khớp nội dung chương trình | Hoàn thiện các khoảng trống thay vì thêm nhiều service mới |
| AWS hoặc AZ-104 và OJT; D137:D138 | Chưa thấy deployment cloud được xác minh trong repo | Chọn một nền tảng, deploy sau khi local capstone ổn; tách bài học cloud khỏi yêu cầu mini-eShop | P2; không cần làm cả AWS và Azure cùng lúc |

Lịch học nhắc .NET 8 và có tài liệu SQL Server; repo hiện dùng .NET 10/PostgreSQL. Giữ stack hiện tại để áp dụng cùng nguyên lý; chỉ đổi nếu bài chấm có ràng buộc phiên bản/DB cụ thể. Cú pháp và công cụ đo phải dùng đúng PostgreSQL [6].

## Phạm vi triển khai đề xuất

### 1. Làm Catalog có dữ liệu đủ để học và API đúng quy mô

- Chốt contract: `q`, `category`, `brand`, khoảng giá, sort, page/pageSize; giới hạn pageSize, sort allowlist, thứ tự ổn định có ID làm tie-breaker.
- DTO trả danh sách, metadata phân trang; filter/sort chạy trong DB. Frontend gọi theo query và thể hiện loading/empty/error; không tải toàn bộ catalog về để lọc.
- Thêm khoảng 500–1.000 SKU hợp lý trong các nhóm thiết bị hiện có. Giữ icon dùng chung theo loại thiết bị; chỉ thêm SKU/device type khi có nghĩa nghiệp vụ rõ.
- Viết seed/import idempotent; giữ 6 ID sản phẩm cũ và tạo inventory tương ứng. Không đưa hàng chục nghìn sản phẩm vào `HasData`/migration.
- Tạo dataset benchmark khoảng 50.000 sản phẩm ở DB riêng khi đo SQL. Sáu hoặc vài trăm dòng không đủ để kết luận lợi ích index; không làm mất dữ liệu demo hiện có.
- Chốt workload trước khi chọn composite index. B-tree không tự giải quyết tốt search chứa chuỗi `%term%`; chỉ dùng trigram/full-text PostgreSQL nếu số đo chỉ ra nhu cầu.

Hoàn thành khi API không trả dữ liệu vượt pageSize, filter/sort/pagination đúng, UI không cần toàn bộ danh sách, seed chạy lại không trùng và sản phẩm mới mua được qua luồng checkout.

### 2. Đặt SOLID/pattern và gRPC vào đúng điểm cần thiết

- Tách provider Demo/Stripe bằng interface và DI; orchestration vẫn quản lý transaction, idempotency và outbox. Không đưa SDK Stripe vào domain.
- Nếu thêm value object Money, bảo toàn currency/decimal precision và snapshot giá của đơn cũ; không thay đổi hành vi settlement chỉ để làm đẹp cấu trúc.
- Viết `.proto` trước; `GetProductsByIds` trả đúng các ID yêu cầu, giới hạn batch size và quy ước missing product. Giá decimal phải biểu diễn chính xác theo contract, tránh float/double.
- Basket chuyển các lượt gọi Catalog tuần tự thành một lượt batch gRPC; có deadline/cancellation, connection reuse, HTTP/2 trong Compose và lỗi hợp lý khi Catalog không sẵn sàng.
- Public frontend/Gateway tiếp tục dùng REST; chỉ thêm gRPC ở giao tiếp nội bộ có lợi ích rõ. Chưa cần đổi mọi API sang gRPC.

Hoàn thành khi test chứng minh cùng kết quả giỏ hàng, một batch Catalog thay cho N HTTP calls, provider có thể thay bằng mock và lỗi không tạo payment/đơn trùng.

### 3. Thêm cache Catalog và đo SQL/pooling

- Cache-aside cho product detail/categories trước; cache trang danh sách sau khi query keys đã được normalize và giới hạn số biến thể.
- L1 memory, L2 Redis; có thể dùng HybridCache thay vì tự viết toàn bộ cơ chế phối hợp. Đặt TTL và memory/key budget; chống nhiều request cùng tải một key.
- Basket và cache dùng namespace khác; thiết kế instance/policy sao cho evict cache không làm mất dữ liệu basket. Redis đang chứa dữ liệu giỏ hàng nên không được xem toàn bộ Redis là cache có thể xóa tùy ý.
- Cache invalidation có contract rõ: product update/import làm mất hiệu lực detail, categories và các trang liên quan; dùng TTL có giới hạn cùng cơ chế version/event phù hợp. Có thể dùng importer để demo invalidation trước khi có admin UI.
- HybridCache invalidation không tự xóa L1 của các server khác. Bản đầu chạy một Catalog replica; trước khi scale cần test cơ chế version/invalidation cho nhiều replica [2].
- Redis lỗi: Catalog có thể đọc DB với timeout và giới hạn tải; Basket không có DB fallback nên phải trả lỗi kiểm soát, không báo giỏ rỗng như thể đọc thành công.
- Checkout/tồn kho vẫn dùng dữ liệu authoritative. Cache Catalog hiển thị không quyết định số lượng còn bán được; đường lấy giá lúc checkout phải có freshness policy rõ và kiểm thử.
- Ghi baseline và kết quả sau thay đổi: p50/p95, throughput, số query, cache hit/miss và query plan. Giữ cùng dataset, máy và workload; không hứa tăng tốc khi chưa đo.
- Kiểm tra pool budget tổng của tất cả service/replica; rà soát checkout hiện giữ DB transaction trong lúc gọi Basket. Chỉ refactor phạm vi transaction sau khi bảo toàn tính đúng và idempotency.

Hoàn thành khi cache hit/miss, hết TTL, thay đổi dữ liệu, request đồng thời và Redis outage đều có test; report trước/sau giải thích được index và pool settings.

### 4. Hoàn thiện IAM/JWT nhưng giữ trải nghiệm Google hiện có

- Thêm Keycloak vào Compose, realm/client/roles cấu hình tái lập được; cấu hình Google thành identity provider qua broker [5].
- Giữ browser session bằng cookie HttpOnly/BFF; token được quản lý phía server, không đưa token vào localStorage. Thiết kế logout và chọn tài khoản rõ ràng khi chuyển sang broker.
- Map `(issuer, subject)` của danh tính vào CustomerId nội bộ ổn định. Chuyển từ Google subject sang Keycloak subject không được làm mất giỏ/đơn cũ; không tự liên kết chỉ dựa trên email chưa chứng minh quyền sở hữu.
- Gateway và protected APIs kiểm tra token/policy phù hợp. Không tin CustomerId/role do browser gửi; service không được truy cập vòng qua Gateway để bỏ qua kiểm tra private data.
- Tối thiểu customer role; thêm admin role cho API quản trị nếu triển khai bước mở rộng. Giữ rate limit và phân bucket theo danh tính nội bộ ổn định.
- Thêm lịch sử đơn theo tài khoản với pagination nếu muốn hoàn thiện UX; backend lấy user từ danh tính đã xác thực và có test chặn đọc đơn người khác.

Hoàn thành khi Google login/đổi tài khoản/logout vẫn hoạt động, dữ liệu cũ còn đúng chủ, token hết hạn/sai audience bị từ chối, hai tài khoản không đọc chéo giỏ/đơn và role được thực thi ở backend.

### 5. Hoàn thiện bằng chứng kiểm thử, CI và demo

- Viết test cùng mỗi bước trên; đây là bước bổ sung môi trường integration/CI, không phải đợi đến cuối mới viết test.
- Unit xUnit kiểm tra invariant trạng thái, amount/quantity/currency và provider orchestration. Moq Verify/Callback phục vụ kiểm tra điều có ý nghĩa: tham số gửi Stripe, không gọi khi sai điều kiện, xử lý lỗi dependency.
- Testcontainers khởi tạo PostgreSQL, Redis, RabbitMQ [3]. Chạy migrations thật, seed fixture riêng; isolate DB/key/queue theo suite và dọn tài nguyên do test sở hữu.
- Kịch bản trọng tâm: concurrent checkout cùng idempotency key; tranh chấp tồn kho; duplicate event/webhook; outbox khi broker mất kết nối; retry sau restart; rollback/compensation; basket atomic; cache invalidation; AuthZ.
- Chuyển script smoke cũ sang authenticated harness: các script anonymous trước Google login không đủ cho protected endpoints hiện tại. Test auth dùng danh tính fixture kiểm soát được; không phụ thuộc Google UI/network trong mỗi CI run.
- CI giữ backend/frontend checks và thêm integration job có Docker; xử lý readiness và chờ event có điều kiện, tránh sleep cố định dễ flaky.
- Health: phân biệt liveness với readiness kiểm tra DB/Redis/broker cần thiết; Seq đã có, bổ sung correlation across REST/gRPC/events và runbook error queue replay có idempotency.
- Stripe: kiểm tra tạo Checkout Session và thanh toán Test Mode qua webhook thực. Signed fixtures hiện tại không thay thế việc gọi Stripe bên ngoài; bước này phụ thuộc test credentials/webhook listener.
- Tài liệu cuối: component diagram, sequence thành công/thất bại, API/proto contracts, capacity/NFR giả định, benchmark, test matrix và hướng dẫn demo từ môi trường sạch.

Hoàn thành khi CI thực sự chạy xanh, test infra tái lập được, demo chứng minh luồng thành công/thất bại/duplicate và report ghi rõ phần external nào đã/chưa kiểm chứng.

## Thứ tự và ước lượng

Ước lượng cho bản tối thiểu ở phạm vi trên, một người hiểu repo hiện tại; là công sức kỹ thuật, không lấy duration trong workbook làm thời gian triển khai. Thời gian chờ tài khoản/credentials và phát sinh migration IAM có thể làm tăng lịch.

| Chặng | Công sức dự kiến | Phụ thuộc / điểm chốt |
|---|---:|---|
| 1. Contract, dữ liệu, backend query và UI pagination | 8–12 giờ | Chốt loại filter/sort; test từ bước này |
| 2. Payment Strategy, contract-first batch gRPC | 10–14 giờ | Có contract Catalog ổn định |
| 3. L1/L2 cache, index/query/pool benchmark | 10–16 giờ | Có dữ liệu và workload; importer làm invalidation demo |
| 4. Keycloak/JWT, mapping tài khoản và AuthZ | 10–16 giờ | Chốt identity migration; Google OAuth callback cho broker |
| 5. Testcontainers/CI, runbook, Stripe test và báo cáo demo | 14–20 giờ | Tích hợp các chặng; tests được phát triển xuyên suốt |
| **Tổng bản tối thiểu** | **52–78 giờ** | Khoảng 26–39 ngày làm việc nếu dành 2 giờ/ngày |

Lịch sử đơn có UI đầy đủ, admin CRUD, email thật, distributed tracing đầy đủ và cloud deployment là các ticket bổ sung; chưa nằm trong tổng 52–78 giờ. Có thể bổ sung một endpoint nhỏ phục vụ demo trong từng chặng, nhưng phải điều chỉnh estimate nếu mở rộng thành module hoàn chỉnh.

## Mở rộng hợp lý sau bản tối thiểu

1. Lịch sử đơn/my orders: tạo lợi ích trực tiếp cho người đã login; áp dụng pagination, index theo CustomerId/CreatedAt và ownership.
2. Admin tối thiểu: quản lý sản phẩm, soft-disable sản phẩm và điều chỉnh tồn kho; không thay đổi snapshot các đơn cũ. Inventory adjustment phải giữ invariant available/reserved, có audit và transaction.
3. Email thật: provider + durable delivery ledger trước khi gửi, chống gửi trùng khi RabbitMQ redeliver; UI cho phép biết trạng thái gửi nếu cần.
4. Observability: OpenTelemetry cho một đường checkout qua HTTP/gRPC/events, dùng IDs/correlation và tránh log token/secret. Thêm metrics/trace khi có câu hỏi cần trả lời, không dựng cả bộ monitoring chỉ để trang trí.
5. Chạy nhiều replica: xử lý quota rate limit dùng chung, L1 invalidation, session/key sharing và tổng pool budget; kiểm thử thay vì giả định Compose một replica đại diện cho scale-out.
6. Chọn AWS hoặc Azure cho OJT/cloud. Có local demo, CI và tài liệu trước; không cần Kubernetes, Kafka cạnh RabbitMQ hay Elasticsearch khi nhu cầu chưa xuất hiện.

## Bằng chứng code đã đối chiếu

- Catalog endpoints/query/seed: `src/Services/Catalog/ShopSphere.Catalog.Api/Program.cs`, `src/Services/Catalog/ShopSphere.Catalog.Infrastructure/CatalogDb.cs`.
- Search/sort UI đang xử lý phía client: `frontend/shopsphere-web/src/features/catalog/catalog.tsx`; device icon registry: `frontend/shopsphere-web/src/lib/device-icons.ts`.
- Redis basket và gọi Catalog từng item: `src/Services/Basket/ShopSphere.Basket.Infrastructure/RedisBaskets.cs`; singleton connection: `src/Services/Basket/ShopSphere.Basket.Api/Program.cs`.
- Checkout/domain invariants: `src/Services/Ordering/ShopSphere.Ordering.Infrastructure/Orders.cs`, `src/Services/Ordering/ShopSphere.Ordering.Domain/Order.cs`, `src/Services/Inventory/ShopSphere.Inventory.Domain/Stock.cs`.
- Demo/Stripe và webhook: `src/Services/Payment/ShopSphere.Payment.Infrastructure/Payments.cs`, `src/Services/Payment/ShopSphere.Payment.Domain/Payment.cs`.
- Google/auth/ownership/quota: `src/Gateway/ShopSphere.Gateway/StoreAuthentication.cs`, `src/Gateway/ShopSphere.Gateway/StoreRateLimiting.cs`.
- Messaging/outbox/retry và health: `src/BuildingBlocks/ShopSphere.Messaging/ServiceSetup.cs`; event contracts: `contracts/ShopSphere.Contracts/Events.cs`.
- Email mô phỏng: `src/Workers/ShopSphere.Notification.Worker/Program.cs`.
- Tests/dependencies: `tests/ShopSphere.Tests/ShopSphere.Tests.csproj` và các test trong cùng thư mục; CI: `.github/workflows/ci.yml`.
- Target .NET 10: `Directory.Build.props`; Docker: `Dockerfile`, `docker-compose.yml`; các kiểm tra đã thực hiện: `docs/validation.md`.

## Tài liệu kỹ thuật tham chiếu

[1] Npgsql xác nhận connection pooling mặc định: [Basic Usage](https://www.npgsql.org/doc/basic-usage.html#pooling).

[2] HybridCache hỗ trợ memory cache và secondary distributed cache, chống stampede; invalidation không tự xóa memory cache ở server khác: [Microsoft HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0).

[3] Thư viện test dùng Docker để tạo dependency tạm cho integration tests: [Testcontainers for .NET](https://dotnet.testcontainers.org/).

[4] gRPC dùng protobuf contract `.proto` và sinh client/server code: [Microsoft gRPC services](https://learn.microsoft.com/en-us/aspnet/core/grpc/basics?view=aspnetcore-10.0).

[5] Keycloak hỗ trợ Google identity provider qua identity brokering: [Keycloak Google provider](https://www.keycloak.org/docs/latest/server_admin/index.html#_google).

[6] Đo execution plan bằng công cụ đúng DB hiện tại: [PostgreSQL Using EXPLAIN](https://www.postgresql.org/docs/current/using-explain.html).

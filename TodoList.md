# Detailed Refactoring Action Plan

## 1. Fix Compilation Errors (WebSockets & AspNetCore)

**Target: `src\ProtobuffEncoder.WebSockets\ProtobufWebSocketConnection.cs`**
- [ ] Add `public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; internal set; }` property so ASP.NET route builders can access scoped DI services directly on the connection.
- [ ] Add `public System.Collections.Concurrent.ConcurrentDictionary<string, object> Items { get; } = new();` to allow developers (and our route extension) to store per-connection state, such as DI scopes and endpoint instances.

**Target: `src\ProtobuffEncoder.WebSockets\WebSocketEndpointRouteBuilderExtensions.cs`**
- [ ] In `MapProtobufWebSocket` (Line ~70), immediately after instantiating `connection = new ProtobufWebSocketConnection(...)`, assign `connection.HttpContext = ctx;`.
- [ ] Rewrite the `MapProtobufWebSocket<TEndpoint, TSend, TReceive>` extension (Lines 42-90) to eliminate CS0165 closures:
  - inside `options.OnConnect`: create an `IServiceScope` via `conn.HttpContext!.RequestServices.CreateScope()`, resolve `TEndpoint` from `scope.ServiceProvider`. Store `scope` and `endpoint` in `conn.Items`. Await `endpoint.OnConnectedAsync(conn)`.
  - inside `options.OnMessage`: retrieve `endpoint` from `conn.Items["Endpoint"]` and await `endpoint.OnMessageReceivedAsync(conn, msg)`.
  - inside `options.OnDisconnect`: retrieve `endpoint` and invoke `OnDisconnectedAsync`. Retrieve `scope` and invoke `scope.Dispose()`.
  - inside `options.OnError`: retrieve `endpoint` and invoke `OnErrorAsync`.
- [ ] **Verification**: Run `dotnet build -c Release` and confirm 0 errors.

## 2. Test Coverage: WebSockets Quick Endpoint

**Target: `tests\ProtobuffEncoder.WebSockets.Tests\ProtobufWebSocketClientEndpointTests.cs`**
- [ ] Create a test class mocking `ProtobufWebSocketClientEndpoint<TestReq, TestRes>` by inheriting from it.
- [ ] Implement a unit test `StartAsync_ConnectsAndBeginsListening` that mocks `ProtobufWebSocketClientOptions` and verifies the underlying `ProtobufWebSocketClient` successfully connects.
- [ ] Implement a unit test `SendAsync_PassesMessageToClient` verifying outbound messages are appropriately serialized and queued.
- [ ] Implement a test suite establishing an end-to-end local `WebSocketServer` connection and sending/receiving a real payload successfully.

## 3. Test Coverage: AspNetCore Minimal APIs

**Target: `tests\ProtobuffEncoder.AspNetCore.Tests\MinimalApiExtensionsTests.cs`**
- [ ] Scaffold standard `WebApplicationFactory` for ASP.NET test server generation.
- [ ] **`MapProtobufSender<T>` Test**: Make an HTTP GET request to the mock sender route. Assert HTTP status code is 200, Content-Type is `application/x-protobuf`, and response payload correctly deserializes into the expected struct.
- [ ] **`MapProtobufReceiver<T>` Test**: Make an HTTP POST request to the mock receiver route with a valid protobuf binary body. Assert the mock handler acknowledges receiving the exact same object properties. Verify doing this without the correct `Content-Type` yields a HTTP 415 response.
- [ ] **`MapProtobufDuplex<TReq, TRes>` Test**: Validate full bidirectional parsing under an HTTP POST endpoint where `ctx.Request.Body` and `ctx.Response.Body` interact concurrently correctly.

## 4. Test Coverage: Grpc Quick Endpoints

**Target: `tests\ProtobuffEncoder.Grpc.Tests\ProtobufGrpcStreamServiceTests.cs`**
- [ ] Create a concrete `TestStreamService` inheriting from `ProtobufGrpcStreamService<TestRequest, TestResponse>`.
- [ ] Write `DuplexStreamingAsync_TransformsRequestsToResponses_Successfully`. Use a mocked `IAsyncStreamReader<TestRequest>` yielding 3 items, and verify `IServerStreamWriter<TestResponse>.WriteAsync` is invoked exactly 3 times with the transposed response equivalents.
- [ ] Validate `ClientStreamingAsync_AggregatesRequests_Correctly`.

## 5. Documentation: Writerside & Markdown

**Target: `Writerside\topics\README.md` & `docs\README.md`**
- [ ] Add `## Quick API Endpoints` subsection underneath existing usage guides.
- [ ] Add code block illustrating `ProtobufWebSocketEndpoint` usage.
- [ ] Add explicit code blocks for `MapProtobufSender<T>` and `MapProtobufReceiver<T>`.
- [ ] Document `.NET 8/9/10` performance semantics (e.g., `ArrayPool` buffer usage automatically scaling efficiency upon runtime inference constraint matching).

## 6. Project History Updates

**Target: `CHANGELOG.md`**
- [ ] Under `## [Unreleased]` section add:
  - Add feature: `ProtobufWebSocketClientEndpoint` abstract class for fast bidirectional socket clients.
  - Add feature: Minimal APIs `MapProtobufSender`, `MapProtobufReceiver`, `MapProtobufDuplex` for AspNetCore direct routing.
  - Add feature: `ProtobufGrpcStreamService` base implementation to ease gRPC server side bi-directional streaming logic.
  - Changed API: Concealed `WireType`, `FieldNumbering`, and `ProtoEncoding` from IntelliSense using `[EditorBrowsable(EditorBrowsableState.Never)]` while retaining runtime accessibility to preserve open-closed mechanics.
  - Add Feature: `PROTO016` syntax analyzer enforcing endpoint target structs appropriately leverage `[ProtoContract]`.

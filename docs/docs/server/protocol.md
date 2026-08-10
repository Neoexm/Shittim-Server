---
id: protocol
title: Protocol handling
---

## The gateway

The client sends everything through one endpoint, `POST /api/gateway` on the gateway port. The body is a protocol envelope: a protocol number and a JSON payload, optionally compressed and encrypted.

The first exchange, `Account_CheckNexon` (50001), is RSA. The server's public key has been patched into the client's `global-metadata.dat` in three 150-byte chunks, and the server decrypts with `Config/GatewayPrivateKey.pem`. If that private key is missing the handshake fails and the client hangs on "Unpacking game resources" rather than reporting anything about keys.

After the handshake the session carries an AES key. In practice the in-session key ends up null on this client, so responses go out as plaintext - a fact worth knowing before chasing a crypto bug that is really a content bug.

## Serialization

The gateway uses Newtonsoft, not System.Text.Json, with three converters that exist to match the official server byte for byte:

- **Dates** are ISO strings with no timezone offset and whole-second precision, like `2026-07-27T04:12:50`. The one fractional value official ever emits is `DateTime.MaxValue`, `9999-12-31T23:59:59.9999999`, for never-ending dates in the game data. The converter is registered for `object`, not `DateTime`, because the typed base class would skip every nullable date on the wire.
- **Floats** that are whole numbers are written without a decimal point.
- **Vectors** must always carry x, y and z. The gateway's default settings drop zero-valued components, and the client's own deserializer throws on a missing one - inside a method with no try/catch, which kills the entire response and leaves the client silent.

Integer positions that omit zero components are correct and are a different code path.

## Handlers

Handlers live in `Shittim-Server/Core/NetworkProtocol/Handlers` and are ordinary classes taking their dependencies through the constructor. A method becomes a handler by carrying `[ProtocolHandler(Protocol.Something)]`:

```csharp
public class NetworkTimeHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public NetworkTimeHandler(IProtocolHandlerRegistry registry, ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.NetworkTime_Sync)]
    public async Task<NetworkTimeSyncResponse> Sync(SchaleDataContext db, NetworkTimeSyncRequest request, NetworkTimeSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        response.ReceiveTick = response.ServerTimeTicks;
        response.EchoSendTick = DateTimeOffset.Now.Ticks;
        return response;
    }
}
```

The database context, the deserialized request and a pre-populated response are injected as parameters. The response already carries the server time and the session envelope, so a handler that has nothing to add can authenticate and return it unchanged.

Handlers are discovered by reflection at startup. There are 65 handler files covering everything from `AccountHandler` to `WorldRaidHandler`, and they are deliberately uneven - some are several hundred lines, some are under thirty, and 21 of them have no comments at all.

## Missing handlers

A protocol with no handler returns error code 500, which the client shows as "A request that cannot be processed". So does an unhandled exception in a handler that does exist. The two are indistinguishable from the client, so the server log is the only way to tell them apart.

## Diagnosing a fault

1. **The client's own log.** `Player.log` in the client's data directory gives the managed stack trace for a deserialization failure in one step. It is suppressed by default; removing `nolog=` from `boot.config` turns it back on.
2. **The wire dump.** `logs/wire-<date>.txt` has the request and response bytes in capture format.
3. **The server log.** Unhandled exceptions land here with a stack trace.

# relay-client

Tunnels HTTP traffic from [Azure Relay hybrid connections](https://learn.microsoft.com/en-us/azure/azure-relay/relay-hybrid-connections-protocol) to local services. Useful for exposing local dev servers to remote clients without opening firewall ports.

## Requirements

- .NET 9.0 SDK
- Azure Relay namespace with one hybrid connection per tunnel

## Setup

1. Copy the example config and fill in your values:

   ```bash
   cp config.json.example config.json
   ```

2. Edit `config.json`:

   ```json
   [
     {
       "connectionString": "Endpoint=sb://NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...;EntityPath=ENTITY",
       "targetUrl": "http://localhost:3000"
     }
   ]
   ```

   - **connectionString** — Azure Relay hybrid connection string (get it from the Azure portal under your hybrid connection's "Shared access policies")
   - **targetUrl** — local service to forward traffic to

   Add more objects to the array to run multiple tunnels simultaneously.

3. Run:

   ```bash
   dotnet run
   ```

   The console shows live tunnel status and a request log. Press `Ctrl+C` to stop.

## Getting a connection string

In the Azure portal: **Azure Relay** → your namespace → **Hybrid Connections** → select connection → **Shared access policies** → copy the primary connection string.

# relay-client

Tunnels HTTP traffic from [Azure Relay hybrid connections](https://learn.microsoft.com/en-us/azure/azure-relay/relay-hybrid-connections-protocol) to local services. Useful for exposing local dev servers to remote clients without opening firewall ports.

## Requirements

- .NET 9.0 SDK to build/install, and .NET 9.0 runtime to run
- Azure Relay namespace with one hybrid connection per tunnel

## Install

From the repository folder:

```powershell
dotnet pack -c Release -o .\bin\nupkg
dotnet tool install --global relay-client --add-source .\bin\nupkg
```

The installed command is `relay` and can be run from any folder. If your
terminal cannot find it, restart the terminal and ensure the .NET global tools
directory is on your `PATH`: `%USERPROFILE%\.dotnet\tools` on Windows, or
`$HOME/.dotnet/tools` on macOS/Linux.

To update an existing installation after rebuilding the package:

```powershell
dotnet tool update --global relay-client --add-source .\bin\nupkg
```

## Tagged releases

Push a version tag to build a new installer package automatically:

```powershell
git tag v_1.2.3
git push origin v_1.2.3
```

The GitHub Actions workflow builds `relay-client.1.2.3.nupkg`, saves it as a
workflow artifact, and attaches it to a GitHub Release for that tag. The package
version comes from the tag, overriding the project's default version.
Prerelease tags such as `v_1.2.3-beta.1` create prereleases. Tags must use
`v_MAJOR.MINOR.PATCH` with an optional prerelease suffix; invalid `v_` tags fail
with a validation error.

Download the `.nupkg` from the release into a local folder, then install it:

```powershell
dotnet tool install --global relay-client --version 1.2.3 --add-source .\downloads
```

For an existing installation, use `dotnet tool update` instead of
`dotnet tool install`. Packages are published to GitHub Releases, not NuGet.org.

## Configure and run

1. Open the configuration in your default editor:

   ```bash
   relay config
   ```

   Configuration is stored at `~/.relay-client/config.json` (on Windows,
   `%USERPROFILE%\.relay-client\config.json`), independent of your current folder.
   The first invocation creates the folder and an example configuration.
   Subsequent invocations preserve your existing file. On Windows, the default
   application for `.json` files is used, with Notepad as a fallback if none is
   registered. macOS uses the default text editor; Linux uses `xdg-open` and
   requires a desktop session with a default application for JSON files.

2. Replace the example values and save the file:

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
   relay
   ```

   The console shows live tunnel status and a request log. Press `Ctrl+C` to stop.

   The request log keeps only the most recent **10 requests**, newest first.
   Configure the limit with a positive integer:

   ```bash
   relay --max-log-rows 20
   ```

   Use a smaller limit if the log does not fit your terminal.

For development, `dotnet run` uses the same user configuration. Use
`dotnet run -- config` to edit it. To reuse a previous repository-local
`config.json`, copy it to `~/.relay-client/config.json`; it is not migrated
automatically.

## Getting a connection string

In the Azure portal: **Azure Relay** → your namespace → **Hybrid Connections** → select connection → **Shared access policies** → copy the primary connection string.

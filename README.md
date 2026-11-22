# DisputeDesk

DisputeDesk is a full-stack transaction dispute portal. Customers sign in, view their transactions, submit a dispute, and track the dispute in their history. The demo uses fictional customers and transactions.

## Run with Docker

You need Git, a browser, and Docker with the Compose plugin and Linux containers. The first build downloads the base images and dependencies. Compose starts its own PostgreSQL database and applies the EF Core migrations. The reviewer does not need a Neon account.

### Install Docker on Windows

If Docker is already running in Linux container mode, skip to [Start the demo](#start-the-demo).

1. Check that your Windows version and hardware meet the Docker Desktop requirements. Hardware virtualization must be enabled. [Docker Desktop for Windows](https://docs.docker.com/desktop/setup/install/windows-install/)
2. If WSL is missing, open PowerShell as Administrator and run `wsl --install --no-distribution`. Restart Windows if prompted. Then run `wsl --update` and `wsl --version` to check your installation. [Microsoft WSL commands](https://learn.microsoft.com/windows/wsl/reference)
3. Install Docker Desktop from the Windows download page, choose the WSL 2 backend, and start Docker Desktop. Keep it in Linux container mode. Docker Desktop includes Compose. [Docker Desktop WSL 2 setup](https://docs.docker.com/desktop/features/wsl/)
4. Open a new PowerShell window and verify that Docker can start a Linux container.

   ```powershell
   docker compose version
   docker run --rm hello-world
   ```

### Start the demo

1. Clone the branch containing the full application and open its repository folder. While packaging is under review, that branch is `feature/angular-frontend`. The GitHub account you use must have access to this private repository.

   ```powershell
   git clone --branch feature/angular-frontend https://github.com/MasegoMpshe/DisputeDesk.git
   Set-Location DisputeDesk
   ```
2. Copy `.env.example` to `.env` using `Copy-Item .env.example .env` in PowerShell or `cp .env.example .env` in Bash. The example values are disposable credentials for this local demo. To customize them before first startup, use an alphanumeric PostgreSQL password and set each demo password to at least six characters with an uppercase letter, a lowercase letter, a number, and a symbol.
3. Build and start the stack.

   ```sh
   docker compose up --build -d
   ```

4. Open [https://localhost:8443](https://localhost:8443). Compose generates a self-signed development certificate, so your browser may ask you to trust it or continue to this local address. The API and Angular UI share this HTTPS origin.
5. Sign in as `demo-naledi` with `DEMO_NALEDI_PASSWORD` from your `.env` file, or as `demo-thabo` with `DEMO_THABO_PASSWORD`.

To build just the application image, run `docker build -f DisputeDesk.Api/Dockerfile -t disputedesk .` from the repository root. Use Compose for the accompanying database, HTTPS configuration, and demo initialization.

- `docker compose ps -a` shows the database, one-time setup, and app status. The setup container should exit with code 0.
- `docker compose logs setup app db` shows startup and migration output.
- `docker compose down` stops the stack and keeps the database.
- `docker compose down --volumes` deletes the local database and certificate. Use this to start with fresh demo data or to apply new demo account or database passwords. This deletes saved disputes in the local Compose instance.

## Run without Docker

Use the .NET 10 SDK, Node.js 24.15 or newer in the 24.x line with npm, Git, and a browser. Use a **fresh PostgreSQL database** from either a local instance or a hosted provider such as Neon. For Neon, copy the direct .NET/Npgsql connection string. Visual Studio is optional.

The commands below are for PowerShell 7.2 or later. Run them from the repository root.

1. Trust the API development certificate.

   ```powershell
   dotnet dev-certs https --trust
   ```

2. Set the connection string and choose passwords for the two fictional customers. The prompts mask the values as you enter them.

   ```powershell
   $apiProject = '.\DisputeDesk.Api'
   dotnet user-secrets set 'ConnectionStrings:DisputeDesk' (Read-Host 'PostgreSQL connection string' -MaskInput) --project $apiProject
   dotnet user-secrets set 'DemoAccounts:demo-naledi:Password' (Read-Host 'Choose a demo-naledi password' -MaskInput) --project $apiProject
   dotnet user-secrets set 'DemoAccounts:demo-thabo:Password' (Read-Host 'Choose a demo-thabo password' -MaskInput) --project $apiProject
   ```

3. Apply the migrations, seed the fictional customers and transactions, and provision the demo logins. Use the same password rules as in the Docker setup.

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   dotnet run --project .\DisputeDesk.Api --no-launch-profile -- --Database:Migrate=true --Database:SeedDemoData=true --Database:ProvisionDemoAccounts=true
   ```

4. Start the API in this terminal. Swagger UI is at [https://localhost:7051/swagger](https://localhost:7051/swagger) in Development.

   ```powershell
   dotnet run --project .\DisputeDesk.Api --launch-profile https
   ```

5. From the repository root in a second terminal, install Angular's dependencies and start the frontend. Open [https://localhost:4200](https://localhost:4200) and sign in with either demo account and the password you chose. The Angular dev server uses a self-signed certificate and may prompt for local browser approval.

   ```powershell
   Set-Location .\disputedesk-web
   npm.cmd ci
   npm.cmd start
   ```

## Build and test

From the repository root on a machine with .NET and Node.js:

```powershell
dotnet build .\DisputeDesk.slnx --configuration Release
dotnet test .\DisputeDesk.slnx --configuration Release --no-build
Set-Location .\disputedesk-web
npm.cmd ci
npm.cmd run build
npm.cmd test -- --watch=false
```

On macOS or Linux, use `npm` and slash-separated paths. The repository's Docker smoke workflow builds the Compose stack and checks the HTTPS frontend route, demo sign-in, transaction list, dispute submission, and dispute history.

### Review the workflow

1. Sign in as `demo-naledi` and check that the dashboard shows two transactions and zero disputes on a fresh database.
2. Open a transaction, select **Dispute this transaction**, pick a reason, and submit.
3. Note the reference, then open **View all disputes** and check the entry remains visible after refreshing. Return to the dashboard and confirm the dispute count increased.
4. Sign out and sign in as `demo-thabo`. Verify that the other customer's dispute is absent from this account.

## Architecture

Angular organizes screens by feature. ASP.NET Core Identity issues a secure session cookie. The API takes the customer identity from that session and filters transaction and dispute queries by ownership. EF Core maps the domain to PostgreSQL. A unique database index prevents duplicates when two submissions target the same transaction. Dispute reasons are database reference data.

The Docker configuration is for local review. A deployment would supply its own TLS certificate, database credentials, and secret storage, with demo-account setup and transaction generation disabled.

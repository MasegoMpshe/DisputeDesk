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

1. Clone the public repository and open its folder. The default branch, `main`, contains the complete application.

   ```powershell
   git clone https://github.com/MasegoMpshe/DisputeDesk.git
   Set-Location DisputeDesk
   ```
2. Copy `.env.example` to `.env` using `Copy-Item .env.example .env` in PowerShell or `cp .env.example .env` in Bash. The example values are disposable credentials for this local demo. To customize them before first startup, use an alphanumeric PostgreSQL password and set each demo password to at least six characters with an uppercase letter, a lowercase letter, a number, and a symbol.
3. Build and start the stack.

   ```sh
   docker compose up --build -d
   ```

4. Open [https://localhost:8443](https://localhost:8443). Compose generates a self-signed development certificate, so your browser may ask you to trust it or continue to this local address. The API and Angular UI share this HTTPS origin.
5. Sign in as `demo-naledi` with `DEMO_NALEDI_PASSWORD` from your `.env` file, or as `demo-thabo` with `DEMO_THABO_PASSWORD`.

The optional `demo-reviewer` account is provisioned when `DEMO_REVIEWER_PASSWORD` is set in `.env`. If you are reusing an older `.env`, copy that entry from `.env.example`. Sign in as `demo-reviewer` with that password to open the review queue.

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

   To enable the demo reviewer, also set its password before provisioning.

   ```powershell
   dotnet user-secrets set 'DemoAccounts:demo-reviewer:Password' (Read-Host 'Choose a demo-reviewer password' -MaskInput) --project $apiProject
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

On macOS or Linux, use `npm` and slash-separated paths. GitHub Actions runs the .NET tests against a disposable PostgreSQL service and builds and tests Angular. A separate Docker job starts a fresh Compose stack and checks submission, reviewer approval, the customer timeline, notification read state after sign-in, and customer isolation.

The PostgreSQL integration tests require `DISPUTEDESK_TEST_POSTGRES` to contain a connection string for a test PostgreSQL server and a role that can create databases. The tests create and remove a uniquely named database. With the variable set, run `dotnet test .\DisputeDesk.slnx --configuration Release` from the root. Without it, the PostgreSQL tests are reported as skipped.

### Review the workflow

1. Sign in as `demo-naledi`. On a fresh database, the dashboard shows three transactions, including an expired Bookshop purchase, and zero disputes.
2. Open a transaction, choose **Dispute this transaction**, pick a reason, and submit. The first timeline entry should be **Submitted**. Return to **View all disputes** and sign out.
3. Sign in as `demo-reviewer`. Open the case from the review queue, choose **Start review**, and record an approval or rejection with a decision note. Return to the queue and check that the closed dispute is gone.
4. Sign out and sign back in as `demo-naledi`. The bell shows two unread updates, one for **Under review** and one for the decision. Open either update to mark it as read and see the dispute timeline. Refresh and check that the remaining unread count is one.
5. Open **View all disputes** and filter by the decision status and submission date. The matching row shows the transaction amount. Clear the filters to return to the whole list.
6. Submit a different transaction as a second dispute and withdraw it while it is **Submitted**. Confirm that the timeline records **Withdrawn**. Sign in as `demo-thabo` and verify that Naledi's disputes and notifications are absent.

### Browse transactions

The header shows the signed-in customer name, or the reviewer’s username. The transaction list supports search by description, merchant, or city, inclusive UTC dates, and an open or closed dispute window. Sort by date or amount and use **Previous** and **Next** to move through the results, ten at a time.

When demo generation is enabled, each batch includes three recent purchases and two purchases beyond the 120-day dispute window.

### Verify an expired transaction

1. Sign in as `demo-naledi`, open **View all transactions**, search for **Bookshop**, and choose **Apply**. The purchase is marked **Dispute window closed**.
2. Open it to see the filing deadline in UTC and the link to dispute history. Open a recent transaction to compare the available filing action.
3. If you already have a demo database and run without Docker, add the sample by rerunning the seed step from the repository root.

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   dotnet run --project .\DisputeDesk.Api --no-launch-profile -- --Database:Migrate=true --Database:SeedDemoData=true
   ```

   The demo seed keeps existing transactions and disputes. The .NET tests cover the exact deadline, rejection just after it, and reviewer actions on existing disputes after their transaction deadline.

### Verify lifecycle endpoints

In Development, use Swagger with a customer or reviewer session. Call `GET /api/auth/session` to verify the active role.

- Customers can call `POST /api/disputes/{id}/withdraw` with `{}` while their dispute is Submitted. New disputes can be filed up to and including 120 days after the recorded transaction time.
- Reviewers can open `GET /api/reviewer/disputes`, start review with `POST /api/reviewer/disputes/{id}/review` and `{}`, then submit a decision to `POST /api/reviewer/disputes/{id}/decision` with `{"status":"Approved","note":"Review outcome"}` or a Rejected status.
- Customers can check their timeline at `GET /api/disputes/{id}` and notifications at `GET /api/dispute-notifications`. Mark an update as read with `PUT /api/dispute-notifications/{id}/read`.

### Health and API errors

- `GET /health/live` returns HTTP 200 when the API can respond.
- `GET /health/ready` returns HTTP 200 when PostgreSQL is reachable and its migrations are current, or HTTP 503 when the API cannot serve database-backed requests. The Docker image uses this endpoint for its health check.
- API errors use Problem Details JSON for standard HTTP statuses. On an expired session, the portal takes the user back to sign in.

## Architecture

Angular organizes screens by feature. ASP.NET Core Identity issues a secure session cookie. The API takes the customer identity from that session and filters transaction and dispute queries by ownership. EF Core maps the domain to PostgreSQL. A unique database index prevents duplicates when two submissions target the same transaction. Dispute reasons are database reference data.

The Docker configuration is for local review. A deployment would supply its own TLS certificate, database credentials, and secret storage, with demo-account setup and transaction generation disabled.

# Browser acceptance test

This test starts the built web app on loopback, uses a unique SQLite database in the test output directory, and drives Chromium with Playwright. Accounts and passwords are generated for each run.

From `E:\Github\ChummerGen`:

```powershell
dotnet build .\ChummerGen.Tests\ChummerGen.Tests.csproj
pwsh .\ChummerGen.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
dotnet test .\ChummerGen.Tests\ChummerGen.Tests.csproj --no-build --filter FullyQualifiedName~BrowserWorkflowTests
```

The browser is required; the test does not skip when Chromium is missing. Re-run the install command after changing the Playwright package version or on a new machine.

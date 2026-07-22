# Start Here — Mosaic RimWorld 0.2 Pre-Alpha

This directory is intentionally separate from `../0.1-closure-candidate/`.

Do not replace, rename, or modify the 0.1 closure tree while validating 0.2.

First actions:

```powershell
python .	ools\static_verify.py
dotnet run --project .\Dagmay.Tests\Dagmay.Tests.csproj
dotnet run --project .\Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj
```

Then run the existing non-installing Windows build path against the owner's installed RimWorld
assemblies. Do not install the mod or alter a save until compilation and contract tests pass.

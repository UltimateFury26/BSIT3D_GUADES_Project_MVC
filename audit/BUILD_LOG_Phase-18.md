# BUILD LOG - Phase 18

| Check | Result |
|---|---|
| Debug build | NOT RUN - no MSBuild, Visual Studio, Mono or .NET SDK on PATH in the authoring environment |
| Release build | NOT RUN - same |
| NuGet restore | NOT RUN |
| Warnings/errors inspected | NOT RUN |

## Static checks that were run
| Check | Actual result |
|---|---|
| Source `.aspx` count / MVC `.cshtml` count | 25 / 36 |
| `.cs` and `.cshtml` files on disk (excluding Scripts, Content) not listed in `BSIT3D_GUADES_Project_MVC.csproj` | none |
| Changed `Web.config` is well-formed XML | yes |
| Release transform | `Web.Release.config` removes `compilation/@debug` |
| Application code/config referencing `Guades_QCUCram_DB` | none; only `QCU-CRAM_Sql/QCU-CRAM_CopyFromOldDb.sql` and docs |

## Commands for the developer (run from the solution root; save output under `audit/evidence/`)
```
nuget restore BSIT3D_GUADES_Project_MVC\BSIT3D_GUADES_Project_MVC.csproj -SolutionDirectory .
msbuild BSIT3D_GUADES_Project_MVC\BSIT3D_GUADES_Project_MVC.csproj /t:Rebuild /p:Configuration=Debug   /v:n /flp:logfile=audit\evidence\build-debug.log
msbuild BSIT3D_GUADES_Project_MVC\BSIT3D_GUADES_Project_MVC.csproj /t:Rebuild /p:Configuration=Release /v:n /flp:logfile=audit\evidence\build-release.log
```
Record: error count, warning count, and any warning that mentions a missing file or bundle. Overwrite this page with the real result.

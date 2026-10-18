# Third-Party Notices

ShikkhaDesk is distributed with the following third-party components. Each is licensed under its
own terms; nothing in the ShikkhaDesk licence restricts the rights those licences grant.

Generated from the `PackageReference` entries in every project file, so this list reflects what is
actually compiled into the product.

---

## Shipped with the application

| Component | Version | Licence |
|---|---|---|
| [Dapper](https://github.com/DapperLib/Dapper) | 2.1.35 | Apache License 2.0 |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) | 8.0.11 | MIT License |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | MIT License |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT License |
| Microsoft.Extensions.Logging | 8.0.1 | MIT License |
| Microsoft.Extensions.Logging.Abstractions | 8.0.2 | MIT License |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.1 | MIT License |
| Microsoft.Win32.Registry | 5.0.0 | MIT License |

`Microsoft.Data.Sqlite` bundles the native SQLite library. SQLite itself is in the **public
domain** — see <https://www.sqlite.org/copyright.html>.

## Used to build and test, not shipped

| Component | Version | Licence |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT License |
| [xunit](https://github.com/xunit/xunit) | 2.9.2 | Apache License 2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache License 2.0 |

## Runtime

The application is published **self-contained for `win-x64`**, so the .NET 8 runtime assemblies
are included in the installation. The .NET runtime and libraries are licensed under the
**MIT License** — see <https://github.com/dotnet/runtime/blob/main/LICENSE.TXT>.

## Fonts

No font is bundled with the application. Bengali text is rendered with whatever Bengali font the
Windows installation provides. See `docs/OPEN-DECISIONS.md` **D-6**.

## Tooling

| Tool | Use |
|---|---|
| [Inno Setup](https://jrsoftware.org/isinfo.php) | Builds the installer. Inno Setup is used to *produce* the installer; its own licence does not apply to the applications it packages. |
| Python + Pillow | Generates the branding assets (`scripts/generate-branding.py`). Not shipped. |

## No bundled third-party offers

The installer contains **no** adware, toolbar, browser extension, spyware, analytics agent, or
trial offer of any kind. There is no "recommended software" page, no checkbox pre-ticked on the
user's behalf, and no paid placement.

#!/usr/bin/env python3
"""Regenerate ShikkhaDesk.sln from the projects present in the tree."""
import pathlib, uuid, sys

root = pathlib.Path(__file__).resolve().parent.parent
projects = sorted(
    list(root.glob("src/*/*.csproj")) + list(root.glob("tests/*/*.csproj")),
    key=lambda p: str(p),
)
if not projects:
    print("no projects found", file=sys.stderr)
    sys.exit(1)

CSHARP = "9A19103F-16F7-4668-BE54-9A1E7A4F7556"
lines = [
    "Microsoft Visual Studio Solution File, Format Version 12.00",
    "# Visual Studio Version 17",
    "VisualStudioVersion = 17.0.31903.59",
    "MinimumVisualStudioVersion = 10.0.40219.1",
]

def pid(path: pathlib.Path) -> str:
    return "{" + str(uuid.uuid5(uuid.NAMESPACE_URL, "shikkhadesk/" + str(path))).upper() + "}"

for path in projects:
    rel = path.relative_to(root).as_posix().replace("/", "\\")
    name = path.stem
    lines.append(f'Project("{CSHARP}") = "{name}", "{rel}", "{pid(path)}"')
    lines.append("EndProject")

lines += [
    "Global",
    "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
    "\t\tDebug|Any CPU = Debug|Any CPU",
    "\t\tRelease|Any CPU = Release|Any CPU",
    "\tEndGlobalSection",
    "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
]
for path in projects:
    for cfg in ("Debug", "Release"):
        lines.append(f"\t\t{pid(path)}.{cfg}|Any CPU.ActiveCfg = {cfg}|Any CPU")
        lines.append(f"\t\t{pid(path)}.{cfg}|Any CPU.Build.0 = {cfg}|Any CPU")
lines += [
    "\tEndGlobalSection",
    "\tGlobalSection(SolutionProperties) = preSolution",
    "\t\tHideSolutionNode = FALSE",
    "\tEndGlobalSection",
    "\tGlobalSection(ExtensibilityGlobals) = postSolution",
    "\t\tSolutionGuid = {" + str(uuid.uuid5(uuid.NAMESPACE_URL, "shikkhadesk/solution")).upper() + "}",
    "\tEndGlobalSection",
    "EndGlobal",
]

(root / "ShikkhaDesk.sln").write_text("\r\n".join(lines) + "\r\n", encoding="utf-8")
print("solution with", len(projects), "projects")
for p in projects:
    print("  -", p.relative_to(root).as_posix())

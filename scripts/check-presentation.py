#!/usr/bin/env python3
"""Fail loudly when the WPF presentation layer disagrees with itself.

Three things have to line up, and a mismatch among them is invisible until the application is
already running:

1. every page template names a view model class that exists, and a view that exists;
2. every navigation key resolves to a view model in PresentationServices;
3. every view registered in PresentationServices has a page template.

Catching these here costs a second. Catching them in Windows CI costs ten minutes a round, and
catching them at a customer's desk costs the product's reputation.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WPF = ROOT / "src" / "ShikkhaDesk.Wpf"
VIEWS = WPF / "Views"
VIEWMODELS = WPF / "ViewModels"
NAV = WPF / "Infrastructure" / "NavigationCatalogue.cs"
PRESENTATION = WPF / "Infrastructure" / "PresentationServices.cs"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def declared_classes(source: str, kinds: tuple[str, ...]) -> set[str]:
    names: set[str] = set()
    for kind in kinds:
        for match in re.finditer(rf"\b{kind}\s+([A-Za-z_][\w]*)\b", source):
            names.add(match.group(1))
    return names


def main() -> int:
    problems: list[str] = []

    vm_sources = "\n".join(
        read(p) for p in sorted(VIEWMODELS.glob("*.cs")) + sorted((WPF / "Infrastructure").glob("*.cs"))
    )
    view_model_classes = declared_classes(vm_sources, ("class", "record", "sealed"))

    # Only user controls are rendered through a page template; windows are opened by name.
    def is_user_control(xaml: Path) -> bool:
        for line in read(xaml).splitlines():
            stripped = line.strip()
            if stripped.startswith("<") and not stripped.startswith("<!--") and not stripped.startswith("<?"):
                return stripped.startswith("<UserControl")
        return False

    view_xaml = {p.stem for p in VIEWS.glob("*.xaml") if is_user_control(p)}
    # A view counts as usable only when its code-behind exists too, otherwise InitializeComponent
    # has no partial to attach to.
    view_codebehind = {p.stem.replace(".xaml", "") for p in VIEWS.glob("*.xaml.cs")}
    usable_views = view_xaml & view_codebehind

    templates = read(VIEWS / "PageTemplates.xaml")
    for vm_name, view_name in re.findall(
        r'DataType="\{x:Type vm:(\w+)\}"[\s\S]{0,120}?<views:(\w+)\s*/>', templates
    ):
        if vm_name not in view_model_classes:
            problems.append(f"page template names {vm_name}, which is not a view model class")
        if view_name not in usable_views:
            problems.append(
                f"page template uses {view_name}, which is not a view with a code-behind"
            )

    # Every navigation key must be creatable, and everything creatable must be renderable.
    navigation = read(NAV)
    keys = re.findall(r'Key\s*=\s*"([a-z0-9]+)"', navigation) or re.findall(
        r'"([a-z0-9]+)"\s*,\s*"', navigation
    )

    presentation = read(PRESENTATION)
    mapped = dict(re.findall(r'"([a-z0-9]+)"\s*=>\s*provider\.GetRequiredService<(\w+)>', presentation))

    if keys:
        for key in keys:
            if key not in mapped:
                problems.append(f'navigation key "{key}" has no page in PresentationServices')

    for key, vm_name in mapped.items():
        if vm_name not in view_model_classes:
            problems.append(f'navigation key "{key}" maps to unknown view model {vm_name}')
        if f'DataType="{{x:Type vm:{vm_name}}}"' not in templates:
            problems.append(f"{vm_name} is creatable from the menu but has no page template")

    # Every view with a code-behind should be reachable from a template, or it is dead XAML.
    referenced = set(re.findall(r"<views:(\w+)\s*/>", templates))
    for view in sorted(usable_views - referenced - {"PageTemplates"}):
        problems.append(f"{view} exists but no page template renders it")

    if problems:
        print(f"{len(problems)} presentation problem(s):")
        for problem in problems:
            print("  -", problem)
        return 1

    print(
        f"presentation layer consistent: {len(keys) if keys else len(mapped)} navigation keys, "
        f"{len(usable_views)} views, {len(view_model_classes)} view models"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

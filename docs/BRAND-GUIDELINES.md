# ShikkhaDesk Brand Guidelines

The single source of truth for how ShikkhaDesk (শিক্ষা ডেস্ক) looks and reads. The WPF palette in
`src/ShikkhaDesk.Wpf/Themes/Colors.xaml` is generated from this document and must be kept in step
with it — that file already carries a pointer back here.

---

## 1. Name and voice

| | |
|---|---|
| Product name (Latin) | **ShikkhaDesk** — one word, capital S and D, never "Shikkha Desk" in product UI |
| Product name (Bengali) | **শিক্ষা ডেস্ক** |
| Tagline | **কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।** |
| Voice | Plain, respectful, second-person familiar (আপনি). Short sentences. Say what happened and what to do next. |

The tagline is a promise about scope — *complete management in one place* — so it is used on the
installer, the login screen and the About page only. It is not used as a marketing strapline on
reports, receipts or printed documents, where it would waste space and look unprofessional.

### Writing rules

* Money is never written as `1200.5`. It is `৳ ১,২০০.৫০` (or `৳ 1,200.50` when the user has chosen
  Latin digits in settings). The digit preference is a user setting, never a developer choice.
* Every error tells the user what to do. "অনুগ্রহ করে প্রথমে লগ ইন করুন।" is an error message;
  "Error 401" is not.
* Never address a user as "ব্যবহারকারী" in running text; use "আপনি".
* Dates in the UI follow the user's digit preference and `dd-MM-yyyy`, the convention across
  Bangladesh. ISO dates are for files and databases only.

---

## 2. Palette

Values are sRGB hex. The WPF theme stores them with a fully opaque alpha channel (`#FF…`).

| Role | Hex | WPF key | Used for |
|---|---|---|---|
| Primary navy | `#102A43` | `PrimaryNavy` | App chrome, side navigation, window header, primary buttons |
| Secondary navy | `#243B53` | `SecondaryNavy` | Hover and pressed states of primary navy; sub-headers |
| Primary teal | `#25C2A0` | `PrimaryTeal` | The single accent of the product: focus rings, active nav item, positive trend |
| Dark teal | `#138A72` | `DarkTeal` | Hover/pressed of teal; charts that need a second teal |
| Background | `#F5F7FA` | `AppBackground` | Page background behind every white card |
| Surface | `#FFFFFF` | `Surface` | Cards, grids, dialogs, inputs |
| Main text | `#172B4D` | `MainText` | Headings, amounts, anything that must be read first |
| Secondary text | `#627D98` | `SecondaryText` | Labels, hints, metadata, empty-state text |
| Border | `#D9E2EC` | `Border` | Card outlines, table rules, input borders |
| Success | `#16803C` | `Success` | Paid, posted, reconciled, present |
| Warning | `#B7791F` | `Warning` | Due soon, partially paid, unsaved changes |
| Error | `#C53030` | `Error` | Failed, reversed, absent, validation failures |
| Accent | `#FFB547` | `Accent` | The one attention colour: unread notices, the "due today" badge |

### Rules of use

* **Teal is an accent, not a theme.** It marks one thing on a screen — the active item, the primary
  action, the positive series. A screen with three teal elements has no accent at all.
* **Navy is structure.** Anything that frames content is navy; nothing that *is* content is.
* **Never encode meaning in colour alone.** Every status chip carries Bengali text as well as its
  colour, so the app still reads correctly on a projector, in greyscale print, or to a
  colour-blind user.
* **Accent (amber) appears at most once per screen.** It is reserved for the thing that needs
  attention now; using it decoratively destroys its meaning.
* Text on navy is `#FFFFFF`. Text on teal is `#102A43` (teal is too light for white body text and
  fails contrast at small sizes).

---

## 3. Typography

| Role | Font stack | Size |
|---|---|---|
| Default UI | `Nirmala UI, Vrinda, Kalpurush, Segoe UI, Arial` | 14 px body |
| Numbers and amounts | `Nirmala UI, Segoe UI, Consolas` | 14 px, right-aligned, tabular |
| Page title | default UI, semibold | 22 px |
| Section title | default UI, semibold | 15 px |
| Small / metadata | default UI | 12 px |

Nirmala UI is first because it ships with Windows 10 and 11 and shapes Bengali conjuncts correctly;
the fallbacks cover older or non-Windows builds. Amounts use the numeric stack so that columns of
figures align — a Bengali-digit column in a proportional face is unreadable.

The application never embeds a font it does not have a licence to redistribute. Fonts are resolved
from the operating system.

---

## 4. Logo

| Asset | Path | Use |
|---|---|---|
| Full colour mark + wordmark | `assets/branding/logo.svg` | Docs, web, the About page |
| Light variant (dark backgrounds) | `assets/branding/logo-light.svg` | Installer, splash, dark chrome |
| Dark variant (light backgrounds) | `assets/branding/logo-dark.svg` | Print, letterheads |
| Raster logo | `assets/branding/logo.png` (512 px) | Anywhere SVG is not accepted |
| Windows icon | `assets/branding/app-icon.ico` | The executable, the installer, the taskbar |

### Clear space and minimum size

* Keep clear space around the mark equal to the height of the "S" in the wordmark on all sides.
* Never reproduce the wordmark below 96 px wide, or the standalone mark below 24 px.
* Never stretch, rotate, outline, add a drop shadow to, or recolour the mark outside the palette
  above. On photography use the light variant at full white.

### The icon is a real icon

`app-icon.ico` is a genuine multi-resolution Windows ICO containing 16, 24, 32, 48, 64, 128, 256
and 512 px images — not a PNG renamed to `.ico`. It is produced by
`scripts/generate-branding.py`, which writes the ICO directory and image data directly. PNG copies
of every size live in `assets/branding/png/` for use on the web and in documentation.

---

## 5. Installer and packaging

| Asset | Path | Required geometry |
|---|---|---|
| Wizard image | `installer/windows/wizard-image.bmp` | 164 × 314 px, 24-bit BMP |
| Wizard small image | `installer/windows/wizard-small-image.bmp` | 55 × 58 px, 24-bit BMP |
| Setup icon | `assets/branding/app-icon.ico` | multi-size ICO |

Inno Setup 6 refuses to compile if the wizard bitmaps are not exactly those sizes, and only accepts
BMP — a PNG with the right pixel dimensions still fails. Both files are generated, not hand-edited.

The installer carries no bundled adware, toolbar, browser offer or third-party prompt of any kind.
This is a brand rule, not just an engineering one: a coaching centre trusting this program with its
accounts must never be sold to in the process.

---

## 6. What is not part of the brand

* No comic or decorative faces anywhere, including in reports.
* No gradients on cards or tables — flat surfaces only.
* No emoji as icons. Iconography is drawn or comes from a consistent icon set.
* No stock photography of students.

---

## 7. Regenerating the assets

```bash
python3 scripts/generate-branding.py
```

The script is deterministic: it rewrites `logo.svg`, `logo-light.svg`, `logo-dark.svg`, `logo.png`,
`app-icon.ico`, the PNG set in `assets/branding/png/`, `preview/branding-preview.png` and both
installer bitmaps. Run it from the repository root and commit the result together with any palette
change, so the assets and this document never drift apart.

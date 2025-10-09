# MarkdownToPDF

Convert one or more Markdown files into a styled, navigable PDF on Windows. MarkdownToPDF is a WinUI 3 desktop app built on .NET 9 that merges multiple markdown sources, provides a live preview, and exports a high?quality PDF with optional heading numbering, table of contents, page numbers, and PDF bookmarks.


## Features

- Multiple files input and ordering
  - Add one or more `.md`/`.markdown` files and define a custom order before export
  - Optional page breaks inserted between files
- Live PDF preview
  - Renders pages for a quick, accurate preview with progress feedback
  - Go to page, scroll-to-top, and a heading hierarchy side panel for navigation
- Page setup
  - Paper sizes: `A4`, `A3`, `Letter`
  - Portrait or landscape
  - Custom margins (mm) and option to print backgrounds
- Typography and markdown rendering
  - Base font family, font size, page body margins, and paragraph alignment
  - Markdig pipeline options: advanced extensions, pipe tables, auto-links
- Document structure
  - Optional heading numbering with customizable pattern (e.g., 1.1.1)
  - Optional Table of Contents (TOC) with configurable indentation, bullet style, and title
  - Place the TOC at the top or after the first file
- Page numbering
  - Turn on/off page numbers and choose their position (top/bottom, left/center/right)
  - Optionally hide page number on the first page
- PDF bookmarks (outline)
  - Injects a PDF outline based on detected headings and their resolved page numbers
- Theming
  - Light/Dark theme toggle


## How it works

1) Markdown processing
   - Markdown files are combined and processed by `Markdig`.
   - Optional heading numbering and a generated TOC are inserted.
2) HTML to PDF
   - `PuppeteerSharp` drives a headless Chromium to render the combined HTML into a PDF.
   - Page numbering templates are applied when enabled.
3) PDF post-process
   - Headings are resolved to page numbers and injected as PDF bookmarks via `PdfSharp`/`PdfPig`.
   - If configured, the footer on the first page is cleared to suppress the page number.
4) Preview
   - The Windows PDF APIs render page thumbnails for the in-app preview.


## Getting started (build and run)

**Prerequisites**
- Windows 11 or Windows 10, version 1809 (build 17763) or later
- .NET SDK 9
- Visual Studio 2022 version 17.12 or newer with the following workloads:
  - .NET Desktop Development
  - Windows App SDK / Windows 10 SDK (26100)

**Build**
- Clone the repository
- Open the solution in Visual Studio
- Restore NuGet packages
- Set `MarkdownToPDF` as startup project
- Run (F5)

**First run note**
- On first export/preview, `PuppeteerSharp` downloads a compatible Chromium. This requires Internet access and may take several minutes depending on the network.


## Usage

1) Add files
   - Click Add Files and select the markdown documents to include.
   - Reorder them if needed in the order dialog.
2) Adjust settings
   - Open Settings (gear icon) to customize page size, margins, fonts, numbering, TOC, and page numbers.
3) Preview
   - The preview pane shows rendered pages with a progress indicator while loading.
   - Use the page input box or the heading hierarchy to navigate.
4) Export
   - Click Export PDF and choose a destination `.pdf`.


## Configuration surface (key models)

- `FormattingOptions`
  - `UseAdvancedExtensions`, `UsePipeTables`, `UseAutoLinks`
  - `BaseFontFamily`, `BodyFontSizePx`, `BodyMarginPx`, `BodyTextAlignment`
  - `AddHeaderNumbering`, `HeaderNumberingPattern`
  - `AddTableOfContents`, `IndentTableOfContents`, `TableOfContentsBulletStyle`, `TableOfContentsHeaderText`, `TableOfContentsAfterFirstFile`
- `ExportOptions`
  - `PaperFormat` (`A4`, `A3`, `Letter`), `Landscape`, `PrintBackground`
  - `TopMarginMm`, `RightMarginMm`, `BottomMarginMm`, `LeftMarginMm`
  - `ShowPageNumbers`, `PageNumberPosition`, `ShowPageNumberOnFirstPage`


## Project structure (high level)

- Views
  - `WireframePage` (main UI with preview and navigation)
  - `SettingsDialog` (export/formatting options), `FileOrderDialog`, `HomeLandingPage`
- ViewModels
  - `WireframePageViewModel`, `MainViewModel`, `AppUpdateSettingViewModel`
- Services
  - `MarkdownService` (Markdig pipeline and HTML generation)
  - `PuppeteerPdfService` (HTML to PDF via PuppeteerSharp)
  - `MarkdownHeadingNumbering`, `MarkdownTableOfContentsGenerator`
  - `PdfOutlineWriter`, `PdfHeadingPageResolver`, `PdfFirstPageFooterRewriter`
- Models
  - `FormattingOptions`, `ExportOptions`, `MarkdownFileModel`, `HeadingInfo`
- App
  - `App` sets up dependency injection and theming (via DevWinUI helpers)


## Dependencies

- [Markdig] for Markdown parsing
- [PuppeteerSharp] for driving headless Chromium to generate the PDF
- [PDFsharp] and [PdfPig] for PDF outline and page analysis
- Windows App SDK / WinUI 3 for the desktop UI
- CommunityToolkit (MVVM and WinUI Controls)
- DevWinUI helpers for navigation, theming, and UI primitives
- `nucs.JsonSettings` for lightweight persisted settings

NuGet packages used (non-exhaustive):
- `Markdig`, `PuppeteerSharp`, `PDFsharp`, `PdfPig`
- `Microsoft.WindowsAppSDK`, `Microsoft.Windows.CsWinRT`, `Microsoft.Windows.SDK.BuildTools`
- `CommunityToolkit.Mvvm`, `CommunityToolkit.WinUI.Controls.*`
- `DevWinUI`, `DevWinUI.Controls`
- `nucs.JsonSettings`, `nucs.JsonSettings.AutoSaveGenerator`


## Limitations and notes

- Windows only (WinUI 3 / Windows App SDK).
- The first export/preview triggers a Chromium download managed by `PuppeteerSharp`.
- Rendering quality and CSS support depend on the embedded Chromium engine.
- Large documents and many images can increase memory and processing time.


## License

This project is licensed under the MIT License. See `LICENSE` for details.


## Acknowledgements

Thanks to the authors and maintainers of Markdig, PuppeteerSharp, PDFsharp, PdfPig, CommunityToolkit, DevWinUI, and the Windows App SDK.
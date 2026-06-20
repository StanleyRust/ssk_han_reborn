# Changelog

All notable changes to this project should be documented in this file.

## Unreleased
- Add exporter that writes a PO-style (gettext-like) file for translators:
  - Exports exact entries, single-placeholder templates, multi-placeholder templates, and long prefix-indexed entries.
  - File: Working/SskCnPoc/Exporter.cs
  - Usage: call Exporter.ExportToPo(outputPath) after loading translations (recommended output: Installer/Output/ssk_translations_export.po).
- Improve missing translation collector (already committed):
  - Better normalization of dynamic text and numeric placeholders to reduce duplicate missing entries.

## 2026-06-20
- Improve missing-collector: better dynamic text normalization and number-based placeholders (commit: Improve missing-collector).

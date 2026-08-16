# ADR 0002 — MIT license; no copyleft catalogue reuse

- Status: Accepted (2026-08-16)

## Context

The project is open source and intended for enterprise adoption, embedding and
forking. Several prior-art projects have tweak catalogues we would like to
learn from; their licenses differ (MIT: Win11Debloat, winutil, Sophia Script;
AGPL-3.0: privacy.sexy; CIS Benchmarks: proprietary content license; DoD
STIGs: public domain; Microsoft security baselines: freely usable).

## Decision

- The repository is licensed under **MIT** (see `LICENSE`).
- We may consult MIT-licensed catalogues and re-implement settings with
  attribution in `docs/`; we **must not copy** AGPL/GPL content (e.g.
  privacy.sexy YAML) or CIS benchmark text. STIG IDs, Microsoft baseline
  settings and Microsoft Learn documentation are the primary sources; every
  catalogue entry carries its own source URLs.

## Consequences

- Contributors must record the source of every tweak; the catalogue tests
  fail on entries without at least one source URL.
- CIS references are by section ID only, never by quoted text.

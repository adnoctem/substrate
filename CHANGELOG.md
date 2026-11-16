## [0.4.0](https://github.com/adnoctem/substrate/compare/v0.3.2...v0.4.0) (2026-11-16)

### Features

- **src:** add recovery divergence evidence and journal retirement ([33db544](https://github.com/adnoctem/substrate/commit/33db544c3de4e63541cbb615e6e6032f9c950171))

### Bug Fixes

- **src:** block recovery on installed-state divergence ([5fa6a59](https://github.com/adnoctem/substrate/commit/5fa6a5946cb8600218930014cfac4a189d7710c5))
- **src:** distinguish verification unknowns from recovery requirements ([39c50b5](https://github.com/adnoctem/substrate/commit/39c50b5b9091b1cdf3d8fb1492b6ad7bc68a7740))
- **src:** guard legacy Office storage paths ([65bdb33](https://github.com/adnoctem/substrate/commit/65bdb33afab107dc07530a2119b46e7ebd19bd06))
- **src:** rename Office journal root and media manifest ([0b0e60e](https://github.com/adnoctem/substrate/commit/0b0e60eaf6f671755b4e628d3865bca7de0e3dce))
- **src:** standardize winkit machine paths and runtime names ([ab7219b](https://github.com/adnoctem/substrate/commit/ab7219b495f662c11bd7b65c586760af13c80277))
- **src:** tolerate language-neutral Office MSI product codes ([e3e44eb](https://github.com/adnoctem/substrate/commit/e3e44eb2dfd86e79c43f37d1c2952e3655bfad41))
- **src:** tolerate unreadable Outlook profile stores and accounts ([dcff86e](https://github.com/adnoctem/substrate/commit/dcff86e4ac6b9dcf34facda80c1802df05c1450b))
- **src:** treat absent Click-to-Run ExcludedApps as an empty set ([b633385](https://github.com/adnoctem/substrate/commit/b6333852314b6c1460f4bc25510cfa8055c223a3))
- **tests:** normalize default operation log path assertions ([d01d4a3](https://github.com/adnoctem/substrate/commit/d01d4a313d9a206875e2ee86cd285f2fecb72015))

## [0.3.2](https://github.com/adnoctem/substrate/compare/v0.3.1...v0.3.2) (2026-11-10)

### Bug Fixes

- **tools:** ignore recased Microsoft download links ([641d683](https://github.com/adnoctem/substrate/commit/641d6835af4b1e48f46bc8b35ca0df41196ceeac))
- **tools:** tolerate PlatyPS CommonParameters syntax drift ([bb663e2](https://github.com/adnoctem/substrate/commit/bb663e2e47523e8557b7886b6181d0c628627adf))

## [0.3.1](https://github.com/adnoctem/substrate/compare/v0.3.0...v0.3.1) (2026-11-03)

### Bug Fixes

- **src:** link plain HTTP URLs in generated releases ([5ccbf9b](https://github.com/adnoctem/substrate/commit/5ccbf9b9769598721e2931007ea69615e4c049e8))
- **tests:** cover plain URL auto-linking ([29d8922](https://github.com/adnoctem/substrate/commit/29d892283d17ef15ad33614b36f2c86baef6c16e))

## [0.3.0](https://github.com/adnoctem/substrate/compare/v0.2.1...v0.3.0) (2026-11-02)

### Features

- **src:** build press-release authoring templates ([5e37f8b](https://github.com/adnoctem/substrate/commit/5e37f8bccb43f843edbaca9dc508cc3ab3066477))
- **src:** complete DOCX styling and pack authoring assets ([e25fd31](https://github.com/adnoctem/substrate/commit/e25fd31ca80e12dc8d1b98edc3652dea9bce22e0))
- **src:** extract press releases from structured Word documents ([cbd858a](https://github.com/adnoctem/substrate/commit/cbd858a9f87858ab5b8704855155b410fa987b60))
- **src:** generate press release DOCX from tagged layouts ([368a975](https://github.com/adnoctem/substrate/commit/368a975f2723cc569910df68bf76828a3654a381))
- **src:** load template packs and generate press release HTML ([07a4227](https://github.com/adnoctem/substrate/commit/07a422786d74bdd064eedb33151fb42d3702e454))
- **tools:** expose document generation types in PowerShell 7 ([7403dfa](https://github.com/adnoctem/substrate/commit/7403dfaa7b19805be627268fdf68448eea2f6e75))

### Bug Fixes

- **tests:** make wait-for-exit cancellation deterministic ([14db06a](https://github.com/adnoctem/substrate/commit/14db06ac8ec439520fb97605d8f28be218d8dad8))

## [0.2.1](https://github.com/adnoctem/substrate/compare/v0.2.0...v0.2.1) (2026-10-29)

### Bug Fixes

- **src:** resolve delivery stores on clients that cannot report them ([056bc18](https://github.com/adnoctem/substrate/commit/056bc186dfc86eae991bf4dc9b6e2d1f3a13fd95))
- **tools:** drain probe output without the thread pool ([9ecfc0c](https://github.com/adnoctem/substrate/commit/9ecfc0c3febf74fe369604a414f0ac620776ea97))

## [0.2.0](https://github.com/adnoctem/substrate/compare/v0.1.1...v0.2.0) (2026-10-28)

### Features

- **src:** read Outlook accounts and delivery stores ([c3474bf](https://github.com/adnoctem/substrate/commit/c3474bfb3af9689a4c3ff13703c74dc58a03e63a))

## [0.1.1](https://github.com/adnoctem/substrate/compare/v0.1.0...v0.1.1) (2026-10-11)

### Bug Fixes

- **src:** preserve result collections during log parameter binding ([263d033](https://github.com/adnoctem/substrate/commit/263d033df82c0f961ec3eaccb936c954e1e4fd5f))

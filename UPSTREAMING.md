# Reviewing LLE changes

[Feature groups](docs/FEATURE_CLUSTERS.md) ·
[Commit inventory](docs/COMMITS.md) · [Attribution](ATTRIBUTION.md)

The comparison baseline is upstream `v1.2.5.0`
(`4c58e1969ea39b35f21754e4f81e4e3baed5f8ca`).

```sh
git fetch https://github.com/beeradmoore/dlss-swapper.git tag v1.2.5.0
git diff --stat v1.2.5.0..HEAD
git diff v1.2.5.0..HEAD
git log --reverse --oneline v1.2.5.0..HEAD
```

To reuse a feature, fetch this repository into an upstream checkout and
cherry-pick its commits in the order listed in the feature group. Include earlier
shared-code commits when the selected feature depends on them:

```sh
git remote add lle https://github.com/ordinarybob/dlss-swapper-lle.git
git fetch lle main
git cherry-pick <commit> [<next-commit> ...]
```

For batch workflows, also review RafaelHGOliveira's
[PR #913](https://github.com/beeradmoore/dlss-swapper/pull/913), whose selection
and batch-update foundation LLE incorporates.

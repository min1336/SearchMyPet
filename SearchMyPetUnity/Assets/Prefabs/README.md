# Wall placement prefab contract

`ChameleonWallPlacementTarget.prefab` is a replaceable placeholder character for the wall-placement MVP.

- Root `+Y`: character up
- Root `+Z`: character front, facing away from the wall toward the viewer
- Root origin: wall-contact center
- Normalized prefab height: approximately `1` Unity unit
- Runtime height: `WallPlacementConfig.characterHeightMeters` (`0.18 m` by default)
- Wall separation: `WallPlacementConfig.wallOffsetMeters` (`0.02 m` by default)

When replacing the placeholder with final art, keep this root-axis, pivot, and normalized-height contract. Child mesh axes may differ if an intermediate child transform corrects them.

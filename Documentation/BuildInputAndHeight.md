# Construction pointer navigation and placement levels

- BuildMenuView disables Selectable navigation only inside the construction/tablet view. Dynamically generated catalogue/property buttons also use Navigation.None. Stale EventSystem focus inside that view is cleared; other menus retain their focus. Pointer hover and click behavior remain enabled.
- Floor surface selection resolves an aimed wall or slab before falling back to the player's floor plane. Upper-half wall targeting selects the wall's top storey, lower-half targeting its base. Slab undersides select the corresponding walkable top. Pointer X/Z is preserved instead of replacing it with the wall pivot. Height survives grid snapping.
- Native tests confirm a sold staircase is valid again at its exact original pose and can reuse its hall. To compensate for preview rounding versus authored visual-bounds origins, stair placement snaps to a sold stair's pivot within one metre, only on the same level and with the same orientation. Collision validation still applies; R rotation and placement on other storeys are not overridden.

Verification: BuildInputAndHeightValidation.Run exercises construction UI events and real physics surface hits, then runs sold-stair placement/save-load tests and construction/UI/animation regressions. Reports: BuildInputAndHeightValidation.txt and .log, FloorFoundationValidation.txt.

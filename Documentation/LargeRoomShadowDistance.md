# Sunlight in the opposite corner of a closed plot-sized room

The PC URP asset limited realtime directional shadows to 50 units. A 44 x 44 room has a 62.2-unit diagonal, before accounting for camera offset and shadow fading. The opposite corner therefore lost the ceiling's shadow, not the ceiling geometry itself.

An isolated render test uses 44 x 44 authored wall prefabs, a full generated RoomFloorPatch roof, four corner cameras, and a directional sun. Each lit image is compared against its identical sun-disabled baseline. With 50 units the same bright wedge is reproduced. With 100 units, aggregate mean excess brightness falls from 0.036377 to 0.000270 in the initial comparison. This is a reduction of about 99.3%, not a claim that every subpixel contact artefact is eliminated.

PC_RPAsset now uses 100-unit shadows, retaining four cascades and its existing 4096 shadow atlas. The first three split fractions are halved so their physical boundaries remain at approximately 6.15, 14.63 and 26.8 units, preserving near-camera shadow allocation. Only the last cascade extends farther. This increases the shadow coverage/caster cost; no additional shadow atlas memory is requested. Mobile settings, materials, meshes, scene objects and bias settings are unchanged.

Test: LargeRoomShadowDistanceValidation.Run. Captures and measurements are under Documentation/LargeRoomShadowDistance. Test scenes and cloned pipeline settings are discarded; the source gameplay scene is not saved or edited.

This is a finite distance suitable for the current plot, not a guarantee of shadowed interiors at arbitrary viewing distances or larger future buildings.

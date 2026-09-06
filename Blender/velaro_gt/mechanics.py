"""Low-poly wheels and a fictional, serviceable-looking engine for Velaro GT.

The builder uses the main scene's helpers and materials.  All wheel geometry is
parented to a named wheel pivot, with the axle along X.  Coordinates are metres.
"""

import math

import bpy


def build_mechanics(api):
    mesh = api["mesh"]
    box = api["box"]
    cylinder = api["cylinder"]
    tube = api["tube"]
    mats = api["mats"]
    created = []
    wheel_roots = {}

    def keep(obj):
        if obj is not None:
            created.append(obj)
        return obj

    def wheel_lathe(name, centre, side, profile, material, segments=32):
        """Closed radial cross-section; profile coordinates are outward X/radius."""
        verts = []
        for axial, radius in profile:
            for index in range(segments):
                angle = 2.0 * math.pi * index / segments
                verts.append((centre[0] + side * axial,
                              centre[1] + math.sin(angle) * radius,
                              centre[2] + math.cos(angle) * radius))
        faces = []
        for ring in range(len(profile)):
            nxt = (ring + 1) % len(profile)
            for index in range(segments):
                k = (index + 1) % segments
                face = (ring * segments + index, ring * segments + k,
                        nxt * segments + k, nxt * segments + index)
                faces.append(tuple(reversed(face)) if side > 0 else face)
        obj = keep(mesh(name, verts, faces, material))
        # Smooth circumferential shading keeps the modest radial resolution
        # unobtrusive. Profile creases remain an intentional stylised treatment.
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        return obj

    def spoke_set(label, centre, side):
        verts, faces = [], []
        for sector in range(5):
            for branch in (-1, 1):
                base = sector * 2.0 * math.pi / 5.0
                inner_angle = base + branch * 0.075
                outer_angle = base + branch * 0.22 + 0.10
                inner_r, outer_r = 0.059, 0.249
                # Each spoke is a tapered prism, slightly swept around the hub.
                inner_yz = (math.sin(inner_angle) * inner_r,
                            math.cos(inner_angle) * inner_r)
                outer_yz = (math.sin(outer_angle) * outer_r,
                            math.cos(outer_angle) * outer_r)
                inner_tan = (math.cos(inner_angle), -math.sin(inner_angle))
                outer_tan = (math.cos(outer_angle), -math.sin(outer_angle))
                yz = [(inner_yz[0] - inner_tan[0] * 0.011,
                       inner_yz[1] - inner_tan[1] * 0.011),
                      (outer_yz[0] - outer_tan[0] * 0.009,
                       outer_yz[1] - outer_tan[1] * 0.009),
                      (outer_yz[0] + outer_tan[0] * 0.009,
                       outer_yz[1] + outer_tan[1] * 0.009),
                      (inner_yz[0] + inner_tan[0] * 0.011,
                       inner_yz[1] + inner_tan[1] * 0.011)]
                first = len(verts)
                for axial in (0.071, 0.096):
                    for y, z in yz:
                        verts.append((centre[0] + side * axial,
                                      centre[1] + y, centre[2] + z))
                prism = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                         (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
                for face in prism:
                    face = tuple(first + index for index in face)
                    faces.append(tuple(reversed(face)) if side > 0 else face)
        return keep(mesh(label + "_TwinSpokes", verts, faces, mats["metal"]))

    tire_profile = [(-0.099, 0.247), (-0.117, 0.278), (-0.117, 0.318),
                    (-0.090, 0.347), (-0.054, 0.355), (0.054, 0.355),
                    (0.090, 0.347), (0.117, 0.318), (0.117, 0.278),
                    (0.099, 0.247)]
    for label, side, y in (("FL", -1, -1.40), ("FR", 1, -1.40),
                           ("RL", -1, 1.45), ("RR", 1, 1.45)):
        centre = (0.90 * side, y, 0.36)
        pivot = bpy.data.objects.new("Wheel_" + label, None)
        bpy.context.collection.objects.link(pivot)
        pivot.location = centre
        pivot.empty_display_type = "PLAIN_AXES"
        pivot.empty_display_size = 0.17
        pivot["component"] = "wheel"
        pivot["spin_axis"] = "LOCAL_X"
        pivot["radius_m"] = 0.355
        pivot["steerable"] = label.startswith("F")
        wheel_roots[label] = pivot
        created.append(pivot)
        before = set(bpy.data.objects)

        wheel_lathe(label + "_Tire", centre, side, tire_profile, mats["rubber"])
        wheel_lathe(label + "_RimBarrel", centre, side,
                    [(-0.092, 0.232), (-0.092, 0.255),
                     (0.096, 0.255), (0.096, 0.232)], mats["dark"], segments=24)
        wheel_lathe(label + "_PolishedRim", centre, side,
                    [(0.092, 0.232), (0.092, 0.258),
                     (0.105, 0.258), (0.105, 0.232)], mats["metal"], segments=24)
        # Subtle sidewall moulding gives the tire a readable bead and shoulder.
        wheel_lathe(label + "_SidewallBead", centre, side,
                    [(0.116, 0.283), (0.116, 0.294),
                     (0.119, 0.292), (0.119, 0.285)], mats["rubber"], segments=16)
        keep(cylinder(label + "_BrakeDisc",
                      (centre[0] + side * 0.037, y, centre[2]),
                      0.208, 0.018, mats["metal"], vertices=32, axis="X"))
        keep(cylinder(label + "_DiscBell",
                      (centre[0] + side * 0.050, y, centre[2]),
                      0.102, 0.014, mats["dark"], vertices=16, axis="X"))
        # A few black indents suggest ventilated rotor holes without booleans.
        vent_verts, vent_faces = [], []
        for index in range(10):
            angle = index * math.tau / 10
            first = len(vent_verts)
            for vertex in range(6):
                phi = vertex * math.tau / 6
                vent_verts.append((centre[0] + side * 0.0463,
                                   y + math.sin(angle) * 0.178 + math.sin(phi) * 0.006,
                                   centre[2] + math.cos(angle) * 0.178 + math.cos(phi) * 0.006))
            face = tuple(range(first, first + 6))
            vent_faces.append(tuple(reversed(face)) if side > 0 else face)
        keep(mesh(label + "_DiscVents", vent_verts, vent_faces, mats["dark"]))
        keep(box(label + "_BrakeCaliper",
                 (centre[0] + side * 0.045, y + 0.160, centre[2] + 0.025),
                 (0.075, 0.075, 0.155), mats["red"], bevel=0.008))
        spoke_set(label, centre, side)
        keep(cylinder(label + "_HubCap",
                      (centre[0] + side * 0.095, y, centre[2]),
                      0.057, 0.025, mats["dark"], vertices=16, axis="X"))
        keep(cylinder(label + "_HubBadge",
                      (centre[0] + side * 0.110, y, centre[2]),
                      0.028, 0.005, mats["metal"], vertices=12, axis="X"))
        for index in range(5):
            angle = index * math.tau / 5.0
            keep(cylinder(label + "_Lug_%02d" % index,
                          (centre[0] + side * 0.111,
                           y + math.sin(angle) * 0.043,
                           centre[2] + math.cos(angle) * 0.043),
                          0.008, 0.008, mats["metal"], vertices=6, axis="X"))
        bpy.context.view_layer.update()
        for obj in set(bpy.data.objects) - before:
            world = obj.matrix_world.copy()
            obj.parent = pivot
            obj.matrix_world = world

    engine_start = set(bpy.data.objects)
    engine_pivot = bpy.data.objects.new("Engine_Assembly", None)
    bpy.context.collection.objects.link(engine_pivot)
    engine_pivot.empty_display_size = 0.13
    engine_pivot.location = (0.0, -1.34, 0.46)
    engine_pivot["component"] = "fictional_engine"
    created.append(engine_pivot)

    keep(box("Engine_Crankcase", (0.0, -1.34, 0.470),
             (0.59, 0.72, 0.245), mats["metal"], bevel=0.025))
    keep(box("Engine_OilPan", (0.0, -1.34, 0.360),
             (0.40, 0.54, 0.065), mats["dark"], bevel=0.02))
    for side, bank in ((-1, "L"), (1, "R")):
        bank_obj = keep(box("Engine_CylinderBank_" + bank,
                            (side * 0.188, -1.33, 0.609),
                            (0.236, 0.72, 0.175), mats["dark"], bevel=0.018))
        bank_obj.rotation_euler[1] = side * 0.20
        cover = keep(box("Engine_BlueCamCover_" + bank,
                         (side * 0.212, -1.33, 0.705),
                         (0.229, 0.725, 0.062), mats["paint"], bevel=0.020))
        cover.rotation_euler[1] = side * 0.12
        keep(box("Engine_CoverStrip_" + bank,
                 (side * 0.208, -1.33, 0.742),
                 (0.073, 0.570, 0.009), mats["metal"], bevel=0.004))
        for index in range(4):
            keep(box("Engine_CoverRib_%s_%d" % (bank, index),
                     (side * 0.205, -1.53 + 0.13 * index, 0.749),
                     (0.17, 0.017, 0.008), mats["metal"], bevel=0.003))
        for index in range(3):
            rear_y = -1.57 + index * 0.235
            keep(tube("Engine_ExhaustRunner_%s_%d" % (bank, index),
                      [(side * 0.28, rear_y, 0.585),
                       (side * 0.365, rear_y, 0.55),
                       (side * 0.407, rear_y + 0.025, 0.47),
                       (side * 0.33, rear_y + 0.085, 0.415)],
                      0.022, mats["metal"], sides=6))
        keep(tube("Engine_FuelRail_" + bank,
                  [(side * 0.102, -1.68, 0.686),
                   (side * 0.102, -0.995, 0.686)],
                  0.014, mats["metal"], sides=8))
    keep(box("Engine_IntakePlenum", (0.0, -1.34, 0.704),
             (0.130, 0.48, 0.099), mats["dark"], bevel=0.025))
    keep(tube("Engine_AirIntake", [(0.0, -1.58, 0.707),
                                  (0.0, -1.79, 0.696),
                                  (-0.31, -1.825, 0.633),
                                  (-0.42, -1.83, 0.613)],
              0.045, mats["rubber"], sides=10))
    keep(box("Engine_Airbox", (-0.425, -1.80, 0.594),
             (0.27, 0.28, 0.175), mats["dark"], bevel=0.025))
    for index in range(5):
        keep(box("Engine_AirboxFin_%d" % index,
                 (-0.50 + index * 0.036, -1.80, 0.685),
                 (0.010, 0.205, 0.009), mats["metal"], bevel=0.003))

    keep(box("Engine_RadiatorFrame", (0.0, -2.005, 0.546),
             (1.08, 0.092, 0.357), mats["dark"], bevel=0.012))
    keep(box("Engine_RadiatorCore", (0.0, -2.061, 0.546),
             (0.96, 0.016, 0.292), mats["metal"], bevel=0.004))
    for index in range(13):
        keep(box("Engine_RadiatorFin_%02d" % index,
                 (-0.443 + index * 0.074, -2.073, 0.546),
                 (0.015, 0.009, 0.272), mats["dark"], bevel=0.001))
    keep(tube("Engine_CoolantHose", [(0.42, -1.990, 0.654),
                                     (0.44, -1.80, 0.70),
                                     (0.35, -1.69, 0.678),
                                     (0.28, -1.69, 0.634)],
              0.025, mats["rubber"], sides=8))
    keep(box("Engine_Battery", (0.444, -0.928, 0.525),
             (0.239, 0.28, 0.218), mats["dark"], bevel=0.014))
    keep(box("Engine_BatteryTop", (0.444, -0.928, 0.637),
             (0.242, 0.28, 0.025), mats["metal"], bevel=0.006))
    for side in (-1, 1):
        keep(box("Engine_BatteryTerminal_%s" % side,
                 (0.444 + side * 0.075, -0.875, 0.662),
                 (0.038, 0.051, 0.030), mats["red"] if side == 1 else mats["dark"],
                 bevel=0.007))
    keep(tube("Engine_BatteryLead", [(0.52, -0.875, 0.67),
                                    (0.55, -1.07, 0.67),
                                    (0.38, -1.17, 0.60)],
              0.011, mats["red"], sides=6))
    keep(box("Engine_ExpansionTank", (-0.458, -0.923, 0.547),
             (0.23, 0.25, 0.195), mats["metal"], bevel=0.035))
    keep(cylinder("Engine_ExpansionTankCap", (-0.458, -0.91, 0.66),
                  0.038, 0.025, mats["dark"], vertices=12, axis="Z"))
    keep(tube("Engine_TankHose", [(-0.465, -1.035, 0.592),
                                 (-0.48, -1.25, 0.56),
                                 (-0.39, -1.49, 0.49)],
              0.016, mats["rubber"], sides=6))
    keep(box("Engine_FuseBox", (0.455, -1.76, 0.493),
             (0.22, 0.275, 0.136), mats["dark"], bevel=0.010))
    keep(cylinder("Engine_OilFillerCap", (0.221, -1.584, 0.758),
                  0.034, 0.016, mats["dark"], vertices=12, axis="Z"))

    bpy.context.view_layer.update()
    for obj in set(bpy.data.objects) - engine_start:
        if obj == engine_pivot:
            continue
        world = obj.matrix_world.copy()
        obj.parent = engine_pivot
        obj.matrix_world = world

    return {"objects": created, "wheels": wheel_roots, "engine": engine_pivot}

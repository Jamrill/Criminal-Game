"""Compact, texture-free cabin for the Velaro GT.

The host provides geometry helpers through build_interior(api).  Coordinates
are metres: X across the car, -Y forward and Z up.  This module deliberately
contains no scene, collection or export setup.
"""

from math import cos, sin, pi


def build_interior(api):
    mesh = api["mesh"]
    box = api["box"]
    cylinder = api["cylinder"]
    tube = api["tube"]
    mats = api["mats"]
    created = []

    def keep(obj):
        created.append(obj)
        return obj

    def block(name, loc, size, material, bevel=0.0):
        return keep(box(name, loc, size, mats[material], bevel=bevel))

    def pipe(name, points, radius, material, sides=6):
        return keep(tube(name, points, radius, mats[material], sides=sides))

    def round_sections(name, sections, material):
        """An eight-sided rounded rectangular cross section at each height.

        Each section is (x, y, z, half_width, half_depth, corner).  Small
        changes between sections create shaped upholstery without subdivision.
        """
        verts = []
        for x, y, z, w, d, r in sections:
            ring = [(-w+r, -d), (w-r, -d), (w, -d+r), (w, d-r),
                    (w-r, d), (-w+r, d), (-w, d-r), (-w, -d+r)]
            verts.extend((x+px, y+py, z) for px, py in ring)
        faces = [tuple(reversed(range(8)))]
        for level in range(len(sections)-1):
            a = level*8
            b = a+8
            faces.extend((a+i, a+(i+1)%8, b+(i+1)%8, b+i)
                         for i in range(8))
        faces.append(tuple(range(len(verts)-8, len(verts))))
        return keep(mesh(name, verts, faces, mats[material]))

    # Recessed charcoal floor, soft mats and a raised central tunnel.
    block("Interior_Floor", (0, .37, .315), (1.43, 2.01, .05), "dark", .012)
    for side, x in (("L", -.405), ("R", .405)):
        block("Floor_Mat_"+side, (x, -.18, .346), (.405, .58, .014), "rubber", .025)
    block("Transmission_Tunnel", (0, .22, .395), (.205, 1.64, .16), "dark", .035)

    # Both bucket seats are separate recognisable upholstered forms, with
    # inset cushions, raised thigh bolsters and a gently reclining backrest.
    for side, x in (("L", -.405), ("R", .405)):
        prefix = "Front_Seat_"+side+"_"
        for dx in (-.112, .112):
            block(prefix+"Rail", (x+dx, .29, .365), (.026, .42, .048), "metal", .005)
        round_sections(prefix+"Cushion", [
            (x, .295, .389, .16, .187, .03),
            (x, .29, .427, .188, .213, .045),
            (x, .275, .493, .18, .204, .047),
            (x, .271, .505, .157, .178, .046),
        ], "leather")
        round_sections(prefix+"Seat_Insert", [
            (x, .265, .494, .113, .159, .026),
            (x, .266, .51, .108, .155, .028),
        ], "dark")
        for direction in (-1, 1):
            bx = x+direction*.150
            round_sections(prefix+"Thigh_Bolster", [
                (bx, .280, .466, .036, .169, .020),
                (bx, .302, .535, .042, .142, .024),
                (bx, .316, .548, .024, .122, .018),
            ], "leather")
        round_sections(prefix+"Back_Shell", [
            (x, .509, .450, .165, .052, .029),
            (x, .565, .654, .195, .066, .037),
            (x, .619, .884, .183, .059, .033),
            (x, .654, 1.026, .135, .043, .026),
        ], "leather")
        round_sections(prefix+"Back_Insert", [
            (x, .466, .541, .105, .019, .012),
            (x, .500, .681, .122, .024, .016),
            (x, .552, .884, .113, .020, .014),
            (x, .606, .992, .094, .014, .012),
        ], "dark")
        for direction in (-1, 1):
            round_sections(prefix+"Back_Bolster", [
                (x+direction*.143, .489, .510, .033, .043, .023),
                (x+direction*.160, .526, .681, .040, .061, .028),
                (x+direction*.145, .584, .894, .036, .046, .025),
                (x+direction*.109, .633, 1.010, .024, .027, .014),
            ], "leather")
        for dx in (-.056, .056):
            keep(cylinder(prefix+"Headrest_Post", (x+dx, .664, 1.048),
                          .009, .078, mats["metal"], vertices=8, axis="Z"))
        round_sections(prefix+"Headrest", [
            (x, .663, 1.049, .084, .041, .018),
            (x, .670, 1.075, .102, .055, .028),
            (x, .678, 1.152, .099, .048, .024),
            (x, .681, 1.172, .077, .031, .020),
        ], "leather")
        # Thin trim follows the visible insert edges; no texture dependency.
        for dx in (-.099, .099):
            pipe(prefix+"Back_Piping", [
                (x+dx, .443, .552), (x+dx, .472, .682),
                (x+dx, .529, .881), (x+dx*.86, .590, .984),
            ], .0028, "leather", sides=4)
        for yy in (.186, .257, .327):
            pipe(prefix+"Cushion_Seam", [(x-.094, yy, .512), (x+.094, yy, .512)],
                 .0018, "leather", sides=4)
        block(prefix+"Belt_Buckle", (x-(.202 if x>0 else -.202), .452, .478),
              (.028, .041, .057), "dark", .007)
        block(prefix+"Buckle_Release", (x-(.202 if x>0 else -.202), .451, .508),
              (.020, .024, .008), "red", .003)

    # Small rear 2+2 seats fit below the sloping rear window.
    block("Rear_Seat_Base", (0, 1.002, .399), (1.26, .38, .13), "dark", .032)
    for side, x in (("L", -.343), ("R", .343)):
        prefix = "Rear_Seat_"+side+"_"
        round_sections(prefix+"Cushion", [
            (x, 1.007, .437, .263, .175, .034),
            (x, 1.001, .490, .258, .164, .042),
            (x, 1.003, .501, .227, .133, .039),
        ], "leather")
        block(prefix+"Inset", (x, .987, .502), (.306, .241, .019), "dark", .022)
        round_sections(prefix+"Back", [
            (x, 1.161, .471, .26, .052, .03),
            (x, 1.204, .725, .252, .055, .03),
            (x, 1.239, .889, .209, .047, .027),
        ], "leather")
        round_sections(prefix+"Back_Insert", [
            (x, 1.104, .519, .166, .014, .012),
            (x, 1.147, .724, .161, .018, .014),
            (x, 1.184, .853, .131, .016, .013),
        ], "dark")
        block(prefix+"Headrest", (x, 1.260, .923), (.221, .098, .097), "leather", .026)
    block("Rear_Center_Armrest", (0, 1.147, .666), (.110, .131, .345), "dark", .023)
    block("Rear_Parcel_Shelf", (0, 1.324, .863), (1.30, .236, .045), "dark", .025)

    # The dash is a shaped extrusion; the outer ends follow the coupe body.
    dash_vertices = []
    for x in (-.738, -.52, .52, .738):
        setback = .045 if abs(x)>.70 else 0.0
        dash_vertices.extend([
            (x, -.585+setback, .855), (x, -.331+setback, .817),
            (x, -.281+setback, .698), (x, -.550+setback, .669),
        ])
    dash_faces = [(0, 1, 2, 3), (15, 14, 13, 12)]
    for section in range(3):
        a = section*4
        dash_faces.extend((a+(i+1)%4, a+i, a+4+i, a+4+(i+1)%4) for i in range(4))
    keep(mesh("Dashboard_Sculpted", dash_vertices, dash_faces, mats["dark"]))
    pipe("Dashboard_Tan_Edge", [(-.714, -.285, .729), (-.52, -.324, .735),
                                (0, -.324, .735), (.52, -.324, .735),
                                (.714, -.285, .729)], .012, "leather", sides=6)
    # Four slatted air outlets create readable detail on the front dash face.
    for i, vx in enumerate((-.647, -.210, .210, .647)):
        vy = -.294 if abs(vx)>.60 else -.328
        block("Dashboard_Vent_Frame_%s"%i, (vx, vy, .779), (.121, .022, .050), "metal", .006)
        block("Dashboard_Vent_Recess_%s"%i, (vx, vy+.013, .779), (.106, .013, .037), "rubber", .003)
        for dz in (-.010, 0, .010):
            block("Dashboard_Vent_Slat_%s"%i, (vx, vy+.022, .779+dz),
                  (.097, .006, .003), "dark", .001)

    # Instrument pod and two small dial faces behind the steering wheel.
    block("Instrument_Binnacle", (-.405, -.338, .871), (.285, .151, .099), "dark", .024)
    block("Instrument_Cluster_Face", (-.405, -.257, .866), (.244, .018, .067), "screen", .011)
    for gx in (-.472, -.338):
        points = [(gx+.026*cos(t*2*pi/12), -.243, .866+.026*sin(t*2*pi/12))
                  for t in range(13)]
        pipe("Instrument_Dial_Rim", points, .0028, "metal", sides=4)
        pipe("Instrument_Needle", [(gx, -.239, .865), (gx+.012, -.239, .881)],
             .0019, "red", sides=4)
    block("Center_Display_Frame", (0, -.267, .847), (.281, .044, .137), "dark", .013)
    block("Center_Display_Glass", (0, -.241, .849), (.254, .009, .109), "screen", .005)
    for j, width in enumerate((.074, .136, .106)):
        block("Display_UI_Line_%s"%j, (-.034+width*.10, -.234, .875-j*.025),
              (width, .003, .004), "metal", .001)

    # Wheel: 20 segments keep the silhouette smooth at game distances.
    steering_center = (-.405, -.079, .870)
    sx, sy, sz = steering_center
    keep(cylinder("Steering_Column", (sx, -.210, .844), .026, .228,
                  mats["dark"], vertices=10, axis="Y"))
    wheel_points = [(sx+.140*cos(t*2*pi/20), sy, sz+.130*sin(t*2*pi/20))
                    for t in range(21)]
    pipe("Steering_Wheel_Rim", wheel_points, .0185, "dark", sides=7)
    for angle in (0, pi, 1.5*pi):
        pipe("Steering_Wheel_Spoke", [(sx, sy+.003, sz),
             (sx+.116*cos(angle), sy, sz+.110*sin(angle))], .017, "metal", sides=5)
    keep(cylinder("Steering_Wheel_Hub", (sx, sy+.011, sz), .043, .044,
                  mats["dark"], vertices=12, axis="Y"))
    block("Steering_Wheel_Center_Emblem", (sx, sy+.035, sz), (.013, .005, .024), "metal", .002)
    for dx in (-.075, .075):
        block("Steering_Paddle", (sx+dx, sy-.022, sz+.018), (.022, .013, .074), "metal", .005)
    pipe("Indicator_Stalk", [(sx-.026, -.154, .844), (sx-.153, -.147, .862)],
         .007, "dark", sides=6)
    for name, px, width in (("Brake", -.446, .071), ("Accelerator", -.334, .038)):
        pedal = block("Pedal_"+name, (px, -.485, .401), (width, .021, .086), "metal", .007)
        pedal.rotation_euler.x = -.30
        for dz in (-.021, 0, .021):
            block("Pedal_"+name+"_Grip", (px, -.468, .401+dz),
                  (width*.72, .006, .005), "rubber", .001)

    # Console, selector and compact cup holders occupy only the center gap.
    block("Center_Console", (0, .143, .481), (.171, .805, .149), "leather", .029)
    block("Center_Console_Inset", (0, -.073, .561), (.133, .283, .020), "dark", .014)
    block("Drive_Selector_Base", (0, -.125, .578), (.080, .109, .025), "metal", .012)
    selector = block("Drive_Selector", (0, -.112, .618), (.039, .072, .057), "dark", .012)
    selector.rotation_euler.x = -.18
    block("Drive_Selector_Cap", (0, -.104, .649), (.035, .045, .010), "metal", .007)
    for yy in (.102, .228):
        keep(cylinder("Cupholder_Recess", (0, yy, .559), .044, .008,
                      mats["rubber"], vertices=12, axis="Z"))
        ring = [(.047*cos(t*2*pi/12), yy+.047*sin(t*2*pi/12), .565)
                for t in range(13)]
        pipe("Cupholder_Rim", ring, .004, "metal", sides=4)
    block("Center_Console_Armrest", (0, .435, .581), (.159, .242, .076), "dark", .023)
    for side in (-1, 1):
        pipe("Console_Piping", [(side*.078, -.20, .548), (side*.078, .28, .548),
                               (side*.070, .515, .553)], .0025, "metal", sides=4)

    # Short visible interior sill caps; the exterior builder owns door cards.
    for side in (-1, 1):
        block("Interior_Sill_Cap", (side*.735, .24, .373), (.040, 1.02, .025), "metal", .007)

    return created

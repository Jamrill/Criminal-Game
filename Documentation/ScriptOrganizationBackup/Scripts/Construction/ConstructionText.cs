using System;
using JuegoCriminal.Localization;

namespace JuegoCriminal.Construction
{
    public static class ConstructionText
    {
        public static string Title(BuildEntry entry)
        {
            if(entry==null)return "";
            string name=(entry.prefab?entry.prefab.name:entry.title).ToLowerInvariant();
            foreach(string suffix in new[]{"_semincomplete","_incomplete","_complete"})
                if(name.EndsWith(suffix,StringComparison.Ordinal))name=name.Substring(0,name.Length-suffix.Length);
            return name switch
            {
                "wall"=>GameLanguage.T("Wall","Pared"),
                "corner_wall"=>GameLanguage.T("Corner wall","Pared de esquina"),
                "diagonal_wall"=>GameLanguage.T("Diagonal wall","Pared diagonal"),
                "t_wall"=>GameLanguage.T("T-junction wall","Pared en T"),
                "+_wall"=>GameLanguage.T("Cross wall","Pared en cruz"),
                "glass_wall"=>GameLanguage.T("Glass wall","Pared de cristal"),
                "glass_wall_door"=>GameLanguage.T("Glass wall with door","Cristalera con puerta"),
                "wall_door_hole"=>GameLanguage.T("Doorway wall","Pared con puerta"),
                "window_wall"=>GameLanguage.T("Window wall","Pared con ventana"),
                "bed"=>GameLanguage.T("Bed","Cama"),"chair"=>GameLanguage.T("Chair","Silla"),
                "table"=>GameLanguage.T("Table","Mesa"),"sofa"=>GameLanguage.T("Sofa","Sofá"),
                "floor"=>GameLanguage.T("Floor","Suelo"),"stairs"=>GameLanguage.T("Stairs","Escaleras"),
                "door"=>GameLanguage.T("Door","Puerta"),"toilet"=>GameLanguage.T("Toilet","Inodoro"),
                "counter"=>GameLanguage.T("Counter","Mostrador"),
                "central_display_unit"=>GameLanguage.T("Display island","Expositor central"),
                "display_shelving_unit"=>GameLanguage.T("Display shelves","Estantería expositora"),
                "floor_lamp"=>GameLanguage.T("Floor lamp","Lámpara de pie"),
                "kitchen cabinet"=>GameLanguage.T("Kitchen cabinet","Mueble de cocina"),
                "light_point"=>GameLanguage.T("Warm ceiling light","Plafón cálido"),
                "light_point_cold"=>GameLanguage.T("Cool ceiling light","Plafón frío"),
                "new_cheap_3d_printer"=>GameLanguage.T("3D printer","Impresora 3D"),
                _=>entry.title.Replace('_',' ')
            };
        }
    }
}

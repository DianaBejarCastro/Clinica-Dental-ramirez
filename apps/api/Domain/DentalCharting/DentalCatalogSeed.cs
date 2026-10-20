namespace ClinicaRamirez.Api.Domain.DentalCharting;
public static class DentalCatalogSeed
{
    public static DentalTooth[] Teeth() {
        var teeth = new List<DentalTooth>(); var order = 0;
        foreach (var q in new[] { 1, 2, 4, 3, 5, 6, 8, 7 }) {
            var primary = q >= 5; var positions = Enumerable.Range(1, primary ? 5 : 8);
            if (q is 1 or 4 or 5 or 8) positions = positions.Reverse();
            foreach (var p in positions) teeth.Add(new() { Id = q*10+p, FdiCode = q*10+p, Quadrant = q,
                DentitionClass = primary ? "PRIMARY" : "PERMANENT", ToothType = p <= 2 ? "INCISOR" : p == 3 ? "CANINE" : !primary && p <= 5 ? "PREMOLAR" : "MOLAR", DisplayOrder = ++order });
        }
        return teeth.ToArray();
    }
    public static DentalSurface[] Surfaces() => [
        new(){Id=1,Code="VESTIBULAR",Name="Vestibular",DisplayOrder=1},
        new(){Id=2,Code="OCCLUSAL_INCISAL",Name="Oclusal / incisal",DisplayOrder=2},
        new(){Id=3,Code="LINGUAL_PALATAL",Name="Lingual / palatina",DisplayOrder=3},
        new(){Id=4,Code="MESIAL",Name="Mesial",DisplayOrder=4},
        new(){Id=5,Code="DISTAL",Name="Distal",DisplayOrder=5}];
    public static PeriodontalSite[] Sites() => [
        new(){Id=1,Code="MB",Name="Mesiovestibular",SurfaceGroup="VESTIBULAR",DisplayOrder=1},
        new(){Id=2,Code="B",Name="Vestibular",SurfaceGroup="VESTIBULAR",DisplayOrder=2},
        new(){Id=3,Code="DB",Name="Distovestibular",SurfaceGroup="VESTIBULAR",DisplayOrder=3},
        new(){Id=4,Code="ML",Name="Mesiolingual / mesiopalatino",SurfaceGroup="LINGUAL_PALATAL",DisplayOrder=4},
        new(){Id=5,Code="L",Name="Lingual / palatino",SurfaceGroup="LINGUAL_PALATAL",DisplayOrder=5},
        new(){Id=6,Code="DL",Name="Distolingual / distopalatino",SurfaceGroup="LINGUAL_PALATAL",DisplayOrder=6}];
    public static OdontogramFindingType[] Findings() => [
        new(){Id=1,Code="ABFRACTION",Name="Abfraccion",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=1},
        new(){Id=2,Code="CERVICAL_ABRASION",Name="Abrasion cervical",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=2},
        new(){Id=3,Code="CARIES",Name="Caries",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=3},
        new(){Id=4,Code="AMALGAM_ADAPTED",Name="Amalgama adaptada",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=4},
        new(){Id=5,Code="AMALGAM_MALADAPTED",Name="Amalgama desadaptada",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=5},
        new(){Id=6,Code="VENEER_ADAPTED",Name="Carilla adaptada",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=true,DisplayOrder=6},
        new(){Id=7,Code="VENEER_MALADAPTED",Name="Carilla desadaptada",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=true,DisplayOrder=7},
        new(){Id=8,Code="CROWN_ADAPTED",Name="Corona adaptada",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=8},
        new(){Id=9,Code="CROWN_MALADAPTED",Name="Corona desadaptada",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=9},
        new(){Id=10,Code="HEALTHY",Name="Sano",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=true,DisplayOrder=10},
        new(){Id=11,Code="MISSING_TOOTH",Name="Diente ausente",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=11},
        new(){Id=12,Code="IMPACTED_TOOTH",Name="Diente retenido",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=12},
        new(){Id=13,Code="PARTIALLY_ERUPTED",Name="Parcialmente erupcionado",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=13},
        new(){Id=14,Code="WITHOUT_ANTAGONIST",Name="Sin antagonista",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=14},
        new(){Id=15,Code="EXTRACTION_INDICATED",Name="Extraccion indicada",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=15},
        new(){Id=16,Code="FRACTURE",Name="Fractura",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=true,DisplayOrder=16},
        new(){Id=17,Code="IMPLANT_ADAPTED",Name="Implante adaptado",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=17},
        new(){Id=18,Code="IMPLANT_MALADAPTED",Name="Implante desadaptado",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=18},
        new(){Id=19,Code="PERIAPICAL_LESION",Name="Lesion periapical",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=19},
        new(){Id=20,Code="STAIN",Name="Mancha",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=20},
        new(){Id=21,Code="ENDODONTIC_GOOD",Name="Endodoncia en buen estado",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=21},
        new(){Id=22,Code="ENDODONTIC_INDICATED",Name="Endodoncia indicada",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=22},
        new(){Id=23,Code="ENDODONTIC_POOR",Name="Endodoncia deficiente",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=23},
        new(){Id=24,Code="INTRARADICULAR_RESTORATION_ADAPTED",Name="Restauracion intrarradicular adaptada",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=24},
        new(){Id=25,Code="INTRARADICULAR_RESTORATION_MALADAPTED",Name="Restauracion intrarradicular desadaptada",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=25},
        new(){Id=26,Code="PONTIC",Name="Pontico",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=26},
        new(){Id=27,Code="PROVISIONAL_ADAPTED",Name="Provisional adaptado",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=27},
        new(){Id=28,Code="PROVISIONAL_MALADAPTED",Name="Provisional desadaptado",Category="RESTORATION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=28},
        new(){Id=29,Code="ROOT_REMAINS",Name="Restos radiculares",Category="CONDITION",AppliesToSurface=false,AppliesToWholeTooth=true,DisplayOrder=29},
        new(){Id=30,Code="SEALANT_ADAPTED",Name="Sellante adaptado",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=30},
        new(){Id=31,Code="SEALANT_MALADAPTED",Name="Sellante desadaptado",Category="RESTORATION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=31},
        new(){Id=32,Code="EROSION",Name="Erosion",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=false,DisplayOrder=32},
        new(){Id=33,Code="OTHER",Name="Otro hallazgo",Category="CONDITION",AppliesToSurface=true,AppliesToWholeTooth=true,DisplayOrder=33},
];
}

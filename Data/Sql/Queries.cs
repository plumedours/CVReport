namespace CVReport.Data.Sql
{
    public static class Queries
    {
        public static class JobInfo
        {
            public const string GetLatestHeader = @"
SELECT TOP (1) [ID], [Job Number], [Job Name]
FROM [Job Info]
ORDER BY [ID] DESC;";
        }

        public static class Cabinets
        {
            public const string ListAll = @"
SELECT [Cabinet ID]   AS CabinetID,
       [Cabinet Name] AS CabinetName
FROM dbo.Cabinets
ORDER BY [Cabinet Name];";

            public const string MaterialsByCabinet = @"
SELECT
    p.[Cabinet ID]  AS CabinetID,
    p.[Material ID] AS MaterialID,
    p.[Description] AS PartDescription,
    m.[Name]        AS MaterialName,
    COUNT(*)        AS Quantity
FROM dbo.Parts AS p
INNER JOIN dbo.Cabinets AS c
    ON c.[Cabinet ID] = p.[Cabinet ID]
LEFT JOIN dbo.CxMaterial AS m
    ON m.[ID] = p.[Material ID]
WHERE p.[Cabinet ID] = @CabinetId
GROUP BY
    p.[Cabinet ID],
    p.[Material ID],
    p.[Description],
    m.[Name]
ORDER BY m.[Name];";
        }

        public static class Materials
        {
            // ===== Agg principale (inchangée côté calculs) =====
            public const string PartsAgg = @"
SELECT TOP 10000
    M.[ID]                          AS [MaterialID],
    M.[Name]                        AS [Product],
    M.[Description]                 AS [Description],
    M.[UnitOfIssueID]               AS [UnitOfIssueID],
    ISNULL(MO.[Waste], 0) / 100.0   AS [WastePercent],

    COUNT(*)                        AS [Each],
    SUM(P.[Length] / 12.0)          AS [LinFt],
    SUM(P.[Area] / 144.0)           AS [SqFt],
    SUM((P.[Area] * (EBI.[RoughThickness] / 25.4)) / 144.0) AS [BdFt],
    SUM(P.[Length] * 0.0254)        AS [LinM],
    SUM(P.[Area] * 0.0254 * 0.0254) AS [SqM],
    SUM((P.[Area] * 0.0254 * 0.0254) * ((EBI.[RoughThickness] / 25.4) * 0.0254)) AS [BdM],
    SUM((P.[Width] / 12.0) * (P.[Length] / 12.0) * (ESI.[Thickness] / 25.4)) AS [CubicFt],
    SUM((P.[Area] * 0.0254 * 0.0254) * ((ESI.[Thickness] / 25.4) * 0.0254))  AS [CubicM],
    SUM( (P.[Area] / 144.0) / NULLIF(IIF(ESI.[Width] > 0 AND ESI.[Length] > 0,
         (ESI.[Width] / 25.4) * (ESI.[Length] / 25.4) / 144.0, 1.0), 0.0) )    AS [NoOfSheets],

    FLOOR(COUNT(*) + (COUNT(*) * ISNULL(MO.[Waste],0) / 100.0) + 0.999999999) AS [EachWaste],
    SUM(P.[Length] / 12.0 * (1 + ISNULL(MO.[Waste],0) / 100.0))          AS [LinFtWaste],
    SUM(P.[Area] / 144.0  * (1 + ISNULL(MO.[Waste],0) / 100.0))          AS [SqFtWaste],
    SUM(((P.[Area] * (EBI.[RoughThickness] / 25.4)) / 144.0) * (1 + ISNULL(MO.[Waste],0) / 100.0)) AS [BdFtWaste],
    SUM(P.[Length] * 0.0254 * (1 + ISNULL(MO.[Waste],0) / 100.0))        AS [LinMWaste],
    SUM((P.[Area] * 0.0254 * 0.0254) * (1 + ISNULL(MO.[Waste],0) / 100.0)) AS [SqMWaste],
    SUM(((P.[Area] * 0.0254 * 0.0254) * ((EBI.[RoughThickness] / 25.4) * 0.0254)) * (1 + ISNULL(MO.[Waste],0) / 100.0)) AS [BdMWaste],
    SUM(((P.[Width] / 12.0) * (P.[Length] / 12.0) * (ESI.[Thickness] / 25.4)) * (1 + ISNULL(MO.[Waste],0) / 100.0)) AS [CubicFtWaste],
    SUM(((P.[Area] * 0.0254 * 0.0254) * ((ESI.[Thickness] / 25.4) * 0.0254)) * (1 + ISNULL(MO.[Waste],0) / 100.0))  AS [CubicMWaste],
    SUM( ((P.[Area] / 144.0) / NULLIF(IIF(ESI.[Width] > 0 AND ESI.[Length] > 0,
         (ESI.[Width] / 25.4) * (ESI.[Length] / 25.4) / 144.0, 1.0), 0.0)) * (1 + ISNULL(MO.[Waste],0) / 100.0) ) AS [NoOfSheetsWaste]

FROM dbo.Parts P
LEFT JOIN [CxMaterial]       M   ON P.[Material ID] = M.[ID]
LEFT JOIN [CxExtraBoardInfo] EBI ON M.[ID] = EBI.[MaterialID]
LEFT JOIN [CxExtraSizeInfo]  ESI ON M.[ID] = ESI.[MaterialID]
LEFT JOIN [MaterialOverride] MO  ON M.[ID] = MO.[MatID]
GROUP BY M.[ID], M.[Name], M.[Description], M.[UnitOfIssueID], ISNULL(MO.[Waste],0)
ORDER BY M.[Name];";

            // Linéaire des moulures à fusionner
            public const string MoldingAgg = @"
SELECT
    M.[ID]          AS [MaterialID],
    M.[Name]        AS [Product],
    M.[Description] AS [Description],
    SUM(Mo.[Lineal Total]) AS [LinFt]
FROM [Molding] Mo
JOIN [CxMaterial] M ON Mo.[Material ID] = M.[ID]
WHERE (Mo.[Tag] > '' OR (Mo.[Tag] = '' AND Mo.[ProfileID] <> Mo.[ProfileSetID]))
  AND Mo.[Type] <> 2
GROUP BY M.[ID], M.[Name], M.[Description];";

            // Liste des MaterialID à exclure – nouveau schéma (CxMaterialParameter)
            public const string ExcludedByCxParam = @"
SELECT DISTINCT MP.[MaterialID]
FROM dbo.[CxMaterialParameter] MP
WHERE MP.[Name] = 'CIENAPPS_EXCL'
  AND (TRY_CONVERT(int, MP.[ParamValue]) = 1
       OR UPPER(LTRIM(RTRIM(CONVERT(varchar(16), MP.[ParamValue])))) IN ('1','TRUE','OUI','YES'));";

            // Liste des MaterialID à exclure – ancien schéma (MaterialParameter.Inclus = 0)
//            public const string ExcludedByOldParam = @"
//SELECT DISTINCT MP.[MaterialID]
//FROM dbo.[CxMaterialParameter] MP
//WHERE ISNULL(MP.[Inclus], 1) = 0;";
        }

        public static class Soumission
        {
            // (existant) Panneaux « simple » (arrondi direct)
            public const string PanelsList = @"
SELECT
    M.[Name]        AS [Nom],
    M.[Description] AS [Description],
    CEILING(SUM(
        ((P.[Area] / 144.0) /
            NULLIF(
                IIF(ESI.[Width] > 0 AND ESI.[Length] > 0,
                    (ESI.[Width] / 25.4) * (ESI.[Length] / 25.4) / 144.0, 1.0),
                0.0)
        ) * (1 + ISNULL(MO.[Waste],0)/100.0)
    )) AS [Qté feuilles]
FROM dbo.Parts AS P
LEFT JOIN dbo.CxMaterial       AS M   ON P.[Material ID] = M.[ID]
LEFT JOIN dbo.CxExtraSizeInfo  AS ESI ON M.[ID] = ESI.[MaterialID]
LEFT JOIN dbo.MaterialOverride AS MO  ON M.[ID] = MO.[MatID]
WHERE M.[MaterialTypeID] = 1
GROUP BY M.[Name], M.[Description]
ORDER BY [Nom];";

            // ✅ NOUVEAU : Panneaux détaillés (pour « Liste des panneaux » de l’appli)
            // Fournit : SqFtWaste (Pi² total), NoOfSheetsWaste (Qté réelle décimale)
            public const string PanelsSheetsDetailed = @"
SELECT
    M.[ID]               AS [MaterialID],
    M.[Name]             AS [Name],
    M.[Description]      AS [Description],
    M.[UnitOfIssueID]    AS [UnitOfIssueID],
    ISNULL(MO.[Waste],0) / 100.0 AS [WastePercent],

    SUM(P.[Area] / 144.0)                                           AS [SqFt],
    SUM((P.[Area] / 144.0) * (1 + ISNULL(MO.[Waste],0)/100.0))      AS [SqFtWaste],

    SUM( (P.[Area] / 144.0) /
         NULLIF(IIF(ESI.[Width] > 0 AND ESI.[Length] > 0,
                    (ESI.[Width] / 25.4) * (ESI.[Length] / 25.4) / 144.0, 1.0), 0.0)
       )                                                            AS [NoOfSheets],

    SUM( ((P.[Area] / 144.0) /
          NULLIF(IIF(ESI.[Width] > 0 AND ESI.[Length] > 0,
                     (ESI.[Width] / 25.4) * (ESI.[Length] / 25.4) / 144.0, 1.0), 0.0)) 
          * (1 + ISNULL(MO.[Waste],0)/100.0)
       )                                                            AS [NoOfSheetsWaste]
FROM dbo.Parts P
LEFT JOIN dbo.CxMaterial       M   ON P.[Material ID] = M.[ID]
LEFT JOIN dbo.CxExtraSizeInfo  ESI ON M.[ID]          = ESI.[MaterialID]
LEFT JOIN dbo.MaterialOverride MO  ON M.[ID]          = MO.[MatID]
WHERE M.[MaterialTypeID] = 1
GROUP BY M.[ID], M.[Name], M.[Description], M.[UnitOfIssueID], ISNULL(MO.[Waste],0)
ORDER BY M.[Name];";

            // Portes — version réduite aux 4 colonnes : Nom, Matériel, Qté, Pi²
            public const string DoorsList = @"
WITH UniqueSchedule AS (
    SELECT ScheduleID, MIN(CostPerSqM) AS CostPerSqM
    FROM CxDoorScheduleMap
    GROUP BY ScheduleID
)
SELECT
    Doors.[Door Name]         AS [Nom],
    Doors.[Material Schedule] AS [Matériel],
    COUNT(DISTINCT Doors.[Door ID]) AS [Qté],
    SUM(
        CASE
            WHEN (Doors.Width * Doors.Height) / 144.0 < 1.5 THEN 1.5
            ELSE (Doors.Width * Doors.Height) / 144.0
        END
    ) AS [Pi² total]
FROM Doors
INNER JOIN UniqueSchedule us
    ON Doors.[Material Schedule ID] = us.ScheduleID
WHERE Doors.[Material Schedule ID] NOT IN (
    SELECT DISTINCT MP.[MaterialID]
    FROM dbo.[CxMaterialParameter] MP
    WHERE MP.[Name] = 'CIENAPPS_EXCL'
      AND (TRY_CONVERT(int, MP.[ParamValue]) = 1
           OR UPPER(LTRIM(RTRIM(CONVERT(varchar(16), MP.[ParamValue])))) IN ('1','TRUE','OUI','YES'))
)
GROUP BY
    Doors.[Door Name],
    Doors.[Material Schedule]
ORDER BY
    Doors.[Door Name],
    Doors.[Material Schedule];";

            public const string DrawersList = @"
SELECT
    COUNT(*)                          AS [Qté],
    m.[Name]                          AS [Nom],
    m.[Description]                   AS [Description]
FROM (
    SELECT GuideMaterialID FROM Drawers
    UNION ALL
    SELECT GuideMaterialID FROM Rollouts
) AS combined
JOIN CxMaterial m ON m.[ID] = combined.[GuideMaterialID]
GROUP BY
    m.[Name], m.[Description]
ORDER BY
    m.[Name];";

            public const string PullsList = @"
SELECT
    COUNT(*)        AS [Qté],
    M.[Name]        AS [Nom],
    M.[Description] AS [Description]
FROM dbo.Parts AS P
INNER JOIN dbo.CxMaterial AS M
    ON M.[ID] = P.[Material ID]
WHERE M.[MaterialTypeID] = 7      -- poignées
GROUP BY
    M.[Name], M.[Description]
ORDER BY
    M.[Name];";

            public const string NestingData = @"
SELECT
    P.ID AS PartID,
    M.ID AS MaterialID,
    M.[Name] AS [Name],
    M.[Description] AS [Description],
    ESI.[Width] / 25.4 AS SheetW,      -- en pouces
    ESI.[Length] / 25.4 AS SheetH,     -- en pouces
    P.[Width] AS PartW,         -- en pouces
    P.[Length] AS PartH,        -- en pouces
    COUNT(*) AS Qty,
    ISNULL(MO.[Waste], 0) AS Waste
FROM Parts P
JOIN CxMaterial M ON M.ID = P.[Material ID]
LEFT JOIN CxExtraSizeInfo ESI ON ESI.MaterialID = M.ID
LEFT JOIN MaterialOverride MO ON MO.MatID = M.ID
WHERE M.[MaterialTypeID] = 1
GROUP BY P.ID, M.ID, M.[Name], M.[Description], ESI.[Width], ESI.[Length], P.[Width], P.[Length], MO.[Waste];
";
        }

        public const string PanelMaterialIds = @"
SELECT [ID]
FROM dbo.[CxMaterial]
WHERE [MaterialTypeID] = 1;";
    }
}
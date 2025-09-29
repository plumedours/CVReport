using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using RapportCVVentes.Data; // SqlDb

namespace CVReport
{
    public partial class MainForm
    {
        private enum InfoKind { Neutral, Loading, Success, Warning, Error }

        // ---------------------------
        // Tables à synchroniser
        // ---------------------------

        private static readonly (string Source, string Dest)[] TableMap = new[]
        {
            ("Assembly",                "CxAssembly"),
            ("DoorScheduleMap",         "CxDoorScheduleMap"),
            ("MaterialExtraBoardInfo",  "CxExtraBoardInfo"),
            ("MaterialExtraSizeInfo",   "CxExtraSizeInfo"),
            ("Finish",                  "CxFinish"),
            ("Material",                "CxMaterial"),
            ("MaterialKitMap",          "CxMaterialKitMap"),
            ("MaterialMenuTree",        "CxMaterialMenuTree"),
            ("MaterialMenuTreeItem",    "CxMaterialMenuTreeItem"),
            ("MaterialParameter",       "CxMaterialParameter"),
            ("MaterialVendor",          "CxVendor"),
            ("Part",                    "CxPart"),
            ("refMaterialType",         "CxrefMaterialType")
        };

        // ---------------------------
        // Lecture appsettings
        // ---------------------------

        private static JsonElement? TryLoadRoot(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return null;
            return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        }

        private static string ReadActiveLocalConnectionString()
        {
            string? active = null;
            foreach (var root in new[] { TryLoadRoot("appsettings.Development.json"), TryLoadRoot("appsettings.json") })
            {
                if (root is JsonElement r && r.TryGetProperty("ActiveConnectionString", out var v))
                {
                    active = v.GetString();
                    if (!string.IsNullOrWhiteSpace(active)) break;
                }
            }
            if (string.IsNullOrWhiteSpace(active))
                throw new InvalidOperationException("ActiveConnectionString manquant dans la configuration.");

            foreach (var root in new[] { TryLoadRoot("appsettings.Development.json"), TryLoadRoot("appsettings.json") })
            {
                if (root is JsonElement r &&
                    r.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty(active!, out var val))
                {
                    var s = val.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s!;
                }
            }
            throw new InvalidOperationException($"ConnectionStrings:{active} est vide ou manquant.");
        }

        /// <summary>
        /// Chaîne de connexion au serveur principal (CVData sur {MACHINE}\CV24).
        /// - Remplace {MACHINE}
        /// - Basculable en SQL Auth si MainServerSqlAuth est défini
        /// </summary>
        private static string ReadMainServerConnectionString()
        {
            string? templ = null;
            string? sqlUser = null;
            string? sqlPass = null;

            foreach (var r in new[] { TryLoadRoot("appsettings.Development.json"), TryLoadRoot("appsettings.json") })
            {
                if (r is not JsonElement root) continue;
                if (!root.TryGetProperty("ConnectionStrings", out var cs)) continue;

                if (templ is null && cs.TryGetProperty("MainServerTemplate", out var t)) templ = t.GetString();

                if (sqlUser is null && cs.TryGetProperty("MainServerSqlAuth", out var sa))
                {
                    if (sa.TryGetProperty("User", out var u)) sqlUser = u.GetString();
                    if (sa.TryGetProperty("Password", out var p)) sqlPass = p.GetString();
                }
            }

            var machine = Environment.MachineName;

            // ⚠️ on force TCP avec le préfixe "tcp:" pour éviter Shared Memory / Named Pipes
            var baseConn = string.IsNullOrWhiteSpace(templ)
                ? $"Server=tcp:{machine}\\CV24;Database=CVData;Integrated Security=True;" +
                  $"MultipleActiveResultSets=True;TrustServerCertificate=True;Encrypt=True;Connect Timeout=30;Application Name=CVReport"
                : templ!.Replace("{MACHINE}", $"tcp:{machine}", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(sqlUser) && !string.IsNullOrWhiteSpace(sqlPass))
            {
                var b = new SqlConnectionStringBuilder(baseConn)
                {
                    IntegratedSecurity = false,
                    UserID = sqlUser,
                    Password = sqlPass
                };
                baseConn = b.ConnectionString;
            }

            return baseConn;
        }

        // ---------------------------
        // Synchro des tables Cx*
        // ---------------------------

        /// <summary>
        /// Recrée dans la localdb toutes les tables Cx* comme copie de la base principale.
        /// DROP (tolérant)/CREATE + BULK COPY pour chaque table de TableMap.
        /// </summary>
        private async Task EnsureCxTablesAsync()
        {
            await _cxTablesGate.WaitAsync();               // ✅ début section critique
            try
            {
                //ShowLoading("Préparation des tables Cx…");

                var mainConn = ReadMainServerConnectionString();
                var localConn = ReadActiveLocalConnectionString();

                var warnings = new List<string>();
                var failures = new List<string>();

                try
                {
                    using var main = new SqlConnection(mainConn);
                    await OpenWithRetryAsync(main);

                    using var dst = new SqlDb(localConn);

                    foreach (var (srcName, destName) in TableMap)
                    {
                        try
                        {
                            if (!await SourceTableExistsAsync(main, srcName))
                            {
                                warnings.Add($"{srcName} absente sur le serveur — ignorée.");
                                continue;
                            }

                            await DropAnyObjectNamedAsync(dst, destName);               // drop tolérant
                            var createSql = await BuildCreateTableSqlFromSourceAsync(main, srcName, destName);
                            await dst.ExecuteAsync(createSql);                           // create
                            await CopyAllRowsAsync(main, localConn, srcName, destName);  // bulk
                        }
                        catch (SqlException ex) when (ex.Number == 916 || ex.Number == 18456)
                        {
                            throw new InvalidOperationException(
                                "Accès refusé/Authentification échouée sur le serveur principal. " +
                                "Vérifiez ConnectionStrings.MainServerSqlAuth (login 'reporting').", ex);
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{srcName} → {destName}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    //HideLoading();
                }

                if (warnings.Count > 0 && failures.Count == 0)
                    MessageBox.Show(this, "Certaines tables sources sont absentes et ont été ignorées :\r\n- " +
                                         string.Join("\r\n- ", warnings), "Information",
                                         MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (failures.Count > 0)
                    throw new ApplicationException("Certaines tables n'ont pas pu être synchronisées:\r\n- " +
                                                   string.Join("\r\n- ", failures));
            }
            finally
            {
                _cxTablesGate.Release();                    // ✅ fin section critique
            }
        }

        // ---------------------------
        // Helpers SQL
        // ---------------------------

        private static async Task<bool> SourceTableExistsAsync(SqlConnection source, string table)
        {
            var sql = $"SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.[{table}]') AND type = 'U';";
            using var cmd = new SqlCommand(sql, source);
            var o = await cmd.ExecuteScalarAsync();
            return o != null;
        }

        private static async Task OpenWithRetryAsync(SqlConnection conn, int maxRetries = 5)
        {
            int delayMs = 150;
            for (int i = 0; ; i++)
            {
                try
                {
                    await conn.OpenAsync();
                    return;
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (
                       ex.Number == 233 // “No process at the other end of the pipe”
                    || ex.InnerException is System.ComponentModel.Win32Exception)
                {
                    if (i >= maxRetries) throw;
                    await Task.Delay(delayMs);
                    delayMs = Math.Min(delayMs * 2, 1500); // backoff
                }
            }
        }

        /// <summary>
        /// Drop de tout objet nommé objectName (tous schémas/types) + drop des FK qui le référencent si table.
        /// Tolérant si l'objet n'existe pas : ne lève pas d'exception.
        /// </summary>
        private static async Task DropAnyObjectNamedAsync(SqlDb localDb, string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return;

            // 1) Drop FKs pointant vers toute table nommée objectName (quel que soit le schéma)
            var dropFksSql = @"
DECLARE @name sysname = @p_name;
DECLARE @sql  nvarchar(max) = N'';

;WITH targets AS (
    SELECT o.object_id, QUOTENAME(s.name)+'.'+QUOTENAME(o.name) AS fqname
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    WHERE o.[type] = 'U' AND o.name = @name
)
SELECT @sql = @sql + N'ALTER TABLE '
         + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.'
         + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
         + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk
JOIN targets t ON fk.referenced_object_id = t.object_id;

IF LEN(@sql) > 0 EXEC sp_executesql @sql;";
            try { await localDb.ExecuteAsync(dropFksSql, new SqlParameter("@p_name", objectName)); } catch { /* ignore */ }

            // 2) Drop des objets homonymes (tous schémas / plusieurs types) — IF OBJECT_ID protège
            var dropObjectsSql = @"
DECLARE @name sysname = @p_name;

-- tables
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'IF OBJECT_ID(''' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + ''',''U'') IS NOT NULL DROP TABLE ' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + N';'
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.name = @name AND o.[type] = 'U';
IF LEN(@sql)>0 EXEC sp_executesql @sql;

-- vues
SET @sql = N'';
SELECT @sql = @sql + N'IF OBJECT_ID(''' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + ''',''V'') IS NOT NULL DROP VIEW ' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + N';'
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.name = @name AND o.[type] = 'V';
IF LEN(@sql)>0 EXEC sp_executesql @sql;

-- synonymes
SET @sql = N'';
SELECT @sql = @sql + N'IF OBJECT_ID(''' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + ''',''SN'') IS NOT NULL DROP SYNONYM ' + QUOTENAME(s.name)+'.'+QUOTENAME(o.name) + N';'
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.name = @name AND o.[type] = 'SN';
IF LEN(@sql)>0 EXEC sp_executesql @sql;";
            try { await localDb.ExecuteAsync(dropObjectsSql, new SqlParameter("@p_name", objectName)); } catch { /* ignore */ }
        }

        /// <summary>
        /// Construit le CREATE TABLE [dbo].[destTable] à partir du schéma dbo.[srcTable].
        /// - copie la collation des colonnes texte
        /// - mappe rowversion/timestamp en binary(8) pour permettre le bulk insert
        /// </summary>
        private static async Task<string> BuildCreateTableSqlFromSourceAsync(SqlConnection source, string srcTable, string destTable)
        {
            string schemaSql = $@"
SELECT
    c.column_id,
    c.name              AS ColumnName,
    t.name              AS TypeName,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.collation_name,
    c.is_identity
FROM sys.columns c
JOIN sys.types   t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.[{srcTable}]')
ORDER BY c.column_id;";

            var cols = new List<string>();

            using (var cmd = new SqlCommand(schemaSql, source))
            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                while (await rdr.ReadAsync())
                {
                    string colName = rdr.GetString(rdr.GetOrdinal("ColumnName"));
                    string typeName = rdr.GetString(rdr.GetOrdinal("TypeName")).ToLowerInvariant();
                    short maxLen = rdr.GetInt16(rdr.GetOrdinal("max_length"));
                    byte precision = rdr.GetByte(rdr.GetOrdinal("precision"));
                    byte scale = rdr.GetByte(rdr.GetOrdinal("scale"));
                    bool isNull = rdr.GetBoolean(rdr.GetOrdinal("is_nullable"));
                    string? coll = rdr.IsDBNull(rdr.GetOrdinal("collation_name"))
                                        ? null : rdr.GetString(rdr.GetOrdinal("collation_name"));
                    // bool isIdentity = rdr.GetBoolean(rdr.GetOrdinal("is_identity")); // ignoré volontairement

                    // Type SQL destination (avec mapping rowversion)
                    string typeSql = BuildTypeDeclaration(typeName, maxLen, precision, scale);

                    // Collation pour les types texte
                    if (coll != null && IsTextualType(typeName))
                        typeSql += $" COLLATE {coll}";

                    cols.Add($"[{colName}] {typeSql} {(isNull ? "NULL" : "NOT NULL")}");
                }
            }

            if (cols.Count == 0)
                throw new InvalidOperationException($"Schéma vide pour dbo.[{srcTable}]");

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE [dbo].[{destTable}] (");
            sb.AppendLine(string.Join(",\r\n", cols));
            sb.AppendLine(");");

            return sb.ToString();
        }

        private static bool IsTextualType(string typeName)
        {
            switch (typeName)
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                case "text":
                case "ntext":
                case "sysname":
                    return true;
                default:
                    return false;
            }
        }

        private static string BuildTypeDeclaration(string typeName, short maxLenBytes, byte precision, byte scale)
        {
            // Normalise sysname -> nvarchar(128)
            if (typeName == "sysname") return "nvarchar(128)";

            // Mappe rowversion/timestamp -> binary(8) (sinon on ne peut pas insérer)
            if (typeName == "timestamp" || typeName == "rowversion")
                return "binary(8)";

            string SizeFor(string t)
            {
                if (maxLenBytes == -1) return "max";
                if (t == "nvarchar" || t == "nchar")
                {
                    int chars = Math.Max(1, maxLenBytes / 2);
                    return chars.ToString();
                }
                return Math.Max(1, (int)maxLenBytes).ToString();
            }

            switch (typeName)
            {
                case "varchar":
                case "char":
                case "nvarchar":
                case "nchar":
                case "varbinary":
                case "binary":
                    return $"{typeName}({SizeFor(typeName)})";

                case "decimal":
                case "numeric":
                    return $"{typeName}({(precision == 0 ? (byte)18 : precision)},{scale})";

                case "datetime2":
                case "time":
                    return scale > 0 ? $"{typeName}({scale})" : typeName;

                // Types simples
                case "datetime":
                case "smalldatetime":
                case "date":
                case "bit":
                case "int":
                case "bigint":
                case "smallint":
                case "tinyint":
                case "float":
                case "real":
                case "money":
                case "smallmoney":
                case "uniqueidentifier":
                case "xml":
                case "image":
                case "text":
                case "ntext":
                    return typeName;

                default:
                    // fallback prudent
                    return "nvarchar(max)";
            }
        }

        /// <summary>
        /// Bulk copy de dbo.[srcTable] (serveur) vers [dbo].[destTable] (local) avec mappings par nom.
        /// </summary>
        private static async Task CopyAllRowsAsync(SqlConnection source, string localConn, string srcTable, string destTable)
        {
            // On lit *toutes* les colonnes source : les rowversion seront des byte[] et iront dans binary(8) cible
            using var readCmd = new SqlCommand($"SELECT * FROM dbo.[{srcTable}];", source);
            using var reader = await readCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            using var destConn = new SqlConnection(localConn);
            await destConn.OpenAsync();

            using var bulk = new SqlBulkCopy(destConn)
            {
                DestinationTableName = $"[dbo].[{destTable}]",
                BulkCopyTimeout = 0
            };

            // Mappings par nom 1-1
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string col = reader.GetName(i);
                bulk.ColumnMappings.Add(col, col);
            }

            await bulk.WriteToServerAsync(reader);
        }
    }
}
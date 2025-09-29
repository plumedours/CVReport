using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace RapportCVVentes.Data
{
    /// <summary>
    /// Accès SQL sûr en concurrence : une connexion par opération (pool ADO.NET).
    /// Garde les mêmes signatures publiques que ta classe d’origine.
    /// </summary>
    public sealed class SqlDb : IAsyncDisposable, IDisposable
    {
        private readonly string _connectionString;

        // Ancienne connexion partagée : on la conserve pour compat (Dispose),
        // mais on NE L'UTILISE PLUS pour exécuter des commandes.
        private SqlConnection? _conn;

        // Petit verrou si jamais OpenAsync() est appelé en parallèle
        private readonly SemaphoreSlim _openGate = new(1, 1);

        public SqlDb(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Compatibilité : test d'ouverture. Ne maintient PAS d'état partagé ouvert.
        /// </summary>
        public async Task OpenAsync()
        {
            await _openGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // On ferme/détruit l’ancienne instance si jamais elle existait
                if (_conn != null)
                {
                    try { await _conn.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
                    await _conn.DisposeAsync().ConfigureAwait(false);
                    _conn = null;
                }

                // On teste simplement que la chaîne ouvre bien
                using var test = new SqlConnection(_connectionString);
                await test.OpenAsync().ConfigureAwait(false);
                // on referme aussitôt (pooling actif côté ADO.NET)
            }
            finally
            {
                _openGate.Release();
            }
        }

        /// <summary>
        /// Exécute une requête SELECT et retourne un DataTable.
        /// </summary>
        public async Task<DataTable> QueryAsync(string sql, params SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL vide", nameof(sql));

            // ➜ Connexion locale par opération (pas de partage d’état)
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text };
            if (parameters is { Length: > 0 })
                cmd.Parameters.AddRange(parameters);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            var dt = new DataTable();
            dt.Load(reader);
            return dt;
        }

        /// <summary>
        /// Exécute une commande non requête (INSERT/UPDATE/DELETE/DDL).
        /// </summary>
        public async Task<int> ExecuteAsync(string sql, params SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL vide", nameof(sql));

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text };
            if (parameters is { Length: > 0 })
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // --- Optionnel, utile si tu en as besoin ailleurs ; ne casse rien si non utilisé.
        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL vide", nameof(sql));

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text };
            if (parameters is { Length: > 0 })
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        }

        // IDisposable (synchro) pour using var
        public void Dispose()
        {
            if (_conn != null)
            {
                try { _conn.Dispose(); } catch { /* ignore */ }
                _conn = null;
            }
            _openGate.Dispose();
        }

        // IAsyncDisposable (si un jour tu veux await using)
        public async ValueTask DisposeAsync()
        {
            if (_conn != null)
            {
                try { await _conn.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
                try { await _conn.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
                _conn = null;
            }
            _openGate.Dispose();
        }
    }
}
using MySqlConnector;
using ExtraitBancaire.Common.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;

namespace ExtraitBancaire.Api.Services
{
    public class MariaDbService
    {
        private readonly string _connectionString;

        public MariaDbService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString); // MySqlConnector.MySqlConnection
        }

        public async Task SaveMonthlyDataAsync(MonthlyData monthlyData)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Insérer ou mettre à jour l'extrait mensuel
                        string insertMonthlySql = @"INSERT INTO extraits_mensuels 
                                                  (annee, mois, nom_mois, solde_initial, solde_final, total_debit, total_credit, mouvement_net, nombre_operations) 
                                                  VALUES (@Annee, @Mois, @NomMois, @SoldeInitial, @SoldeFinal, @TotalDebit, @TotalCredit, @MouvementNet, @NombreOperations)
                                                  ON DUPLICATE KEY UPDATE 
                                                  nom_mois = VALUES(nom_mois), solde_initial = VALUES(solde_initial), solde_final = VALUES(solde_final),
                                                  total_debit = VALUES(total_debit), total_credit = VALUES(total_credit), mouvement_net = VALUES(mouvement_net),
                                                  nombre_operations = VALUES(nombre_operations);";

                        using (var cmd = new MySqlCommand(insertMonthlySql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Annee", DateTime.Now.Year);
                            cmd.Parameters.AddWithValue("@Mois", monthlyData.Month);
                            cmd.Parameters.AddWithValue("@NomMois", monthlyData.MonthName);
                            cmd.Parameters.AddWithValue("@SoldeInitial", monthlyData.InitialBalance);
                            cmd.Parameters.AddWithValue("@SoldeFinal", monthlyData.FinalBalance);
                            cmd.Parameters.AddWithValue("@TotalDebit", monthlyData.TotalDebit);
                            cmd.Parameters.AddWithValue("@TotalCredit", monthlyData.TotalCredit);
                            cmd.Parameters.AddWithValue("@MouvementNet", monthlyData.NetMovement);
                            cmd.Parameters.AddWithValue("@NombreOperations", monthlyData.OperationCount);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Supprimer les anciennes opérations pour ce mois et cette année avant d'insérer les nouvelles
                        string deleteOperationsSql = "DELETE FROM operations_mensuelles WHERE annee_extrait = @Annee AND mois_extrait = @Mois;";
                        using (var cmd = new MySqlCommand(deleteOperationsSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Annee", DateTime.Now.Year);
                            cmd.Parameters.AddWithValue("@Mois", monthlyData.Month);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Insérer les nouvelles opérations
                        string insertOperationSql = @"INSERT INTO operations_mensuelles 
                                                  (annee_extrait, mois_extrait, date_operation, libelle, debit, credit, est_ajoute_manuellement) 
                                                  VALUES (@AnneeExtrait, @MoisExtrait, @DateOperation, @Libelle, @Debit, @Credit, @EstAjouteManuellement);";

                        foreach (var op in monthlyData.Entries)
                        {
                            using (var cmd = new MySqlCommand(insertOperationSql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@AnneeExtrait", DateTime.Now.Year);
                                cmd.Parameters.AddWithValue("@MoisExtrait", monthlyData.Month);
                                cmd.Parameters.AddWithValue("@DateOperation", op.Date);
                                cmd.Parameters.AddWithValue("@Libelle", op.Libelle);
                                cmd.Parameters.AddWithValue("@Debit", op.Debit);
                                cmd.Parameters.AddWithValue("@Credit", op.Credit);
                                cmd.Parameters.AddWithValue("@EstAjouteManuellement", op.IsManuallyAdded);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // Renvoyer l'exception pour gestion ultérieure
                    }
                }
            }
        }

        public async Task<List<MonthlyData>> LoadAllMonthlyDataAsync()
        {
            var monthlyDataList = new List<MonthlyData>();

            using (var conn = GetConnection())
            {
                await conn.OpenAsync();
                // Si cette ligne échoue, le problème est bien au niveau de la connexion/configuration

                string selectMonthlySql = "SELECT annee, mois, nom_mois, solde_initial, solde_final, total_debit, total_credit, mouvement_net, nombre_operations FROM extraits_mensuels;";
                using (var cmd = new MySqlCommand(selectMonthlySql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            try
                            {
                                var monthlyData = new MonthlyData
                                {
                                    Month = reader.IsDBNull(reader.GetOrdinal("mois")) ? 0 : Convert.ToInt32(reader["mois"]),
                                    InitialBalance = reader.IsDBNull(reader.GetOrdinal("solde_initial")) ? 0M : Convert.ToDecimal(reader["solde_initial"]),
                                    FinalBalance = reader.IsDBNull(reader.GetOrdinal("solde_final")) ? 0M : Convert.ToDecimal(reader["solde_final"]),
                                    TotalDebit = reader.IsDBNull(reader.GetOrdinal("total_debit")) ? 0M : Convert.ToDecimal(reader["total_debit"]),
                                    TotalCredit = reader.IsDBNull(reader.GetOrdinal("total_credit")) ? 0M : Convert.ToDecimal(reader["total_credit"]),
                                };
                                monthlyData.MonthName = reader.IsDBNull(reader.GetOrdinal("nom_mois")) ? string.Empty : reader["nom_mois"].ToString();
                                monthlyData.OperationCount = reader.IsDBNull(reader.GetOrdinal("nombre_operations")) ? 0 : Convert.ToInt32(reader["nombre_operations"]);
                                monthlyData.NetMovement = reader.IsDBNull(reader.GetOrdinal("mouvement_net")) ? 0M : Convert.ToDecimal(reader["mouvement_net"]);
                                monthlyDataList.Add(monthlyData);
                            }
                            catch (Exception ex)
                            {
                                // Log détaillé pour identifier la colonne problématique
                                throw new Exception($"Erreur lors de la lecture d'un extrait mensuel. Valeurs lues : " +
                                    $"mois={reader["mois"]}, solde_initial={reader["solde_initial"]}, solde_final={reader["solde_final"]}, " +
                                    $"total_debit={reader["total_debit"]}, total_credit={reader["total_credit"]}, nom_mois={reader["nom_mois"]}, " +
                                    $"nombre_operations={reader["nombre_operations"]}, mouvement_net={reader["mouvement_net"]}", ex);
                            }
                        }
                    }
                }

                // Maintenant, chargez les opérations pour chaque mois
                foreach (var monthlyData in monthlyDataList)
                {
                    string selectOperationsSql = @"SELECT date_operation, libelle, debit, credit, est_ajoute_manuellement 
                                                 FROM operations_mensuelles 
                                                 WHERE annee_extrait = @Annee AND mois_extrait = @Mois;";
                    using (var cmd = new MySqlCommand(selectOperationsSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Annee", DateTime.Now.Year);
                        cmd.Parameters.AddWithValue("@Mois", monthlyData.Month);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                monthlyData.Entries.Add(new ExtraitBancaireEntry
                                {
                                    Date = reader.IsDBNull(reader.GetOrdinal("date_operation")) ? default(DateTime) : Convert.ToDateTime(reader["date_operation"]),
                                    Libelle = reader["libelle"].ToString(),
                                    Debit = reader.IsDBNull(reader.GetOrdinal("debit")) ? 0 : Convert.ToDecimal(reader["debit"]),
                                    Credit = reader.IsDBNull(reader.GetOrdinal("credit")) ? 0 : Convert.ToDecimal(reader["credit"]),
                                    IsManuallyAdded = reader.IsDBNull(reader.GetOrdinal("est_ajoute_manuellement")) ? false : Convert.ToBoolean(reader["est_ajoute_manuellement"])
                                });
                            }
                        }
                    }
                    // Assurez-vous que les totaux sont corrects après le chargement des opérations
                    monthlyData.UpdateTotals();
                }
            }

            return monthlyDataList;
        }
    }
} 
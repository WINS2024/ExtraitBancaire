using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraitBancaire.Common.Models;
using System.Text.Json;

namespace ExtraitBancaire.Common.Models
{
    public class ExtraitBancaireEntry
    {
        public DateTime Date { get; set; }
        public string Libelle { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public bool IsManuallyAdded { get; set; }

        // Méthode pour créer une entrée à partir d'une opération
        public static ExtraitBancaireEntry FromOperation(Operation op)
        {
            Console.WriteLine($"Conversion de l'opération: {JsonSerializer.Serialize(op)}");
            
            DateTime dateOperation;
            if (string.IsNullOrEmpty(op.DateOperation))
            {
                Console.WriteLine("Date d'opération vide, utilisation de la date actuelle");
                dateOperation = DateTime.Now;
            }
            else
            {
                // Essayer d'abord le format DD/MM/YYYY
                if (!DateTime.TryParseExact(op.DateOperation, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOperation))
                {
                    // Si ça ne marche pas, essayer le format DD/MM et ajouter l'année courante
                    if (DateTime.TryParseExact(op.DateOperation, "dd/MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOperation))
                    {
                        dateOperation = new DateTime(DateTime.Now.Year, dateOperation.Month, dateOperation.Day);
                    }
                    else
                    {
                        Console.WriteLine($"Impossible de parser la date: {op.DateOperation}, utilisation de la date actuelle");
                        dateOperation = DateTime.Now;
                    }
                }
            }

            var entry = new ExtraitBancaireEntry
            {
                Date = dateOperation,
                Libelle = op.DescriptionOp ?? string.Empty,
                Debit = op.DebitOp ?? 0,
                Credit = op.CreditOp ?? 0,
                IsManuallyAdded = false
            };

            Console.WriteLine($"Entrée créée: {JsonSerializer.Serialize(entry)}");
            return entry;
        }
    }

    public class MonthlyData
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal InitialBalance { get; set; }
        public decimal FinalBalance { get; set; }
        public int OperationCount { get; set; }
        public List<ExtraitBancaireEntry> Entries { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetMovement { get; set; }

        public void UpdateTotals()
        {
            TotalDebit = Entries.Sum(op => op.Debit);
            TotalCredit = Entries.Sum(op => op.Credit);
            NetMovement = TotalCredit - TotalDebit;
            FinalBalance = InitialBalance + NetMovement;
            OperationCount = Entries.Count;
            MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);
        }

        public void AddOperation(ExtraitBancaireEntry operation)
        {
            Entries.Add(operation);
            UpdateTotals();
        }

        public void RemoveOperation(ExtraitBancaireEntry operation)
        {
            Entries.Remove(operation);
            UpdateTotals();
        }
    }

    public class PdfExtractionRequest
    {
        public string Base64Content { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    // Nouveau modèle pour la requête de division de PDF
    public class PdfSplitRequest : PdfExtractionRequest
    {
        public string PagesToSplit { get; set; } = string.Empty;
    }
} 
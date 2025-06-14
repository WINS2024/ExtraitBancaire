using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExtraitBancaire.Common.Models
{
    public class ExtractionResult
    {
        [JsonPropertyName("run_id")]
        public string RunId { get; set; }

        [JsonPropertyName("data")]
        public List<DataItem> Data { get; set; }

        [JsonPropertyName("extraction_agent_id")]
        public string ExtractionAgentId { get; set; }

        [JsonPropertyName("extraction_metadata")]
        public ExtractionMetadata ExtractionMetadata { get; set; }
    }

    public class DataItem
    {
        [JsonPropertyName("NomBanque")]
        public string NomBanque { get; set; }

        [JsonPropertyName("ReleveIdentiteBancaire")]
        public string ReleveIdentiteBancaire { get; set; }

        [JsonPropertyName("BatcheDate")]
        public string BatcheDate { get; set; }

        [JsonPropertyName("SoldeInitial")]
        public SoldeInitial SoldeInitial { get; set; }

        [JsonPropertyName("Operations")]
        public List<Operation> Operations { get; set; }

        [JsonPropertyName("SoldeFinal")]
        public SoldeFinal SoldeFinal { get; set; }
    }

    public class SoldeInitial
    {
        [JsonPropertyName("DebitSoldeInitial")]
        public decimal DebitSoldeInitial { get; set; }

        [JsonPropertyName("CreditSoldeInitial")]
        public decimal CreditSoldeInitial { get; set; }

        [JsonPropertyName("DateSoldeInitial")]
        public string DateSoldeInitial { get; set; }
    }

    public class Operation
    {
        [JsonPropertyName("DateOperation")]
        public string DateOperation { get; set; }

        [JsonPropertyName("DateValeur")]
        public string DateValeur { get; set; }

        [JsonPropertyName("DescriptionOp")]
        public string DescriptionOp { get; set; }

        [JsonPropertyName("DebitOp")]
        public decimal? DebitOp { get; set; }

        [JsonPropertyName("CreditOp")]
        public decimal? CreditOp { get; set; }
    }

    public class SoldeFinal
    {
        [JsonPropertyName("TotalDebit")]
        public decimal TotalDebit { get; set; }

        [JsonPropertyName("TotalCredit")]
        public decimal TotalCredit { get; set; }

        [JsonPropertyName("Nouveau Solde Au")]
        public decimal NouveauSoldeAu { get; set; }

        [JsonPropertyName("Direction Nouveau Solde Au")]
        public string DirectionNouveauSoldeAu { get; set; }

        [JsonPropertyName("Date Nouveau Solde Au")]
        public string DateNouveauSoldeAu { get; set; }
    }

    public class ExtractionMetadata
    {
        [JsonPropertyName("field_metadata")]
        public List<object> FieldMetadata { get; set; }

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("num_pages_extracted")]
        public int NumPagesExtracted { get; set; }

        [JsonPropertyName("num_document_tokens")]
        public int NumDocumentTokens { get; set; }

        [JsonPropertyName("num_output_tokens")]
        public int NumOutputTokens { get; set; }
    }
} 
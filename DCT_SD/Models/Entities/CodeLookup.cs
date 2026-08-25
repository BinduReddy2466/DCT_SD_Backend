namespace DCT_SD.Models.Entities;

// Generic lookup table (replaces the old RegistryOffices/DocumentTypes/TitleSequenceLookups
// tables), discriminated by LookupType. Per-type conventions observed in the live data:
//   RegistryOffice: Code = registry code (e.g. "004"), Name = registry name, DataJson = null
//   DocumentType:   Code = document code (e.g. "0001"), Name = document name, DataJson = null
//   TitleSequence:  Code = "{Title}|{TitleType}|{Plan}|{Block}|{Lot}" composite key,
//                   Name = the Sequence value, DataJson = structured mirror of the Code parts
// Interpreting Code/Name/DataJson per LookupType is a service-layer concern, not EF Core's.
public class CodeLookup : AuditableEntity
{
    public string LookupType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public bool IsActive { get; set; } = true;
}

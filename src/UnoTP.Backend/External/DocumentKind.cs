using System.Text.Json.Serialization;

namespace UnoTP.Backend.External;

/// <summary>The documents the outside services are asked about.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentKind>))]
public enum DocumentKind
{
    PanCard,
    ProofOfAddress,
    Cheque,
}

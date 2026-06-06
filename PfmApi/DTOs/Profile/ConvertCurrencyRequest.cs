namespace PfmApi.DTOs.Profile;

public class ConvertCurrencyRequest
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

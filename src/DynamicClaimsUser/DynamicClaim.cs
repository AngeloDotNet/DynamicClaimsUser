namespace DynamicClaimsUser;

public class DynamicClaim
{
	public int Id { get; set; }
	public string Type { get; set; } = null!;
	public string Value { get; set; } = null!;
}